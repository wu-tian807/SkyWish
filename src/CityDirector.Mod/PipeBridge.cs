using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityDirector
{
    // Transport owns no Unity/ECS objects. Only DirectorSystem drains the queue.
    internal sealed class PipeBridge : IDisposable
    {
        public const string DefaultName = "city-director-v1";
        private readonly string name;
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();
        private readonly ConcurrentDictionary<NamedPipeServerStream, byte> clients = new ConcurrentDictionary<NamedPipeServerStream, byte>();
        private readonly ConcurrentQueue<PendingRead> pending = new ConcurrentQueue<PendingRead>();
        private readonly Action<string> log;
        private int queued;

        public PipeBridge(string name, Action<string> log) { this.name = name; this.log = log; }

        public void Start()
        {
            // Create the first listener synchronously so startup failures are observable.
            var first = CreatePipe();
            LaunchWorker(first);
            for (int i = 1; i < 4; i++) LaunchWorker(null);
        }

        private void LaunchWorker(NamedPipeServerStream first) => Task.Run(async () =>
        {
            try { await Worker(first).ConfigureAwait(false); }
            catch (Exception e) { log("Pipe worker stopped: " + e); }
        });

        private NamedPipeServerStream CreatePipe()
        {
            var acl = new PipeSecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new PipeAccessRule(CurrentProcessUser(), PipeAccessRights.FullControl, AccessControlType.Allow));
            var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 4, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 4096, 4096, acl);
            clients.TryAdd(pipe, 0);
            if (stopping.IsCancellationRequested) { pipe.Dispose(); throw new OperationCanceledException(); }
            return pipe;
        }

        // Unity's Mono throws NotImplementedException for WindowsIdentity.User.
        // Read the process token directly; retain the same user-only pipe ACL.
        private static SecurityIdentifier CurrentProcessUser()
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x0008, out var token))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr buffer = IntPtr.Zero;
            try
            {
                GetTokenInformation(token, 1, IntPtr.Zero, 0, out var size);
                if (size <= 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                buffer = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, 1, buffer, size, out size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
            }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); CloseHandle(token); }
        }
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr information, int length, out int required);

        private async Task Worker(NamedPipeServerStream first)
        {
            while (!stopping.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = first ?? CreatePipe();
                    first = null;
                    await pipe.WaitForConnectionAsync(stopping.Token).ConfigureAwait(false);
                    using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))
                    {
                        deadline.CancelAfter(TimeSpan.FromSeconds(5));
                        // Disposing also interrupts Mono pipe operations that ignore cancellation.
                        using (deadline.Token.Register(() => pipe.Dispose()))
                        {
                            string line = await ReadFrame(pipe, deadline.Token).ConfigureAwait(false);
                            JObject request;
                            try { request = JObject.Parse(line); }
                            catch (JsonException) { await Write(pipe, Failure(null, "invalid_request", "Expected a JSON object."), deadline.Token); continue; }
                            string id = request["id"]?.Type == JTokenType.String ? (string)request["id"] : null;
                            string method = request["method"]?.Type == JTokenType.String ? (string)request["method"] : null;
                            if (id == null || id.Length > 100 || request["schemaVersion"]?.Type != JTokenType.Integer || (int)request["schemaVersion"] != 1)
                            { await Write(pipe, Failure(id, "invalid_request", "Expected schemaVersion 1 and a string id."), deadline.Token); continue; }
                            if (method == "bridge.ping")
                            {
                                await Write(pipe, Success(id, new { bridgeVersion = "0.3.4", readOnly = false,
                                    implemented = new[] { "bridge.ping", "game.state", "assets.search", "assets.import", "assets.import_status", "menus.tree", "menus.assets", "menus.assign" },
                                    planned = new[] { "scene.edit", "transit.edit", "director.play" } }), deadline.Token);
                                continue;
                            }
                            if (method != "game.state" && method != "assets.search" && method != "assets.import" && method != "assets.import_status" && method != "menus.tree" && method != "menus.assets" && method != "menus.assign")
                            { await Write(pipe, Failure(id, "unsupported_method", "Unsupported bridge method."), deadline.Token); continue; }
                            if (request["params"] != null && !(request["params"] is JObject))
                            { await Write(pipe, Failure(id, "invalid_request", "params must be an object."), deadline.Token); continue; }
                            if (Interlocked.Increment(ref queued) > 64)
                            { Interlocked.Decrement(ref queued); await Write(pipe, Failure(id, "busy", "Game queue is full."), deadline.Token); continue; }
                            var item = new PendingRead(id, method, (JObject)request["params"] ?? new JObject());
                            pending.Enqueue(item);
                            using (deadline.Token.Register(() => item.Completion.TrySetCanceled()))
                            {
                                var result = await item.Completion.Task.ConfigureAwait(false);
                                await Write(pipe, result, deadline.Token).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
                catch (Exception e) { log("Pipe request failed: " + e.GetType().Name); }
                finally { if (pipe != null) { clients.TryRemove(pipe, out _); pipe.Dispose(); } }
            }
        }

        private static async Task<string> ReadFrame(Stream stream, CancellationToken token)
        {
            var bytes = new byte[8192];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes, count, bytes.Length - count, token).ConfigureAwait(false);
                if (read == 0) throw new IOException("Disconnected before request.");
                int end = Array.IndexOf(bytes, (byte)'\n', count, read);
                if (end >= 0) return new UTF8Encoding(false, true).GetString(bytes, 0, end);
                count += read;
            }
            throw new IOException("Request exceeds 8 KiB.");
        }

        private static async Task Write(Stream stream, object response, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response) + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        public void Drain(Func<PendingRead, object> handler)
        {
            for (int i = 0; i < 8 && pending.TryDequeue(out var item); i++)
            {
                Interlocked.Decrement(ref queued);
                if (item.Completion.Task.IsCompleted) continue;
                if (DateTime.UtcNow > item.ExpiresAt)
                { item.Completion.TrySetResult(Failure(item.Id, "expired", "Request expired before game update.")); continue; }
                try { item.Completion.TrySetResult(Success(item.Id, handler(item))); }
                catch (Exception e) { item.Completion.TrySetResult(Failure(item.Id, "game_unavailable", e.Message)); }
            }
        }

        public static object Success(string id, object result) => new { schemaVersion = 1, id, ok = true, result };
        public static object Failure(string id, string code, string message) => new { schemaVersion = 1, id, ok = false, error = new { code, message } };
        public void Dispose()
        {
            if (stopping.IsCancellationRequested) return;
            stopping.Cancel();
            foreach (var pipe in clients.Keys) pipe.Dispose();
            while (pending.TryDequeue(out var item)) { Interlocked.Decrement(ref queued); item.Completion.TrySetCanceled(); }
        }
    }

    internal sealed class PendingRead
    {
        public readonly string Id;
        public readonly string Method;
        public readonly JObject Parameters;
        public readonly DateTime ExpiresAt = DateTime.UtcNow.AddSeconds(4);
        public readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        public PendingRead(string id, string method, JObject parameters) { Id = id; Method = method; Parameters = parameters; }
    }
}
