using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EasyShut
{
    internal static class Cli
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                Command command = CommandLine.Parse(args);
                if (command.Kind == CommandKind.Help) { Console.WriteLine(CommandLine.Help); return 0; }
                if (command.Kind == CommandKind.Show) HideOwnConsole();
                Reply reply;
                // Serialize startup and commands from concurrent terminals in this logon session.
                using (var startup = new Mutex(false, @"Local\" + Ipc.Name + ".client"))
                {
                    bool owned;
                    try { owned = startup.WaitOne(12000); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) throw new IOException("easyshut obsługuje inne polecenie. Spróbuj ponownie.");
                    try
                    {
                        if (!Ipc.TrySend(args, out reply, 300))
                        {
                            if (command.Kind == CommandKind.Status || command.Kind == CommandKind.Stop)
                                reply = Reply.Success("Brak aktywnej sesji. Obowiązują ustawienia usypiania Windows.");
                            else if (command.Kind == CommandKind.Extend)
                                reply = Reply.Error("Brak aktywnej sesji. Najpierw uruchom easyshut z czasem lub -n.");
                            else
                            {
                                string host = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "easyshut-window.exe");
                                if (!File.Exists(host)) throw new FileNotFoundException("Brak easyshut-window.exe. Wypakuj cały pakiet easyshut.");
                                // ShellExecute does not inherit the CLI's redirected pipe handles.
                                // A WinExe host also stays independent when its terminal closes.
                                using (var child = Process.Start(new ProcessStartInfo(host, "--background")
                                {
                                    UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden,
                                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                                })) { }
                                var wait = Stopwatch.StartNew();
                                while (!Ipc.TrySend(args, out reply, 300))
                                {
                                    if (wait.ElapsedMilliseconds > 10000) throw new IOException("Nie udało się uruchomić easyshut.");
                                    Thread.Sleep(50);
                                }
                            }
                        }
                    }
                    finally { startup.ReleaseMutex(); }
                }
                if (command.Kind != CommandKind.Show || !reply.Ok)
                {
                    if (reply.Ok) Console.WriteLine(reply.Message); else Console.Error.WriteLine(reply.Message);
                }
                return reply.Ok ? 0 : 1;
            }
            catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
            catch (Exception ex) { Console.Error.WriteLine("easyshut: " + ex.Message); return 1; }
        }
        private static void HideOwnConsole()
        {
            uint[] ids = new uint[2];
            if (GetConsoleProcessList(ids, 2) == 1) ShowWindow(GetConsoleWindow(), 0);
        }
        [DllImport("kernel32.dll")] private static extern uint GetConsoleProcessList(uint[] ids, uint count);
        [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
    }
}
