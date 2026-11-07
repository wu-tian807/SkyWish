using System;
using System.Threading;
using System.Threading.Tasks;
using CityDirector;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1) return 2;
        using (var bridge = new PipeBridge(args[0], Console.Error.WriteLine))
        {
            bridge.Start();
            Console.WriteLine("READY");
            var done = Task.Run(() => Console.ReadLine());
            while (!done.IsCompleted)
            {
                if (args.Length == 1) bridge.Drain(item => new { transportProbe = true, method = item.Method, echo = item.Parameters });
                Thread.Sleep(5);
            }
        }
        return 0;
    }
}
