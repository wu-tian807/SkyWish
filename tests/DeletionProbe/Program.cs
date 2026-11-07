using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CityDirector;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "hold")
        {
            using (var p = Process.GetCurrentProcess()) Console.WriteLine(p.StartTime.ToUniversalTime().Ticks);
            while (!File.Exists(args[1])) Thread.Sleep(50);
            return 0;
        }
        if (args.Length == 1 && args[0] == "game-running") { Console.WriteLine(AssetDeletion.AnyGameRunning()); return 0; }
        if (args.Length == 3 && args[0] == "prepare-worker") { Console.WriteLine(AssetDeletion.PrepareWorker(args[1], args[2])); return 0; }
        if (args.Length != 4) return 2;
        try
        {
            if (args[0] == "reconcile") AssetDeletion.Reconcile(args[1], args[3]);
            else if (args[0] == "estimate") Console.WriteLine(AssetDeletion.Estimate(args[1], args[2], args[3]));
            else if (args[0] == "execute")
            {
                var job = new DeletionJob { assetId = args[3], state = "deleting" };
                AssetDeletion.Execute(args[1], args[2], job);
                Console.WriteLine(job.deletedBytes);
            }
            else return 2;
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
