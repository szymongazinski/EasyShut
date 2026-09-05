using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace EasyShut
{
    public sealed class WarningRule
    {
        public int BeforeMinutes { get; set; }
        public decimal MinimumSessionHours { get; set; }
        public WarningRule Clone() { return new WarningRule { BeforeMinutes = BeforeMinutes, MinimumSessionHours = MinimumSessionHours }; }
    }

    public sealed class AdvancedSettings
    {
        public int Version { get; set; }
        public bool ProtectDocuments { get; set; }
        public List<WarningRule> Warnings { get; set; }
        public AdvancedSettings() { Version = 1; Warnings = new List<WarningRule>(); }
        public static AdvancedSettings Defaults()
        {
            return new AdvancedSettings { Warnings = new List<WarningRule> {
                new WarningRule { BeforeMinutes = 15, MinimumSessionHours = 3 },
                new WarningRule { BeforeMinutes = 1, MinimumSessionHours = 0 }
            } };
        }
        public AdvancedSettings Clone()
        {
            Validate();
            return new AdvancedSettings { Version = Version, ProtectDocuments = ProtectDocuments, Warnings = Warnings.Select(r => r.Clone()).ToList() };
        }
        public void Validate()
        {
            if (Version != 1) throw new ArgumentException("Nieobsługiwana wersja ustawień EasyShut.");
            if (Warnings == null || Warnings.Count > 100) throw new ArgumentException("Możesz ustawić od 0 do 100 ostrzeżeń.");
            if (Warnings.Any(r => r == null || r.BeforeMinutes < 1 || r.BeforeMinutes > 52560000 || r.MinimumSessionHours < 0 || r.MinimumSessionHours > 876000))
                throw new ArgumentException("Ostrzeżenie musi wyprzedzać akcję o co najmniej minutę; maksymalny zakres to 100 lat.");
            if (Warnings.Select(r => r.BeforeMinutes).Distinct().Count() != Warnings.Count)
                throw new ArgumentException("Dwa ostrzeżenia nie mogą mieć tego samego czasu przed akcją.");
        }
    }

    public sealed class SettingsStore
    {
        private readonly string path;
        public SettingsStore(string path) { this.path = path; }
        public static SettingsStore ForCurrentUser()
        {
#if TESTING
            string testPath = Environment.GetEnvironmentVariable("EASYSHUT_TEST_SETTINGS");
            return new SettingsStore(string.IsNullOrEmpty(testPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-settings.xml") : testPath);
#else
            return new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyShut", "settings.xml"));
#endif
        }
        public AdvancedSettings Load()
        {
            try
            {
                var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 };
                using (var file = File.OpenRead(path))
                using (var reader = XmlReader.Create(file, options))
                {
                    var settings = (AdvancedSettings)new XmlSerializer(typeof(AdvancedSettings)).Deserialize(reader);
                    if (settings == null) throw new InvalidDataException("Pusty plik ustawień.");
                    settings.Validate();
                    return settings;
                }
            }
            catch (FileNotFoundException) { return AdvancedSettings.Defaults(); }
            catch (DirectoryNotFoundException) { return AdvancedSettings.Defaults(); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is XmlException)) throw;
                throw new InvalidDataException("Nie można odczytać ustawień EasyShut. Otwórz Zaawansowane, sprawdź ustawienia i kliknij Zapisz.", ex);
            }
        }
        public void Save(AdvancedSettings settings)
        {
            AdvancedSettings copy = settings.Clone();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(AdvancedSettings)).Serialize(file, copy);
                    file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
