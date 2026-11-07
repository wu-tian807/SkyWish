using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityDirector
{
    internal static class AssetLibrary
    {
        internal static string Root { get; private set; }
        internal static string Error { get; private set; }
        internal static void Initialize()
        {
            try
            {
                if (string.IsNullOrEmpty(Mod.InstallDirectory)) throw new InvalidOperationException("游戏未提供模组安装路径。");
                var file = Path.Combine(Mod.InstallDirectory, "CityDirector.settings.json");
                Root = Path.GetFullPath((string)JObject.Parse(File.ReadAllText(file))["libraryRoot"]);
                Directory.CreateDirectory(Root);
                Error = null;
            }
            catch (Exception e) { Root = null; Error = "资产库尚未配置：" + e.Message; }
        }
        internal static string Id(string id)
        {
            if (id == null || !Regex.IsMatch(id, @"^CD_[A-Za-z][A-Za-z0-9]{0,39}[a-f0-9]{32}$"))
                throw new ArgumentException("Invalid managed asset id.");
            return id;
        }
        internal static string FileFor(string area, string id) => Path.Combine(Root ?? throw new InvalidOperationException(Error), area, Id(id) + ".json");
        internal static JObject Read(string file) => File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject();
        internal static void Write(string file, JObject data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, data.ToString(Formatting.Indented));
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
        internal static JObject Manifest(string id)
        {
            var data = Read(FileFor("catalog", id));
            if ((string)data["id"] != id) throw new InvalidOperationException("No staged asset with this id.");
            return data;
        }
        internal static bool AllowImports => Root != null && (bool?)Read(Path.Combine(Root, "policy.json"))["allowImports"] != false;
        internal static void SetAllowImports(bool allow) => Write(Path.Combine(Root, "policy.json"), new JObject { ["allowImports"] = allow });
        internal static bool Archived(string id) => (bool?)Read(FileFor("state", id))["archived"] == true;
        internal static void Archive(string id, bool archived) => Write(FileFor("state", id), new JObject { ["archived"] = archived, ["updatedAt"] = DateTime.UtcNow.ToString("o") });
        internal static string Hash(string file)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        internal static string VerifyPackage(string id, JObject manifest)
        {
            string dir = Path.Combine(Root, "imports", id);
            if (!Directory.Exists(dir)) throw new InvalidOperationException("Staged package is missing.");
            // Refuse junctions/symlinks in the entire path; the importer must read the package we verified.
            for (var ancestor = new DirectoryInfo(dir); ancestor != null; ancestor = ancestor.Parent)
                if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse paths are not supported for asset imports.");
            var entries = manifest["files"] as JArray ?? throw new InvalidOperationException("Missing file inventory.");
            if (Directory.GetFileSystemEntries(dir).Length != entries.Count) throw new InvalidOperationException("Package file inventory changed.");
            bool mesh = false, color = false, normal = false, mask = false;
            foreach (JObject entry in entries)
            {
                string name = (string)entry["name"];
                if (name == null || Path.GetFileName(name) != name ||
                    !Regex.IsMatch(name, "^" + Regex.Escape(id) + @"(\.fbx|_(BaseColor|Normal|MaskMap|ControlMask|Emissive)\.png)$"))
                    throw new InvalidOperationException("Unsupported file in static-object package.");
                string file = Path.Combine(dir, name);
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file).Length != (long)entry["bytes"] || Hash(file) != (string)entry["sha256"])
                    throw new InvalidOperationException("Package hash mismatch: " + name);
                mesh |= name == id + ".fbx"; color |= name == id + "_BaseColor.png";
                normal |= name == id + "_Normal.png"; mask |= name == id + "_MaskMap.png";
            }
            if (!(mesh && color && normal && mask)) throw new InvalidOperationException("Required mesh or textures are missing.");
            return dir;
        }
    }
}
