using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EasyShut
{
    public static class Installation
    {
        public const string Version = "1.2.2";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EasyShut";
        public static readonly string[] PayloadNames = {
            "EasyShut.exe", "EasyShut-window.exe", "EasyShut-uninstall.exe",
            "EasyShut.exe.config", "EasyShut-window.exe.config", "README.md", "LICENSE"
        };
        private static readonly string[] LegacyNames = { "install.ps1", "uninstall.ps1" };
        public static string DefaultDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EasyShut"); } }
        public static string DesktopShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EasyShut.lnk"); } }

        public static void RequireClosed(string directory)
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id != Process.GetCurrentProcess().Id &&
                            (process.ProcessName.Equals("EasyShut", StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("EasyShut-window", StringComparison.OrdinalIgnoreCase)) &&
                            SameDirectory(Path.GetDirectoryName(process.MainModule.FileName), directory))
                            throw new InvalidOperationException("Zamknij EasyShut przed instalacją lub odinstalowaniem. Jeśli działa w tle, wpisz EasyShut -stop i poczekaj 11 sekund.");
                    }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException ex)
                    {
                        if (ex.Message.StartsWith("Zamknij EasyShut", StringComparison.Ordinal)) throw;
                    }
                }
            }
        }

        public static bool SameDirectory(string first, string second)
        {
            return string.Equals(Path.GetFullPath(first).TrimEnd('\\'), Path.GetFullPath(second).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        public static string UpdatePath(string current, string target, bool add)
        {
            var parts = (current ?? "").Split(';').Where(value =>
            {
                if (string.IsNullOrWhiteSpace(value)) return false;
                try { return !SameDirectory(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')), target); }
                catch (ArgumentException) { return true; }
                catch (NotSupportedException) { return true; }
            }).ToList();
            if (add) parts.Add(target);
            return string.Join(";", parts);
        }

        // Extract only a fixed list of embedded files. Back up previous versions and
        // restore them if any write or Windows integration operation fails.
        public static void InstallPayload(string directory, Action afterFiles)
        {
            directory = Path.GetFullPath(directory).TrimEnd('\\');
            RequireClosed(directory);
            var payload = new Dictionary<string, byte[]>();
            foreach (string name in PayloadNames)
            {
                using (Stream input = typeof(Installation).Assembly.GetManifestResourceStream("Payload." + name))
                {
                    if (input == null) throw new IOException("Instalator nie zawiera pliku " + name + ". Pobierz ponownie EasyShut-Setup.exe.");
                    using (var data = new MemoryStream()) { input.CopyTo(data); payload.Add(name, data.ToArray()); }
                }
            }
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, ".EasyShut-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var backups = new Dictionary<string, string>();
            var installed = new List<string>();
            bool committed = false;
            try
            {
                foreach (var item in payload) File.WriteAllBytes(Path.Combine(staging, item.Key), item.Value);
                foreach (string name in PayloadNames.Concat(LegacyNames))
                {
                    string existing = Directory.GetFiles(directory).FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        string backup = Path.Combine(staging, "backup-" + name);
                        File.Move(existing, backup);
                        backups.Add(existing, backup);
                    }
                    if (payload.ContainsKey(name))
                    {
                        string destination = Path.Combine(directory, name);
                        File.Move(Path.Combine(staging, name), destination);
                        installed.Add(destination);
                    }
                }
                if (afterFiles != null) afterFiles();
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    foreach (string path in installed) File.Delete(path);
                    foreach (var item in backups) File.Move(item.Value, item.Key);
                }
                // This newly generated directory contains only our staged files;
                // leave it intact if rollback itself failed, so backups survive.
                foreach (string path in Directory.GetFiles(staging)) File.Delete(path);
                Directory.Delete(staging);
            }
        }

        public static void InstallDefault() { InstallDefault(false); }
        public static void InstallDefault(bool addDesktopShortcut)
        {
            using (var integration = new WindowsIntegration(DefaultDirectory, addDesktopShortcut))
            {
                InstallPayload(DefaultDirectory, integration.Register);
                integration.Commit();
            }
            BroadcastEnvironment();
        }

        public static void RemovePayload(string directory)
        {
            RequireClosed(directory);
            foreach (string name in PayloadNames.Concat(LegacyNames))
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }

        public static void UninstallDefault(string directory)
        {
            directory = Path.GetFullPath(directory).TrimEnd('\\');
            if (!SameDirectory(directory, DefaultDirectory)) throw new InvalidOperationException("Nieprawidłowy katalog instalacji EasyShut.");
            RequireClosed(directory);
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EasyShut.lnk");
            RemovePayload(directory);
            using (RegistryKey environment = Registry.CurrentUser.CreateSubKey("Environment"))
            {
                string previous = (string)environment.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                RegistryValueKind kind = environment.GetValueNames().Contains("Path", StringComparer.OrdinalIgnoreCase) ? environment.GetValueKind("Path") : RegistryValueKind.ExpandString;
                environment.SetValue("Path", UpdatePath(previous, directory, false), kind);
            }
            ShortcutRegistration.RemoveOwned(shortcut, directory);
            ShortcutRegistration.RemoveOwned(DesktopShortcut, directory);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            BroadcastEnvironment();
        }

        private sealed class WindowsIntegration : IDisposable
        {
            private readonly string target;
            private readonly ShortcutRegistration shortcuts;
            private readonly string oldPath;
            private readonly RegistryValueKind pathKind;
            private readonly bool hadPath, hadKey;
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            private readonly Dictionary<string, RegistryValueKind> kinds = new Dictionary<string, RegistryValueKind>();
            private bool started, committed;
            public WindowsIntegration(string target, bool addDesktop)
            {
                this.target = target;
                shortcuts = new ShortcutRegistration(target, Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), addDesktop);
                using (RegistryKey env = Registry.CurrentUser.OpenSubKey("Environment"))
                {
                    hadPath = env != null && env.GetValueNames().Contains("Path", StringComparer.OrdinalIgnoreCase);
                    oldPath = hadPath ? (string)env.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) : "";
                    pathKind = hadPath ? env.GetValueKind("Path") : RegistryValueKind.ExpandString;
                }
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    hadKey = key != null;
                    if (key != null) foreach (string name in key.GetValueNames())
                    {
                        values.Add(name, key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                        kinds.Add(name, key.GetValueKind(name));
                    }
                }
            }
            public void Register()
            {
                started = true;
                shortcuts.Register();
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    key.SetValue("DisplayName", "EasyShut");
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", "szymongazinski");
                    key.SetValue("InstallLocation", target);
                    key.SetValue("DisplayIcon", Path.Combine(target, "EasyShut-window.exe"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(target, "EasyShut-uninstall.exe") + "\"");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey env = Registry.CurrentUser.CreateSubKey("Environment"))
                    env.SetValue("Path", UpdatePath(oldPath, target, true), pathKind);
            }
            public void Commit() { shortcuts.Commit(); committed = true; }
            public void Dispose()
            {
                if (!started || committed) return;
                shortcuts.Dispose();
                using (RegistryKey env = Registry.CurrentUser.CreateSubKey("Environment"))
                {
                    if (hadPath) env.SetValue("Path", oldPath, pathKind); else env.DeleteValue("Path", false);
                }
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                if (hadKey) using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                    foreach (var item in values) key.SetValue(item.Key, item.Value, kinds[item.Key]);
            }
        }
        private static void BroadcastEnvironment()
        {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xffff), 0x1a, UIntPtr.Zero, "Environment", 2, 3000, out result);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
    }

    internal static class Setup
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
#if UNINSTALL
                if (args.Length == 3 && args[0] == "--remove")
                {
                    // Run from a temporary copy, so the installed uninstaller can be removed.
                    int parent;
                    if (!int.TryParse(args[2], out parent)) throw new ArgumentException("Nieprawidłowe polecenie.");
                    try { using (Process p = Process.GetProcessById(parent)) if (!p.WaitForExit(10000)) throw new IOException("Odinstalowanie jest jeszcze uruchomione."); }
                    catch (ArgumentException) { }
                    Installation.UninstallDefault(args[1]);
                    MessageBox.Show("EasyShut został odinstalowany.", "EasyShut", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                string folder = AppDomain.CurrentDomain.BaseDirectory;
                if (!Installation.SameDirectory(folder, Installation.DefaultDirectory)) throw new InvalidOperationException("Uruchom odinstalowanie z katalogu zainstalowanego EasyShut lub z listy aplikacji Windows.");
                Installation.RequireClosed(folder);
                if (MessageBox.Show("Odinstalować EasyShut z tego konta Windows?", "EasyShut — odinstalowanie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                string copy = Path.Combine(Path.GetTempPath(), "EasyShut-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Application.ExecutablePath, copy);
                Process.Start(new ProcessStartInfo(copy, "--remove \"" + folder.TrimEnd('\\') + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
                return 0;
#else
                if (args.Length > 0 && args[0] == "--install-silent")
                {
                    if (args.Length > 2 || (args.Length == 2 && args[1] != "--desktop-shortcut"))
                        throw new ArgumentException("Użyj --install-silent [--desktop-shortcut].");
                    Installation.InstallDefault(args.Length == 2);
                    return 0;
                }
                if (args.Length != 0) throw new ArgumentException("Uruchom EasyShut-Setup.exe bez argumentów.");
                using (var window = new SetupWindow()) Application.Run(window);
                return 0;
#endif
            }
            catch (Exception ex)
            {
                // Silent installation remains suitable for scripted deployment and returns an error code.
                if (args.Length == 0 || args[0] != "--install-silent")
                    MessageBox.Show(ex.Message, "EasyShut — błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

#if !UNINSTALL
    internal sealed class SetupWindow : Form
    {
        public SetupWindow()
        {
            Text = "EasyShut — instalator";
            Icon = AppIcon.Value;
            Font = new Font("Segoe UI", 9);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(22), Dock = DockStyle.Fill };
            root.Controls.Add(new Label { Text = "EasyShut " + Installation.Version, Font = new Font(Font.FontFamily, 16, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
            var description = new Label { Text = "Kliknij Zainstaluj, aby dodać EasyShut do tego konta Windows.\nProgram będzie dostępny w menu Start i w terminalu.", AutoSize = true, MaximumSize = new Size(470, 0), Margin = new Padding(0, 0, 0, 15) };
            root.Controls.Add(description);
            root.Controls.Add(new Label { Text = "Katalog instalacji:", AutoSize = true });
            root.Controls.Add(new TextBox { ReadOnly = true, Text = Installation.DefaultDirectory, Width = 470, Margin = new Padding(0, 4, 0, 14) });
            var desktop = new CheckBox { Text = "Dodaj skrót na pulpicie", AutoSize = true, Checked = ShortcutRegistration.IsOwned(Installation.DesktopShortcut, Installation.DefaultDirectory), Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(desktop);
            var launch = new CheckBox { Text = "Otwórz EasyShut po instalacji", AutoSize = true, Checked = true, Margin = new Padding(0, 0, 0, 15) };
            root.Controls.Add(launch);
            var error = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(470, 0), Visible = false };
            root.Controls.Add(error);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            var install = new Button { Text = "Zainstaluj", AutoSize = true, MinimumSize = new Size(115, 34) };
            var cancel = new Button { Text = "Anuluj", AutoSize = true, MinimumSize = new Size(100, 34), DialogResult = DialogResult.Cancel };
            row.Controls.Add(install); row.Controls.Add(cancel); root.Controls.Add(row);
            Controls.Add(root);
            AcceptButton = install; CancelButton = cancel;
            bool complete = false;
            install.Click += delegate
            {
                if (complete)
                {
                    if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(Installation.DefaultDirectory, "EasyShut-window.exe")) { UseShellExecute = true });
                    Close(); return;
                }
                install.Enabled = cancel.Enabled = false;
                error.Visible = false;
                try
                {
                    Installation.InstallDefault(desktop.Checked); complete = true;
                    desktop.Enabled = false;
                    description.Text = "EasyShut jest zainstalowany. Znajdziesz go w menu Start.\nW nowym oknie terminala możesz wpisać EasyShut lub EasyShut -help.";
                    install.Text = "Zakończ"; cancel.Visible = false;
                }
                catch (Exception ex) { error.Text = ex.Message; error.Visible = true; }
                finally { install.Enabled = cancel.Enabled = true; }
            };
        }
    }
#endif
}
