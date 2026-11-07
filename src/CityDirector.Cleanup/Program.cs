using System;
using System.Diagnostics;
using System.IO;

namespace CityDirector
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 4) return 2; // library root, game data root, asset id, immutable request token
            string root = args[0], gameData = args[1], id = args[2], token = args[3];
            DeletionJob job = null;
            try
            {
                using (AssetDeletion.Lock(root, id))
                {
                    job = AssetDeletion.Read(root, id);
                    if (job == null || job.token != token || job.state != "waiting_for_exit") return 3;
                    try
                    {
                        if (AssetDeletion.SameProcess(job.processId, job.processStartTicks))
                            using (var process = Process.GetProcessById(job.processId)) process.WaitForExit();
                        // Also covers a new CS2 process started while this worker was waiting.
                        if (AssetDeletion.AnyGameRunning()) throw new IOException("游戏正在运行；请在资产库中重试，退出游戏后执行删除。");
                        job.state = "deleting"; AssetDeletion.Write(root, job);
                        AssetDeletion.Execute(root, gameData, job);
                        job.state = "deleted"; job.error = null;
                    }
                    catch (Exception e) { job.state = "failed"; job.error = e.Message; }
                    AssetDeletion.Write(root, job);
                    return job.state == "deleted" ? 0 : 1;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                try { AssetDeletion.FailIfUnlocked(root, id, token, "删除工具异常退出：" + e.Message); }
                catch (Exception reportError) { Console.Error.WriteLine("Cannot record failure: " + reportError.Message); }
                return 1;
            }
        }
    }
}
