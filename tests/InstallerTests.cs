using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EasyShut;

internal static class InstallerTests
{
    private static int passed, failed;
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void WithDirectory(Action<string> action)
    {
        string folder = Path.Combine(Path.GetTempPath(), "EasyShut-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { action(folder); }
        finally
        {
            // Test-owned root and an explicit file list. Never recursively remove unknown content.
            Installation.RemovePayload(folder);
            foreach (string name in new[] { "sentinel.txt", "old.exe" })
                if (File.Exists(Path.Combine(folder, name))) File.Delete(Path.Combine(folder, name));
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
    }
    private static void WithShortcuts(Action<string, string, string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "EasyShut-shortcuts-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "app"), programs = Path.Combine(root, "programs"), desktop = Path.Combine(root, "redirected desktop");
        foreach (string folder in new[] { app, programs, desktop }) Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(app, "EasyShut-window.exe"), new byte[] { 77, 90 });
        try { action(app, programs, desktop); }
        finally
        {
            foreach (string folder in new[] { app, programs, desktop })
            {
                foreach (string name in new[] { "EasyShut.lnk", "EasyShut-window.exe" }) File.Delete(Path.Combine(folder, name));
                Directory.Delete(folder);
            }
            Directory.Delete(root);
        }
    }
    private static string ShortcutIdentity(string path)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
        try
        {
            dynamic folder = shell.NameSpace(Path.GetDirectoryName(path));
            try
            {
                dynamic item = folder.ParseName(Path.GetFileName(path));
                try { return (string)item.ExtendedProperty("System.AppUserModel.ID"); }
                finally { Marshal.FinalReleaseComObject(item); }
            }
            finally { Marshal.FinalReleaseComObject(folder); }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
    [STAThread]
    private static int Main()
    {
        Test("embedded installer extracts a complete standalone application", delegate
        {
            WithDirectory(delegate(string folder)
            {
                Installation.InstallPayload(folder, null);
                foreach (string name in Installation.PayloadNames)
                {
                    string path = Path.Combine(folder, name);
                    Check(File.Exists(path), "Missing " + name);
                    Check(new FileInfo(path).Length > 0, "Empty " + name);
                    if (name.EndsWith(".exe")) Check(File.ReadAllBytes(path)[0] == (byte)'M', "Not an EXE: " + name);
                }
                Check(!Directory.GetDirectories(folder).Any(), "Staging directory leaked");
            });
        });
        Test("upgrade fixes filename case and removes obsolete PowerShell installers", delegate
        {
            WithDirectory(delegate(string folder)
            {
                string oldName = "EasyShut.exe".ToLowerInvariant();
                File.WriteAllText(Path.Combine(folder, oldName), "old version");
                File.WriteAllText(Path.Combine(folder, "install.ps1"), "old installer");
                File.WriteAllText(Path.Combine(folder, "uninstall.ps1"), "old uninstaller");
                File.WriteAllText(Path.Combine(folder, "sentinel.txt"), "untouched");
                Installation.InstallPayload(folder, null);
                Check(Directory.GetFiles(folder).Select(Path.GetFileName).Contains("EasyShut.exe", StringComparer.Ordinal), "Wrong filename case");
                Check(!File.Exists(Path.Combine(folder, "install.ps1")), "Old installer survived");
                Check(!File.Exists(Path.Combine(folder, "uninstall.ps1")), "Old uninstaller survived");
                Check(File.ReadAllText(Path.Combine(folder, "sentinel.txt")) == "untouched", "Unrelated file changed");
            });
        });
        Test("failed integration restores old files, letter case and scripts", delegate
        {
            WithDirectory(delegate(string folder)
            {
                string oldName = "EasyShut.exe".ToLowerInvariant();
                File.WriteAllText(Path.Combine(folder, oldName), "old version");
                File.WriteAllText(Path.Combine(folder, "install.ps1"), "old installer");
                bool failedAsExpected = false;
                try { Installation.InstallPayload(folder, delegate { throw new IOException("Simulated registration failure"); }); }
                catch (IOException) { failedAsExpected = true; }
                Check(failedAsExpected, "Failure was not propagated");
                Check(File.ReadAllText(Path.Combine(folder, oldName)) == "old version", "Previous binary not restored");
                Check(Directory.GetFiles(folder).Select(Path.GetFileName).Contains(oldName, StringComparer.Ordinal), "Old name not restored");
                Check(File.ReadAllText(Path.Combine(folder, "install.ps1")) == "old installer", "Previous installer not restored");
                Check(!File.Exists(Path.Combine(folder, "EasyShut-window.exe")), "Partially installed file survived rollback");
                Check(!Directory.GetDirectories(folder).Any(), "Staging directory leaked");
            });
        });
        Test("locked old executable aborts without deleting previous data", delegate
        {
            WithDirectory(delegate(string folder)
            {
                string exe = Path.Combine(folder, "EasyShut.exe");
                File.WriteAllText(exe, "old version");
                bool rejected = false;
                using (var locked = File.Open(exe, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { Installation.InstallPayload(folder, null); }
                    catch (IOException) { rejected = true; }
                }
                Check(rejected, "Locked file was not rejected");
                Check(File.ReadAllText(exe) == "old version", "Old file damaged");
                Check(!Directory.GetDirectories(folder).Any(), "Staging directory leaked");
            });
        });
        Test("uninstall removes only package files", delegate
        {
            WithDirectory(delegate(string folder)
            {
                Installation.InstallPayload(folder, null);
                File.WriteAllText(Path.Combine(folder, "sentinel.txt"), "keep");
                Installation.RemovePayload(folder);
                Check(Directory.GetFiles(folder).Length == 1, "Wrong number of remaining files");
                Check(File.ReadAllText(Path.Combine(folder, "sentinel.txt")) == "keep", "Unrelated file removed");
            });
        });
        Test("PATH update preserves unrelated paths and deduplicates legacy casing", delegate
        {
            string target = Installation.DefaultDirectory;
            string original = @"%USERPROFILE%\bin;C:\Tools;" + target.ToLowerInvariant() + "\\;" + target;
            string result = Installation.UpdatePath(original, target, true);
            Check(result == @"%USERPROFILE%\bin;C:\Tools;" + target, "Unexpected PATH: " + result);
            Check(Installation.UpdatePath(result, target, true) == result, "Reinstall duplicates PATH");
            Check(Installation.UpdatePath(result, target, false) == @"%USERPROFILE%\bin;C:\Tools", "Uninstall changes unrelated PATH entries");
        });
        Test("Start shortcut has a stable app identity; desktop is opt-in", delegate
        {
            WithShortcuts(delegate(string app, string programs, string desktop)
            {
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, false)) { shortcuts.Register(); shortcuts.Commit(); }
                string start = Path.Combine(programs, "EasyShut.lnk");
                Check(ShortcutRegistration.IsOwned(start, app), "Start target incorrect");
                Check(ShortcutIdentity(start) == "szymongazinski.EasyShut", "Missing app identity");
                Check(!File.Exists(Path.Combine(desktop, "EasyShut.lnk")), "Unrequested desktop shortcut");
            });
        });
        Test("desktop opt-in uses the supplied redirected desktop and matching identity", delegate
        {
            WithShortcuts(delegate(string app, string programs, string desktop)
            {
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, true)) { shortcuts.Register(); shortcuts.Commit(); }
                string link = Path.Combine(desktop, "EasyShut.lnk");
                Check(ShortcutRegistration.IsOwned(link, app), "Desktop target incorrect");
                Check(ShortcutIdentity(link) == "szymongazinski.EasyShut", "Desktop identity differs");
                byte[] previous = File.ReadAllBytes(link);
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, false)) { shortcuts.Register(); shortcuts.Commit(); }
                Check(previous.SequenceEqual(File.ReadAllBytes(link)), "Unchecked option altered existing desktop shortcut");
            });
        });
        Test("failed registration removes new shortcuts", delegate
        {
            WithShortcuts(delegate(string app, string programs, string desktop)
            {
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, true)) { shortcuts.Register(); }
                Check(!File.Exists(Path.Combine(programs, "EasyShut.lnk")), "Start shortcut survived rollback");
                Check(!File.Exists(Path.Combine(desktop, "EasyShut.lnk")), "Desktop shortcut survived rollback");
            });
        });
        Test("failed upgrade restores both existing shortcuts byte for byte", delegate
        {
            WithShortcuts(delegate(string app, string programs, string desktop)
            {
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, true)) { shortcuts.Register(); shortcuts.Commit(); }
                string start = Path.Combine(programs, "EasyShut.lnk"), desk = Path.Combine(desktop, "EasyShut.lnk");
                byte[] oldStart = File.ReadAllBytes(start), oldDesk = File.ReadAllBytes(desk);
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, true)) { shortcuts.Register(); }
                Check(oldStart.SequenceEqual(File.ReadAllBytes(start)), "Start shortcut not restored");
                Check(oldDesk.SequenceEqual(File.ReadAllBytes(desk)), "Desktop shortcut not restored");
            });
        });
        Test("unrelated shortcuts are neither overwritten nor removed", delegate
        {
            WithShortcuts(delegate(string app, string programs, string desktop)
            {
                using (var shortcuts = new ShortcutRegistration(app, programs, desktop, true)) { shortcuts.Register(); shortcuts.Commit(); }
                string start = Path.Combine(programs, "EasyShut.lnk"), desk = Path.Combine(desktop, "EasyShut.lnk");
                bool rejected = false;
                try { using (var shortcuts = new ShortcutRegistration(programs, programs, desktop, true)) { shortcuts.Register(); shortcuts.Commit(); } }
                catch (IOException) { rejected = true; }
                Check(rejected, "Different target was overwritten");
                ShortcutRegistration.RemoveOwned(desk, programs);
                Check(File.Exists(desk), "Unrelated shortcut removed");
                ShortcutRegistration.RemoveOwned(desk, app);
                ShortcutRegistration.RemoveOwned(start, app);
                Check(!File.Exists(desk) && !File.Exists(start), "Owned shortcuts survived removal");
            });
        });
        Console.WriteLine(passed + " installer tests passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
