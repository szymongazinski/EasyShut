using System;
using System.IO;
using System.Linq;
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
        Console.WriteLine(passed + " installer tests passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
