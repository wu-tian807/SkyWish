// Icon/locale integration follows nachocheesedog/cs2-mcp (MIT); see THIRD_PARTY_NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using Colossal.Localization;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.InGame;
using Newtonsoft.Json.Linq;
using Unity.Entities;

namespace CityDirector
{
    public sealed partial class DirectorSystem
    {
        private readonly Dictionary<string, string> appliedMenus = new Dictionary<string, string>();
        private readonly Dictionary<string, Tuple<string, MemorySource>> menuLocales = new Dictionary<string, Tuple<string, MemorySource>>();
        private readonly Dictionary<string, string> menuIconHosts = new Dictionary<string, string>();
        private float nextMenuRefresh;
        private static string MenuKey(PrefabBase prefab) => prefab.GetType().Name + ":" + prefab.name;
        private static string MenuTitle(PrefabBase prefab)
        {
            var dictionary = GameManager.instance.localizationManager.activeDictionary;
            string prefix = prefab is UIAssetCategoryPrefab ? "SubServices.NAME[" : prefab is UIAssetMenuPrefab ? "Services.NAME[" : "Assets.NAME[";
            return dictionary.TryGetValue(prefix + prefab.name + "]", out var title) ? title : prefab.name;
        }
        private PrefabSystem Prefabs => World.GetOrCreateSystemManaged<PrefabSystem>();
        private UIAssetCategoryPrefab Category(string key)
        {
            var matches = AllPrefabs().OfType<UIAssetCategoryPrefab>().Where(p => p != null && MenuKey(p) == key).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Category unavailable or ambiguous. Query director_menu_tree again.");
            var cat = matches[0];
            if (cat.m_Menu == null || !Prefabs.TryGetEntity(cat, out var entity) ||
                !EntityManager.HasComponent<UIAssetCategoryData>(entity) || !EntityManager.HasBuffer<UIGroupElement>(entity))
                throw new InvalidOperationException("Category is not initialized in the native build menu.");
            return cat;
        }
        internal JObject MenuTree()
        {
            RequireMenuCity();
            var menus = new JArray();
            var all = AllPrefabs().Where(p => p != null).ToArray();
            foreach (var menu in all.OfType<UIAssetMenuPrefab>().OrderBy(p => p.name))
            {
                if (!Prefabs.TryGetEntity(menu, out _)) continue;
                var categories = new JArray();
                foreach (var cat in all.OfType<UIAssetCategoryPrefab>().Where(c => c.m_Menu == menu).OrderBy(c => c.name))
                {
                    if (!Prefabs.TryGetEntity(cat, out var entity) || !EntityManager.HasBuffer<UIGroupElement>(entity)) continue;
                    var entries = EntityManager.GetBuffer<UIGroupElement>(entity, true);
                    categories.Add(new JObject { ["categoryId"] = MenuKey(cat), ["name"] = cat.name,
                        ["title"] = MenuTitle(cat), ["entryCount"] = entries.Length,
                        ["locked"] = EntityManager.HasComponent<Locked>(entity) && EntityManager.IsComponentEnabled<Locked>(entity),
                        ["source"] = cat.isBuiltin ? "game" : "mod_or_local", ["assignable"] = true });
                }
                menus.Add(new JObject { ["menuId"] = MenuKey(menu), ["name"] = menu.name,
                    ["title"] = MenuTitle(menu), ["categories"] = categories });
            }
            return new JObject { ["session"] = session, ["menus"] = menus,
                ["note"] = "Live native menu registry. Empty/locked categories and user theme/pack filters may affect on-screen visibility. Menu assignment does not change asset functionality." };
        }
        private JObject MenuAssets(JObject args)
        {
            RequireMenuCity();
            var cat = Category(RequiredText(args, "categoryId", 256));
            string query = OptionalText(args, "query", 120) ?? "";
            int offset = Integer(args, "offset", 0, 0, 1000000), limit = Integer(args, "limit", 50, 1, 100);
            var entries = EntityManager.GetBuffer<UIGroupElement>(Prefabs.GetEntity(cat), true);
            var result = new List<JObject>(); var seen = new HashSet<Entity>();
            foreach (var entry in entries)
            {
                if (!seen.Add(entry.m_Prefab) || !Prefabs.TryGetPrefab<PrefabBase>(entry.m_Prefab, out var prefab)) continue;
                string title = MenuTitle(prefab);
                if (query.Length > 0 && prefab.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool managed = IsManagedMenuAsset(prefab.name);
                result.Add(new JObject { ["name"] = prefab.name, ["title"] = title, ["type"] = prefab.GetType().Name,
                    ["assetId"] = managed ? prefab.name : null, ["managed"] = managed,
                    ["icon"] = prefab.TryGet<UIObject>(out var ui) ? ui.m_Icon : null,
                    ["resolvedIcon"] = Game.UI.ImageSystem.GetThumbnail(prefab),
                    ["priority"] = EntityManager.HasComponent<UIObjectData>(entry.m_Prefab) ? EntityManager.GetComponentData<UIObjectData>(entry.m_Prefab).m_Priority : 0,
                    ["locked"] = EntityManager.HasComponent<Locked>(entry.m_Prefab) && EntityManager.IsComponentEnabled<Locked>(entry.m_Prefab) });
            }
            var ordered = result.OrderBy(p => (int)p["priority"]).ThenBy(p => (string)p["name"]).ToArray();
            return new JObject { ["session"] = session, ["categoryId"] = MenuKey(cat), ["total"] = ordered.Length,
                ["offset"] = offset, ["assets"] = new JArray(ordered.Skip(offset).Take(limit)),
                ["hasMore"] = offset + limit < ordered.Length,
                ["note"] = "Registry entries before user theme/asset-pack filters; not a screenshot of the currently open panel." };
        }
        private static bool IsManagedMenuAsset(string id)
        {
            try { return File.Exists(AssetLibrary.FileFor("catalog", id)); } catch (ArgumentException) { return false; }
        }
        private static void RequireMenuCity()
        {
            var game = GameManager.instance;
            if (game == null || game.gameMode != GameMode.Game || game.isGameLoading) throw new InvalidOperationException("Load a city before editing or querying build menus.");
        }
        internal JObject AssignCategory(JObject args)
        {
            RequireMenuCity();
            string id = AssetLibrary.Id(RequiredText(args, "assetId", 100));
            var manifest = AssetLibrary.Manifest(id);
            if (AssetDeletion.Read(AssetLibrary.Root, id) != null) throw new InvalidOperationException("Asset has a deletion request.");
            if (importTask != null) throw new InvalidOperationException("Wait for the active import to finish.");
            var prefab = ImportedObject(id) ?? throw new InvalidOperationException("Import this asset before assigning its menu category.");
            if (prefab.asset == null || prefab.asset.database != AssetDatabase.user) throw new InvalidOperationException("Only managed user assets can be assigned.");
            var category = Category(RequiredText(args, "categoryId", 256));
            // Check the toolbar version before changing live membership.
            ToolbarRefreshActions();
            var previous = AssetLibrary.Read(AssetLibrary.FileFor("menus", id));
            var config = new JObject { ["assetId"] = id, ["categoryId"] = MenuKey(category),
                ["title"] = OptionalText(args, "title", 120) ?? (string)previous["title"] ?? (string)manifest["title"] ?? id,
                ["description"] = OptionalText(args, "description", 2000) ?? (string)previous["description"] ?? (string)manifest["description"] ?? "",
                ["priority"] = Integer(args, "priority", (int?)previous["priority"] ?? 1000, -10000, 10000),
                ["state"] = "pending", ["updatedAt"] = DateTime.UtcNow.ToString("o") };
            AssetLibrary.Write(AssetLibrary.FileFor("menus", id), config);
            try { ApplyCategory(config); SetMenuResult(config, "applied", null); }
            catch (Exception e) { SetMenuResult(config, "failed", e.Message); throw; }
            return config;
        }
        private void ApplyCategory(JObject config)
        {
            string id = AssetLibrary.Id((string)config["assetId"]);
            var prefab = ImportedObject(id) ?? throw new InvalidOperationException("Asset is not registered in this session.");
            var category = Category((string)config["categoryId"]);
            config["categoryTitle"] = MenuTitle(category.m_Menu) + " / " + MenuTitle(category);
            if (!Prefabs.TryGetEntity(prefab, out var entity)) throw new InvalidOperationException("Asset entity is not ready.");
            var refresh = ToolbarRefreshActions();
            config["icon"] = ImportMenuIcon(id);
            // Remove all stale memberships of this managed asset, including any duplicate entries.
            foreach (var group in AllPrefabs().OfType<UIAssetCategoryPrefab>().Where(p => p != null).ToArray())
            {
                if (!Prefabs.TryGetEntity(group, out var groupEntity)) continue;
                if (EntityManager.HasBuffer<UIGroupElement>(groupEntity))
                {
                    var buffer = EntityManager.GetBuffer<UIGroupElement>(groupEntity);
                    for (int i = buffer.Length - 1; i >= 0; i--) if (buffer[i].m_Prefab == entity) buffer.RemoveAt(i);
                }
                if (EntityManager.HasBuffer<UnlockRequirement>(groupEntity))
                {
                    var buffer = EntityManager.GetBuffer<UnlockRequirement>(groupEntity);
                    for (int i = buffer.Length - 1; i >= 0; i--) if (buffer[i].m_Prefab == entity) buffer.RemoveAt(i);
                }
            }
            var ui = prefab.AddOrGetComponent<UIObject>();
            ui.active = true; ui.m_IsDebugObject = false; ui.m_Group = category;
            ui.m_Icon = (string)config["icon"]; ui.m_Priority = (int)config["priority"];
            if (!EntityManager.HasComponent<UIObjectData>(entity)) EntityManager.AddComponent<UIObjectData>(entity);
            EntityManager.SetComponentData(entity, new UIObjectData { m_Group = Prefabs.GetEntity(category), m_Priority = ui.m_Priority });
            category.AddElement(EntityManager, entity);
            var lm = GameManager.instance.localizationManager;
            if (menuLocales.TryGetValue(id, out var old)) lm.RemoveSource(old.Item1, old.Item2);
            var source = new MemorySource(new Dictionary<string, string> { ["Assets.NAME[" + id + "]"] = (string)config["title"], ["Assets.DESCRIPTION[" + id + "]"] = (string)config["description"] });
            lm.AddSource(lm.activeLocaleId, source); menuLocales[id] = Tuple.Create(lm.activeLocaleId, source);
            foreach (var action in refresh) action();
            // Category references are replayed from our config, never baked into the native prefab.
            // Removing another mod's category must not make this model's native file unloadable.
            appliedMenus[id] = MenuSignature(config);
        }
        private Action[] ToolbarRefreshActions()
        {
            var toolbar = World.GetOrCreateSystemManaged<ToolbarUISystem>();
            return new[] { "m_ToolbarGroupsBinding", "m_AssetMenuCategoriesBinding", "m_AssetsBinding" }.Select(name =>
            {
                var binding = typeof(ToolbarUISystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(toolbar)
                    ?? throw new InvalidOperationException("Toolbar refresh API changed: " + name);
                var method = binding.GetType().GetMethod(name == "m_ToolbarGroupsBinding" ? "Update" : "UpdateAll", Type.EmptyTypes)
                    ?? throw new InvalidOperationException("Toolbar binding cannot refresh.");
                return (Action)(() => method.Invoke(binding, null));
            }).ToArray();
        }
        private string ImportMenuIcon(string id)
        {
            string file = AssetDeletion.Under(AssetLibrary.Root, "sources", id, "preview.png");
            if (!File.Exists(file)) return "Media/Game/Icons/Props.svg";
            if (new FileInfo(file).Length > 8 * 1024 * 1024) throw new IOException("Preview exceeds 8 MiB.");
            // Newly added ImageAssets are not reliably resolved through the UI's global
            // database in this session. Serve only the managed icon directory through
            // the native local UI host, registered after the complete file is available.
            string hash = AssetLibrary.Hash(file);
            string folder = AssetDeletion.Under(AssetLibrary.Root, "sources", id, "menu-icons");
            Directory.CreateDirectory(folder);
            string icon = AssetDeletion.Under(folder, hash + ".png");
            if (!File.Exists(icon)) File.Copy(file, icon, false);
            if (AssetLibrary.Hash(icon) != hash) throw new IOException("Menu icon copy failed validation.");
            string host = "citydirector-" + id.ToLowerInvariant();
            var uiSystem = Colossal.UI.UIManager.defaultUISystem
                ?? throw new InvalidOperationException("Native UI is not ready for menu icons.");
            if (!menuIconHosts.TryGetValue(host, out var registered) || registered != folder)
            {
                if (registered != null) uiSystem.RemoveHostLocation(host, registered);
                uiSystem.AddHostLocation(host, folder, shouldWatch: false);
                menuIconHosts[host] = folder;
            }
            return "coui://" + host + "/" + hash + ".png";
        }
        private void ReleaseMenuIconHosts()
        {
            var uiSystem = Colossal.UI.UIManager.defaultUISystem;
            if (uiSystem != null)
                foreach (var host in menuIconHosts) uiSystem.RemoveHostLocation(host.Key, host.Value);
            menuIconHosts.Clear();
        }
        private string MenuSignature(JObject config) => session + "|" + GameManager.instance.localizationManager.activeLocaleId + "|" + (string)config["updatedAt"];
        private static void SetMenuResult(JObject config, string state, string error)
        {
            config["state"] = state; config["error"] = error;
            var path = AssetLibrary.FileFor("menus", (string)config["assetId"]);
            if (!JToken.DeepEquals(AssetLibrary.Read(path), config)) AssetLibrary.Write(path, config);
        }
        private void PumpMenus()
        {
            if (UnityEngine.Time.unscaledTime < nextMenuRefresh || AssetLibrary.Root == null) return;
            nextMenuRefresh = UnityEngine.Time.unscaledTime + 2;
            var game = GameManager.instance;
            if (game == null || game.gameMode != GameMode.Game || game.isGameLoading || importTask != null) return;
            string folder = Path.Combine(AssetLibrary.Root, "menus");
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                JObject config = null;
                try
                {
                    string id = AssetLibrary.Id(Path.GetFileNameWithoutExtension(file));
                    config = AssetLibrary.Read(file);
                    if ((string)config["assetId"] != id || !IsManagedMenuAsset(id) || AssetDeletion.Read(AssetLibrary.Root, id) != null) continue;
                    if (appliedMenus.TryGetValue(id, out var signature) && signature == MenuSignature(config)) continue;
                    ApplyCategory(config); SetMenuResult(config, "applied", null);
                }
                catch (Exception e) { if (config != null) { try { SetMenuResult(config, "unavailable", e.Message); } catch { } } }
            }
        }
        private static string RequiredText(JObject args, string key, int max) => OptionalText(args, key, max) ?? throw new ArgumentException("Missing " + key);
        private static string OptionalText(JObject args, string key, int max)
        {
            var value = args[key]; if (value == null) return null;
            if (value.Type != JTokenType.String || ((string)value).Length > max || (key != "description" && key != "query" && ((string)value).Length == 0)) throw new ArgumentException("Invalid " + key);
            return (string)value;
        }
        private static int Integer(JObject args, string key, int fallback, int min, int max)
        {
            var value = args[key]; if (value == null) return fallback;
            if (value.Type != JTokenType.Integer || (long)value < min || (long)value > max) throw new ArgumentException("Invalid " + key);
            return (int)value;
        }
    }
}
