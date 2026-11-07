using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace CityDirector
{
    public sealed partial class DirectorSystem
    {
        internal long PreviewDeletion(string id)
        {
            AssetLibrary.Manifest(id);
            if (importTask != null) throw new InvalidOperationException("请等待当前导入完成后再删除。");
            return AssetDeletion.Estimate(AssetLibrary.Root, Application.persistentDataPath, id);
        }
        internal DeletionJob RequestDeletion(string id)
        {
            long bytes = PreviewDeletion(id);
            string worker = Path.Combine(Mod.InstallDirectory, "cleanup", "CityDirector.Cleanup.exe");
            // Run the trusted helper beside its managed data. Some installations restrict
            // writes from executables launched inside the game's mod directory.
            string executable = AssetDeletion.PrepareWorker(AssetLibrary.Root, worker);
            string runtime = Path.GetDirectoryName(executable);
            DeletionJob job;
            using (AssetDeletion.Lock(AssetLibrary.Root, id))
            using (var current = Process.GetCurrentProcess())
            {
                var previous = AssetDeletion.Read(AssetLibrary.Root, id);
                job = new DeletionJob { assetId = id, token = Guid.NewGuid().ToString("N"), state = "waiting_for_exit",
                    processId = current.Id, processStartTicks = current.StartTime.ToUniversalTime().Ticks,
                    estimatedBytes = bytes, deletedBytes = previous?.deletedBytes ?? 0 };
                AssetDeletion.Write(AssetLibrary.Root, job);
            }
            try
            {
                using (Process.Start(new ProcessStartInfo(executable,
                    Quote(AssetLibrary.Root) + " " + Quote(Application.persistentDataPath) + " " + Quote(id) + " " + Quote(job.token))
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = runtime })) { }
            }
            catch (Exception e) { job.state = "failed"; job.error = "无法启动删除工具：" + e.Message; AssetDeletion.Write(AssetLibrary.Root, job); throw; }
            return job;
        }
        // These values are absolute directory paths or validated identifiers, never shell commands.
        private static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }
    }
}
