// Native import call, factory inspection and prefab-list lookup adapted from nachocheesedog/cs2-mcp (MIT).
// Attribution and license: src/asset-pipeline/vendor/LICENSE.nachocheesedog; THIRD_PARTY_NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI.Editor;
using Newtonsoft.Json.Linq;

namespace CityDirector
{
    public sealed partial class DirectorSystem
    {
        private Task<AssetImportPanel.PrefabFactory> importTask;
        private JObject importReceipt;
        private string importSession;
        private bool importFaultReported;
        private static readonly FieldInfo prefabListField = typeof(PrefabSystem).GetField("m_Prefabs", BindingFlags.NonPublic | BindingFlags.Instance);
        private IEnumerable<PrefabBase> AllPrefabs() => prefabListField?.GetValue(World.GetOrCreateSystemManaged<PrefabSystem>()) as IEnumerable<PrefabBase>
            ?? throw new InvalidOperationException("Game prefab registry layout is incompatible with this mod.");
        private ObjectPrefab ImportedObject(string id) => AllPrefabs().OfType<ObjectPrefab>().FirstOrDefault(p => p != null && p.name == id);

        private object ImportStatus(string id)
        {
            AssetLibrary.Id(id);
            var receipt = AssetLibrary.Read(AssetLibrary.FileFor("receipts", id));
            if (!receipt.HasValues) return new { assetId = id, state = "not_submitted" };
            var result = (JObject)receipt.DeepClone();
            if ((string)result["state"] == "importing" && (importTask == null || (string)importReceipt["assetId"] != id))
                result["state"] = "interrupted_unknown"; // Never rerun a partially committed import after a restart.
            result["registeredNow"] = ImportedObject(id) != null;
            result["saveReloadVerified"] = false;
            result["deletionAllowed"] = true;
            result["deletionState"] = AssetDeletion.Read(AssetLibrary.Root, id)?.state;
            if (ImportedObject(id) is ObjectGeometryPrefab geometry)
            {
                try { result["materialsNow"] = InspectMaterials(geometry); result["materialBindingsVerifiedNow"] = true; }
                catch (Exception e) { result["materialBindingsVerifiedNow"] = false; result["materialErrorNow"] = e.Message; }
            }
            return result;
        }
        private object StartImport(string id)
        {
            AssetLibrary.Id(id);
            if (AssetDeletion.Read(AssetLibrary.Root, id) != null)
                throw new InvalidOperationException("This asset has a deletion request. Use a new asset id for a new import.");
            if (File.Exists(AssetLibrary.FileFor("receipts", id))) return ImportStatus(id);
            if (!AssetLibrary.AllowImports) throw new InvalidOperationException("Imports are disabled in the in-game asset manager.");
            if (AssetLibrary.Archived(id)) throw new InvalidOperationException("Restore the archived asset before importing.");
            if (importTask != null) throw new InvalidOperationException("Another import is still running. Poll it before starting a new one.");
            var game = GameManager.instance;
            if (game == null || game.gameMode != GameMode.Game || game.isGameLoading) throw new InvalidOperationException("Load a city before importing a new asset.");
            var manifest = AssetLibrary.Manifest(id);
            string dir = AssetLibrary.VerifyPackage(id, manifest);
            // CD_ is our namespace, but CS2 only recognizes such prefixes after
            // registering them as themes. Otherwise _Bench... becomes the texture suffix.
            Colossal.AssetPipeline.AssetUtils.AddSupportedThemes(new[] { "CD" });
            foreach (string suffix in new[] { "BaseColor", "Normal", "MaskMap" })
            {
                Colossal.AssetPipeline.AssetUtils.ParseName(id + "_" + suffix, out var theme, out _, out _, out _, out _, out _, out _, out var parsedSuffix);
                if (theme != "CD" || parsedSuffix != "_" + suffix)
                    throw new InvalidOperationException("Native texture filename parsing is incompatible: " + id + "_" + suffix);
            }
            string nativeDir = NativeDirectory(id);
            if (Directory.Exists(nativeDir) || AllPrefabs().Any(p => p != null && p.name.StartsWith(id, StringComparison.Ordinal)))
                throw new InvalidOperationException("A native asset with this id already exists. Overwrite is forbidden.");
            importReceipt = new JObject { ["assetId"] = id, ["state"] = "importing", ["startedAt"] = DateTime.UtcNow.ToString("o"),
                ["session"] = session, ["nativeDirectory"] = nativeDir, ["saveReloadVerified"] = false,
                ["deletionAllowed"] = true, ["saveUsage"] = "unknown", ["preset"] = "StaticObject" };
            // Persist the reservation BEFORE invoking any game mutations.
            AssetLibrary.Write(AssetLibrary.FileFor("receipts", id), importReceipt);
            importSession = session;
            importFaultReported = false;
            try
            {
                importTask = AssetImportPanel.ImportAssets(Path.GetDirectoryName(dir), dir, id,
                    AssetImportPanel.Preset.StaticObject, null, World, Mod.Log, null);
            }
            catch (Exception e) { importReceipt["state"] = "failed_partial_possible"; importReceipt["error"] = e.Message; PersistReceipt(); throw; }
            return importReceipt.DeepClone();
        }
        private static string NativeDirectory(string id) => Path.Combine(UnityEngine.Application.persistentDataPath,
            "ImportedData", AssetLibrary.Id(id));
        private void PersistReceipt() => AssetLibrary.Write(AssetLibrary.FileFor("receipts", (string)importReceipt["assetId"]), importReceipt);

        private void PumpImport()
        {
            if (importTask == null || !importTask.IsCompleted) return;
            string id = (string)importReceipt["assetId"];
            try
            {
                var factory = importTask.GetAwaiter().GetResult();
                if (importSession != session) throw new InvalidOperationException("City changed during import; partial files are retained for inspection.");
                var obj = ImportedObject(id) ?? throw new InvalidOperationException("Importer did not register the expected object prefab.");
                var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
                if (!prefabs.TryGetEntity(obj, out _)) return; // Allow native prefab initialization to run on subsequent frames.
                if (!(obj is ObjectGeometryPrefab geometry) || geometry.m_Meshes == null ||
                    !geometry.m_Meshes.Any(m => m?.m_Mesh is RenderPrefab rp && rp.hasGeometryAsset))
                    throw new InvalidOperationException("Registered prefab has no geometry asset.");
                if (obj.asset == null || obj.asset.database != AssetDatabase.user) throw new InvalidOperationException("Imported prefab is not a persisted user asset.");
                obj.asset.MarkDirty(); obj.asset.Save(force: true);
                importReceipt["materials"] = InspectMaterials(geometry);
                importReceipt["materialBindingsVerified"] = true;
                importReceipt["prefabName"] = obj.name;
                importReceipt["prefabAssetPath"] = obj.asset.path.ToString();
                importReceipt["renderPrefabs"] = factory.prefabs.Count;
                importReceipt["nativeFiles"] = BackupNativeFiles(id);
                importReceipt["state"] = "imported";
                importReceipt["completedAt"] = DateTime.UtcNow.ToString("o");
                importReceipt["registeredNow"] = true;
                importReceipt["persistedByImporter"] = true;
                PersistReceipt();
                Mod.Log.Info("Asset imported: " + id + ". Save reload verification is still required.");
            }
            catch (Exception e)
            {
                importReceipt["state"] = "failed_partial_possible";
                importReceipt["error"] = e.GetBaseException().Message;
                try { PersistReceipt(); }
                catch (Exception diskError) { if (!importFaultReported) Mod.Log.Error(diskError, "Cannot persist import receipt; files retained."); importFaultReported = true; }
                Mod.Log.Error(e, "Import failed; retained files for inspection.");
            }
            // Native importer can select its placement tool; we never submit a placement and restore normal selection.
            try
            {
                var tools = World.GetOrCreateSystemManaged<ToolSystem>();
                if (tools.activeTool is ObjectToolSystem) tools.activeTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            }
            catch (Exception e) { Mod.Log.Warn("Could not restore default tool: " + e.Message); }
            importTask = null;
        }
        private static JArray InspectMaterials(ObjectGeometryPrefab geometry)
        {
            var result = new JArray();
            foreach (var mesh in geometry.m_Meshes)
            {
                if (!(mesh?.m_Mesh is RenderPrefab render)) continue;
                int materialIndex = 0;
                foreach (var surface in render.surfaceAssets)
                {
                    if (surface == null) throw new InvalidOperationException("Imported mesh has a missing surface.");
                    surface.LoadProperties(useVT: false);
                    try
                    {
                        var slots = new JObject();
                        foreach (string slot in new[] { "_BaseColorMap", "_NormalMap", "_MaskMap" })
                        {
                            if (surface.textures == null || !surface.textures.TryGetValue(slot, out var texture) || texture == null || texture.isDummy)
                                throw new InvalidOperationException("Imported surface is missing texture binding " + slot + "; import is incomplete.");
                            slots[slot] = texture.path.ToString();
                        }
                        var live = render.ObtainMaterial(materialIndex++);
                        var runtimeSlots = new JObject();
                        foreach (string slot in new[] { "_BaseColorMap", "_NormalMap", "_MaskMap" })
                        {
                            bool supported = live != null && live.HasProperty(slot);
                            var bound = supported ? live.GetTexture(slot) : null;
                            runtimeSlots[slot] = new JObject { ["supported"] = supported, ["texture"] = bound?.name,
                                ["width"] = bound?.width, ["height"] = bound?.height };
                        }
                        result.Add(new JObject { ["surface"] = surface.path.ToString(), ["textures"] = slots,
                            ["templateHash"] = surface.materialTemplateHash, ["shader"] = live?.shader?.name,
                            ["runtimeTextures"] = runtimeSlots });
                    }
                    finally { surface.UnloadProperties(); }
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("Imported mesh has no materials.");
            return result;
        }
        private JArray BackupNativeFiles(string id)
        {
            string nativeDir = NativeDirectory(id);
            if (!Directory.Exists(nativeDir)) throw new InvalidOperationException("Expected ImportedData directory is missing; persistence layout needs inspection.");
            var result = new JArray();
            string backup = Path.Combine(AssetLibrary.Root, "native-backups", id);
            foreach (string file in Directory.GetFiles(nativeDir, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(nativeDir.Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
                string hash = AssetLibrary.Hash(file);
                if (hash != AssetLibrary.Hash(target)) throw new IOException("Native backup changed during copy.");
                result.Add(new JObject { ["relativePath"] = relative, ["sha256"] = hash, ["bytes"] = new FileInfo(file).Length });
            }
            if (result.Count == 0) throw new InvalidOperationException("Native asset folder is empty.");
            importReceipt["nativeBackupDirectory"] = backup;
            importReceipt["dependencyAudit"] = "own-import-folder-backed-up; external-runtime-dependencies-not-audited";
            return result;
        }
    }
}
