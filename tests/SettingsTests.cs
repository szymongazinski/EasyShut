using System;
using System.IO;
using System.Linq;
using EasyShut;

internal static class SettingsTests
{
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action<SettingsStore, string> action)
    {
        string folder = Path.Combine(Path.GetTempPath(), "EasyShut-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "settings.xml");
        try { action(new SettingsStore(path), path); passed++; Console.WriteLine("PASS " + name); }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(folder); }
    }
    private static int Main()
    {
        Test("missing file gives historical defaults without writing anything", delegate(SettingsStore store, string path)
        {
            var s = store.Load(); Check(!s.ProtectDocuments && s.Warnings.Count == 2, "Wrong defaults");
            Check(s.Warnings.Single(r => r.BeforeMinutes == 15).MinimumSessionHours == 3, "Lost three-hour condition"); Check(!File.Exists(path), "Unexpected persistent write");
        });
        Test("settings survive a new store instance", delegate(SettingsStore store, string path)
        {
            var s = new AdvancedSettings { ProtectDocuments = true }; s.Warnings.Add(new WarningRule { BeforeMinutes = 8, MinimumSessionHours = .5m });
            store.Save(s); var restored = new SettingsStore(path).Load();
            Check(restored.ProtectDocuments && restored.Warnings.Single().BeforeMinutes == 8 && restored.Warnings.Single().MinimumSessionHours == .5m, "Roundtrip failed");
        });
        Test("all warnings can be removed persistently", delegate(SettingsStore store, string path) { store.Save(new AdvancedSettings()); Check(new SettingsStore(path).Load().Warnings.Count == 0, "Defaults unexpectedly restored"); });
        Test("editing a draft does not mutate loaded settings", delegate(SettingsStore store, string path)
        {
            var s = AdvancedSettings.Defaults(); store.Save(s); var draft = s.Clone(); draft.Warnings[0].BeforeMinutes = 22; draft.ProtectDocuments = true;
            Check(s.Warnings[0].BeforeMinutes == 15 && !store.Load().ProtectDocuments, "Draft was persisted without save");
        });
        Test("duplicate warning save preserves previous settings", delegate(SettingsStore store, string path)
        {
            store.Save(AdvancedSettings.Defaults()); byte[] original = File.ReadAllBytes(path); var s = store.Load(); s.Warnings.Add(new WarningRule { BeforeMinutes = 1 });
            bool rejected = false; try { store.Save(s); } catch (ArgumentException) { rejected = true; }
            Check(rejected && original.SequenceEqual(File.ReadAllBytes(path)), "Invalid save changed old data");
        });
        Test("malformed settings fail rather than disabling saved protection", delegate(SettingsStore store, string path)
        {
            File.WriteAllText(path, "<broken"); bool rejected = false; try { store.Load(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Bad configuration silently accepted");
            store.Save(new AdvancedSettings { ProtectDocuments = true }); Check(store.Load().ProtectDocuments, "Repair by Save failed");
        });
        Test("external XML entities are rejected", delegate(SettingsStore store, string path)
        {
            File.WriteAllText(path, "<!DOCTYPE AdvancedSettings [<!ENTITY x SYSTEM 'file:///not-read'>]><AdvancedSettings>&x;</AdvancedSettings>");
            bool rejected = false; try { store.Load(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "DTD accepted");
        });
        Test("failed atomic replace leaves previous settings intact", delegate(SettingsStore store, string path)
        {
            store.Save(AdvancedSettings.Defaults()); byte[] original = File.ReadAllBytes(path); bool failed = false;
            using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            { try { store.Save(new AdvancedSettings { ProtectDocuments = true }); } catch (IOException) { failed = true; } }
            Check(failed && original.SequenceEqual(File.ReadAllBytes(path)), "Old settings lost"); Check(Directory.GetFiles(Path.GetDirectoryName(path)).Length == 1, "Temporary file leaked");
        });
        Console.WriteLine(passed + " settings tests passed"); return 0;
    }
}
