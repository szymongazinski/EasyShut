using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;

namespace EasyShut
{
    public enum PowerAction { Shutdown, Sleep }
    public enum CommandKind { Show, Start, Extend, Status, Stop, Help }

    public sealed class Command
    {
        public CommandKind Kind;
        public TimeSpan? Duration;
        public PowerAction Action;
        public bool ScreenOn;
        public bool? ProtectDocuments;
    }

    public static class CommandLine
    {
        public const string Help = @"EasyShut — czasowa blokada automatycznego usypiania Windows

  EasyShut                          Otwórz okno ustawień.
  EasyShut <godziny> [flagi]         Rozpocznij odliczanie w tle.
  EasyShut -n [flagi]                Blokuj usypianie bez limitu czasu.
  EasyShut +<godziny>                Dodaj czas do aktywnego odliczania.

Flagi:
  -help             Pokaż tę pomoc.
  -n                Nigdy nie wyłączaj; nie łącz z liczbą godzin.
  -shut             Wyłącz komputer po czasie (domyślnie).
  -sleep            Uśpij komputer po czasie; wyklucza -shut.
  -screen_on        Utrzymuj ekran włączony przez całą sesję.
                    Bez tej flagi ekran wyłączy się po 5 sekundach.
  -pdoc             Chroń niezapisane dokumenty w tej sesji.
                    Bez flagi obowiązuje wybór zapisany w Zaawansowanych.
  -status           Pokaż stan aktualnej sesji.
  -stop             Anuluj sesję i zwolnij blokadę usypiania.

Przykłady:
  EasyShut 0.25
  EasyShut 1,5 -sleep -screen_on
  EasyShut -n -screen_on
  EasyShut +1

Godziny: dodatnia liczba, kropka lub przecinek dziesiętny, minimum 1 s.
Liczba godzin musi być pierwszym argumentem. +godziny występuje osobno.
Ponowne uruchomienie z czasem lub -n zastępuje bieżące ustawienia.
+godziny nie zmienia trybu Nigdy; bez sesji zgłasza błąd.
Zamknięcie głównego okna anuluje również sesję uruchomioną z terminala.
Ostrzeżenia można dodawać i usuwać w oknie Zaawansowane.
Domyślnie: 15 min przed dla sesji >= 3 h i 1 min przed zawsze.
Ochrona dokumentów pozwala aplikacjom zatrzymać wyłączenie, aby zapisać pracę.
Bez ochrony wyłączenie wymusza zamknięcie niezapisanych dokumentów.
Ustawienia zaawansowane są zapamiętywane po kliknięciu Zapisz.
Plan zasilania Windows pozostaje bez zmian. Brak autostartu i zapisu odliczania.";

        public static TimeSpan ParseHours(string text)
        {
            decimal hours;
            string normalized = (text ?? "").Trim().Replace(',', '.');
            if (normalized.Length == 0 || normalized.Any(c => !char.IsDigit(c) && c != '.') ||
                !decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out hours) || hours <= 0)
                throw new ArgumentException("Podaj dodatnią liczbę godzin, np. 0.25, 1,5 lub 6.");
            // Leave headroom for arithmetic and a readable expected finish date.
            if (hours > 876000) throw new ArgumentException("Maksymalny czas to 876000 godzin (100 lat).");
            decimal ticks = decimal.Round(hours * TimeSpan.TicksPerHour, 0, MidpointRounding.AwayFromZero);
            if (ticks < TimeSpan.TicksPerSecond) throw new ArgumentException("Minimalny czas to 1 sekunda (np. 0.001 godziny).");
            return TimeSpan.FromTicks((long)ticks);
        }

        public static Command Parse(string[] args)
        {
            var command = new Command { Kind = CommandKind.Show, Action = PowerAction.Shutdown };
            if (args.Length == 0) return command;
            string first = args[0].ToLowerInvariant();
            if (first == "-help" || first == "--help" || first == "-h" || first == "/?")
            {
                RequireAlone(args); command.Kind = CommandKind.Help; return command;
            }
            if (first == "-status" || first == "-stop")
            {
                RequireAlone(args); command.Kind = first == "-status" ? CommandKind.Status : CommandKind.Stop; return command;
            }
            if (first.StartsWith("+", StringComparison.Ordinal))
            {
                RequireAlone(args); command.Kind = CommandKind.Extend; command.Duration = ParseHours(first.Substring(1)); return command;
            }
            bool never = false, shut = false, sleep = false, screen = false, pdoc = false;
            int start = 0;
            if (!first.StartsWith("-", StringComparison.Ordinal)) { command.Duration = ParseHours(first); start = 1; }
            for (int i = start; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-n": if (never) Duplicate("-n"); never = true; break;
                    case "-shut": if (shut) Duplicate("-shut"); shut = true; break;
                    case "-sleep": if (sleep) Duplicate("-sleep"); sleep = true; break;
                    case "-screen_on": if (screen) Duplicate("-screen_on"); screen = true; break;
                    case "-pdoc": if (pdoc) Duplicate("-pdoc"); pdoc = true; break;
                    default: throw new ArgumentException("Nieznany argument: " + args[i] + ". Użyj EasyShut -help.");
                }
            }
            if (shut && sleep) throw new ArgumentException("Nie można łączyć -shut i -sleep.");
            if (never && command.Duration.HasValue) throw new ArgumentException("Nie można łączyć -n z czasem.");
            if (!never && !command.Duration.HasValue) throw new ArgumentException("Podaj czas jako pierwszy argument albo użyj -n.");
            command.Kind = CommandKind.Start;
            command.Action = sleep ? PowerAction.Sleep : PowerAction.Shutdown;
            command.ScreenOn = screen;
            command.ProtectDocuments = pdoc ? true : (bool?)null;
            return command;
        }
        private static void RequireAlone(string[] args) { if (args.Length != 1) throw new ArgumentException("To polecenie musi występować samodzielnie."); }
        private static void Duplicate(string flag) { throw new ArgumentException("Powtórzona flaga: " + flag); }
    }

    public interface IClock { TimeSpan Now { get; } DateTimeOffset UtcNow { get; } }
    public sealed class Clock : IClock
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        public TimeSpan Now { get { return watch.Elapsed; } }
        public DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }
    }
    public interface IPower : IDisposable
    {
        void Hold(bool screenOn);
        void Release();
        void TurnScreenOff();
        void Execute(PowerAction action, bool protectDocuments);
    }
    public sealed class Snapshot
    {
        public bool Active;
        public bool ScreenOn;
        public PowerAction Action;
        public TimeSpan? Remaining;
        public TimeSpan? Total;
        public DateTimeOffset? EndsAt;
        public bool ProtectDocuments;
        public int WarningCount;
    }

    // All methods run on the host's UI thread. Both the monotonic clock and native
    // execution-state request therefore have a single, explicit owner.
    public sealed class Session
    {
        private readonly IClock clock;
        private readonly IPower power;
        private bool active, screenOn;
        private bool? protectOverride;
        private AdvancedSettings settings = AdvancedSettings.Defaults();
        private readonly HashSet<int> warned = new HashSet<int>();
        private TimeSpan? deadline, screenOffAt, total;
        private PowerAction action;
        public event Action<int> Warning;
        public event Action Completed;
        public event Action<string> Failed;

        public Session(IClock clock, IPower power) { this.clock = clock; this.power = power; }

        public void Start(Command command, AdvancedSettings configuration = null)
        {
            if (command.Kind != CommandKind.Start) throw new ArgumentException("Nieprawidłowe polecenie startu.");
            if (command.Duration.HasValue && (command.Duration.Value < TimeSpan.FromSeconds(1) || command.Duration.Value.TotalHours > 876000))
                throw new ArgumentException("Czas musi wynosić od 1 sekundy do 876000 godzin.");
            AdvancedSettings next = (configuration ?? AdvancedSettings.Defaults()).Clone();
            power.Hold(command.ScreenOn);
            settings = next; protectOverride = command.ProtectDocuments;
            active = true; screenOn = command.ScreenOn; action = command.Action;
            total = command.Duration;
            deadline = total.HasValue ? clock.Now + total.Value : (TimeSpan?)null;
            screenOffAt = screenOn ? (TimeSpan?)null : clock.Now + TimeSpan.FromSeconds(5);
            warned.Clear();
        }

        public void Configure(AdvancedSettings configuration)
        {
            AdvancedSettings next = configuration.Clone();
            warned.RemoveWhere(minutes => !next.Warnings.Any(r => r.BeforeMinutes == minutes && settings.Warnings.Any(old => old.BeforeMinutes == minutes && old.MinimumSessionHours == r.MinimumSessionHours)));
            settings = next;
        }

        public bool Extend(TimeSpan amount)
        {
            if (!active) throw new InvalidOperationException("Brak aktywnej sesji. Najpierw uruchom EasyShut z czasem lub -n.");
            if (!deadline.HasValue) return false;
            if (amount < TimeSpan.FromSeconds(1) || total.Value.TotalHours + amount.TotalHours > 876000)
                throw new ArgumentException("Łączny czas nie może przekroczyć 876000 godzin; dodaj co najmniej 1 sekundę.");
            deadline += amount; total += amount;
            TimeSpan remaining = deadline.Value - clock.Now;
            warned.RemoveWhere(minutes => remaining > TimeSpan.FromMinutes(minutes));
            return true;
        }

        public Snapshot GetSnapshot()
        {
            TimeSpan? remaining = active && deadline.HasValue ? MaxZero(deadline.Value - clock.Now) : (TimeSpan?)null;
            return new Snapshot { Active = active, ScreenOn = screenOn, Action = action, Remaining = remaining,
                Total = active ? total : null, EndsAt = remaining.HasValue ? clock.UtcNow + remaining.Value : (DateTimeOffset?)null,
                ProtectDocuments = (active ? protectOverride : null) ?? settings.ProtectDocuments, WarningCount = settings.Warnings.Count };
        }

        public void Stop()
        {
            active = false; deadline = screenOffAt = total = null; warned.Clear(); protectOverride = null;
            power.Release();
        }

        public void Tick()
        {
            if (!active) return;
            TimeSpan remaining = deadline.HasValue ? deadline.Value - clock.Now : TimeSpan.MaxValue;
            if (remaining <= TimeSpan.Zero)
            {
                PowerAction dueAction = action;
                bool protectDocuments = protectOverride ?? settings.ProtectDocuments;
                Stop(); // Clear before executing: never repeat a shutdown/sleep on resume or error.
                try { power.Execute(dueAction, protectDocuments); if (Completed != null) Completed(); }
                catch (Exception ex) { if (Failed != null) Failed("Nie udało się wykonać akcji: " + ex.Message); }
                return;
            }
            if (screenOffAt.HasValue && clock.Now >= screenOffAt.Value)
            {
                screenOffAt = null;
                try { power.TurnScreenOff(); }
                catch (Exception ex) { if (Failed != null) Failed("Nie udało się wyłączyć ekranu: " + ex.Message); }
            }
            if (!deadline.HasValue) return;
            var due = settings.Warnings.Where(r => !warned.Contains(r.BeforeMinutes) &&
                total.Value >= TimeSpan.FromHours((double)r.MinimumSessionHours) && remaining <= TimeSpan.FromMinutes(r.BeforeMinutes)).ToList();
            if (due.Count > 0)
            {
                // If ticks were missed, show only the most urgent warning and consume
                // older thresholds so they cannot appear out of order on the next tick.
                foreach (var rule in due) warned.Add(rule.BeforeMinutes);
                if (Warning != null) Warning(due.Min(r => r.BeforeMinutes));
            }
        }

        private static TimeSpan MaxZero(TimeSpan time) { return time < TimeSpan.Zero ? TimeSpan.Zero : time; }
    }

    public static class StatusText
    {
        public static string Duration(TimeSpan value)
        {
            long seconds = Math.Max(0, (long)Math.Ceiling(value.TotalSeconds));
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60);
        }
        public static string Describe(Snapshot state)
        {
            if (!state.Active) return "Brak aktywnej sesji. Obowiązują ustawienia usypiania Windows.";
            string mode = state.Remaining.HasValue
                ? (state.Action == PowerAction.Shutdown ? "Wyłączenie" : "Uśpienie") + " za " + Duration(state.Remaining.Value) +
                    " (" + state.EndsAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + ")."
                : "Nigdy — automatyczne usypianie zablokowane bez limitu.";
            return mode + Environment.NewLine + (state.ScreenOn ? "Ekran pozostaje włączony." : "Ekran: wyłączenie po 5 sekundach od startu; można go obudzić myszą lub klawiaturą.") +
                Environment.NewLine + "Ochrona dokumentów: " + (state.ProtectDocuments ? "włączona." : "wyłączona.") + " Ostrzeżenia: " + state.WarningCount + ".";
        }
    }
}
