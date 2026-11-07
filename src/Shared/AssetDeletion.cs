using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace CityDirector
{
    [DataContract]
    internal sealed class DeletionJob
    {
        [DataMember] public string assetId { get; set; }
        [DataMember] public string token { get; set; }
        [DataMember] public string state { get; set; }
        [DataMember] public string error { get; set; }
        [DataMember] public string updatedAt { get; set; }
        [DataMember] public int processId { get; set; }
        [DataMember] public long processStartTicks { get; set; }
        [DataMember] public long estimatedBytes { get; set; }
        [DataMember] public long deletedBytes { get; set; }
    }

    // No game dependencies. Shared by the mod and the after-exit worker.
    // Never use manifest, receipt or provenance paths as deletion targets.
    internal static class AssetDeletion
    {
        internal static string Id(string id)
        {
            if (id == null || !Regex.IsMatch(id, @"^CD_[A-Za-z][A-Za-z0-9]{0,39}[a-f0-9]{32}$"))
                throw new ArgumentException("Invalid managed asset id.");
            return id;
        }
        internal static string Full(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        internal static string Under(string root, params string[] parts)
        {
            string parent = Full(root), target = Full(Path.Combine(new[] { parent }.Concat(parts).ToArray()));
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Deletion target escapes its managed root.");
            CheckAncestors(target);
            return target;
        }
        internal static void CheckAncestors(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Refusing linked path: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        internal static string JobPath(string root, string id) => Under(root, "deletions", Id(id) + ".json");
        internal static DeletionJob Read(string root, string id)
        {
            string file = JobPath(root, id);
            if (!File.Exists(file)) return null;
            using (var stream = File.OpenRead(file))
            {
                var job = (DeletionJob)new DataContractJsonSerializer(typeof(DeletionJob)).ReadObject(stream);
                if (job.assetId != id) throw new IOException("Deletion record id mismatch.");
                return job;
            }
        }
        internal static void Write(string root, DeletionJob job)
        {
            string file = JobPath(root, job.assetId);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            job.updatedAt = DateTime.UtcNow.ToString("o");
            using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(DeletionJob)).WriteObject(stream, job);
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
        internal static FileStream Lock(string root, string id)
        {
            string file = Under(root, "deletions", Id(id) + ".lock");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            return new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        // A worker owns the lock for its entire lifetime, including waiting for exit.
        // The persisted waiting state alone is not evidence that a worker is alive.
        internal static void Reconcile(string root, string id)
        {
            var job = Read(root, id);
            if (job == null || (job.state != "waiting_for_exit" && job.state != "deleting")) return;
            if (DateTime.TryParse(job.updatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var updated)
                && DateTime.UtcNow - updated.ToUniversalTime() < TimeSpan.FromSeconds(15)) return;
            FailIfUnlocked(root, id, job.token, "清理程序已停止，删除未完成。请点击重试删除；不需要重复退出游戏。");
        }
        internal static bool FailIfUnlocked(string root, string id, string token, string error)
        {
            FileStream lease;
            try { lease = Lock(root, id); }
            catch (IOException) { return false; } // An active worker owns the lock.
            using (lease)
            {
                var job = Read(root, id);
                if (job == null || job.token != token || (job.state != "waiting_for_exit" && job.state != "deleting")) return false;
                job.state = "failed"; job.error = error;
                Write(root, job);
                return true;
            }
        }
        internal static string[] Targets(string root, string gameData, string id)
        {
            Id(id);
            return new[] {
                Under(gameData, "ImportedData", id),
                Under(root, "imports", id), Under(root, "sources", id), Under(root, "native-backups", id),
                Under(root, "receipts", id + ".json"), Under(root, "state", id + ".json"),
                Under(root, "menus", id + ".json"),
                Under(root, "catalog", id + ".json") // Remove the visible entry last; failed jobs remain retryable.
            };
        }
        private static string HashFile(string file)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        internal static string PrepareWorker(string root, string installedWorker)
        {
            if (!File.Exists(installedWorker)) throw new IOException("删除工具未安装，请完整更新模组。");
            string hash = HashFile(installedWorker);
            string runtime = Under(root, "cleanup-runtime", hash);
            Directory.CreateDirectory(runtime);
            string executable = Under(runtime, "CityDirector.Cleanup.exe");
            if (!File.Exists(executable)) File.Copy(installedWorker, executable, false);
            if (HashFile(executable) != hash) throw new IOException("删除工具校验失败，请完整更新模组。");
            string config = installedWorker + ".config";
            if (File.Exists(config)) File.Copy(config, Under(runtime, "CityDirector.Cleanup.exe.config"), true);
            return executable;
        }
        private static void Inventory(string target, List<string> files, List<string> directories)
        {
            CheckAncestors(target);
            if (Directory.Exists(target))
            {
                foreach (string entry in Directory.GetFileSystemEntries(target)) Inventory(entry, files, directories);
                directories.Add(target); // Postorder; no recursive delete that might follow a junction.
            }
            else if (File.Exists(target)) files.Add(target);
        }
        internal static long Estimate(string root, string gameData, string id)
        {
            var files = new List<string>(); var dirs = new List<string>();
            foreach (string target in Targets(root, gameData, id)) Inventory(target, files, dirs);
            return files.Sum(f => new FileInfo(f).Length);
        }
        internal static void Execute(string root, string gameData, DeletionJob job)
        {
            // Preflight the entire scope before the first mutation, including nested links.
            Estimate(root, gameData, job.assetId);
            foreach (string target in Targets(root, gameData, job.assetId))
            {
                var files = new List<string>(); var dirs = new List<string>();
                Inventory(target, files, dirs);
                foreach (string file in files)
                {
                    CheckAncestors(file);
                    long bytes = new FileInfo(file).Length;
                    File.Delete(file);
                    job.deletedBytes += bytes;
                }
                foreach (string dir in dirs) { CheckAncestors(dir); Directory.Delete(dir, false); }
            }
            // A concurrent write must be reported, never called successful.
            if (Targets(root, gameData, job.assetId).Any(p => File.Exists(p) || Directory.Exists(p)))
                throw new IOException("Some asset files remain. Retry deletion after all writers exit.");
        }
        internal static bool SameProcess(int pid, long ticks)
        {
            try { using (var p = Process.GetProcessById(pid)) return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == ticks; }
            catch (ArgumentException) { return false; }
        }
        internal static bool AnyGameRunning()
        {
            var games = Process.GetProcessesByName("Cities2");
            try { return games.Length > 0; }
            finally { foreach (var game in games) game.Dispose(); }
        }
    }
}
