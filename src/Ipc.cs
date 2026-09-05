using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace EasyShut
{
    public sealed class Reply
    {
        public bool Ok;
        public string Message;
        public static Reply Success(string message) { return new Reply { Ok = true, Message = message }; }
        public static Reply Error(string message) { return new Reply { Ok = false, Message = message }; }
    }
    public static class Ipc
    {
        public static string Name
        {
            get
            {
                string name = "EasyShut.v1." + WindowsIdentity.GetCurrent().User.Value + "." + Process.GetCurrentProcess().SessionId;
#if TESTING
                name += ".test";
#endif
                return name;
            }
        }
        public static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
        public static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        public static bool TrySend(string[] args, out Reply reply, int connectTimeout)
        {
            reply = null;
            using (var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                try { pipe.Connect(connectTimeout); }
                catch (TimeoutException) { return false; }
                // Once connected, do not retry an uncertain command (especially +hours).
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true))
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                {
                    writer.AutoFlush = true;
                    string[] encoded = Array.ConvertAll(args, Encode);
                    writer.WriteLine(string.Join("\t", encoded));
                    var read = reader.ReadLineAsync();
                    if (!read.Wait(8000)) throw new IOException("Brak odpowiedzi EasyShut. Sprawdź -status przed ponowieniem polecenia.");
                    string line = read.Result;
                    if (line == null) throw new IOException("Połączenie przerwane. Sprawdź -status przed ponowieniem polecenia.");
                    string[] parts = line.Split(new[] { '\t' }, 2);
                    if (parts.Length != 2) throw new IOException("Nieprawidłowa odpowiedź EasyShut.");
                    reply = new Reply { Ok = parts[0] == "OK", Message = Decode(parts[1]) };
                    return true;
                }
            }
        }
    }

    public sealed class PipeServer : IDisposable
    {
        private readonly Func<string[], Reply> dispatch;
        private readonly object gate = new object();
        private NamedPipeServerStream current;
        private volatile bool stopped;
        public PipeServer(Func<string[], Reply> dispatch)
        {
            this.dispatch = dispatch;
            var thread = new Thread(Run) { IsBackground = true, Name = "EasyShut commands" };
            thread.Start();
        }
        private void Run()
        {
            while (!stopped)
            {
                try
                {
                    var security = new PipeSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream(Ipc.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
                    {
                        lock (gate) { if (stopped) return; current = pipe; }
                        pipe.WaitForConnection();
                        using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true))
                        {
                            var read = reader.ReadLineAsync();
                            if (!read.Wait(3000)) continue;
                            string line = read.Result;
                            if (line == null || line.Length > 8192) continue;
                            Reply response;
                            try
                            {
                                string[] args = line.Length == 0 ? new string[0] : Array.ConvertAll(line.Split('\t'), Ipc.Decode);
                                response = dispatch(args);
                            }
                            catch (Exception ex) { response = Reply.Error(ex.Message); }
                            writer.WriteLine((response.Ok ? "OK" : "ERR") + "\t" + Ipc.Encode(response.Message));
                            writer.Flush();
                        }
                    }
                }
                catch (Exception) { if (!stopped) Thread.Sleep(100); }
                finally { lock (gate) { current = null; } }
            }
        }
        public void Dispose()
        {
            stopped = true;
            lock (gate) { if (current != null) current.Dispose(); }
        }
    }
}
