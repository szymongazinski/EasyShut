using System;
using System.Collections.Generic;
using System.Linq;
using EasyShut;

internal static class CoreTests
{
    private static int passed, failed;
    private static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Equal<T>(T expected, T actual) { if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
    private static Command Parse(params string[] args) { return CommandLine.Parse(args); }
    private static int Main()
    {
        Test("no arguments opens GUI", delegate { Equal(CommandKind.Show, Parse().Kind); });
        Test("CLI defaults shutdown and screen off", delegate { var c = Parse("1.5"); Equal(TimeSpan.FromMinutes(90), c.Duration.Value); Equal(PowerAction.Shutdown, c.Action); Equal(false, c.ScreenOn); });
        Test("comma hours and flags", delegate { var c = Parse("1,5", "-sleep", "-screen_on"); Equal(TimeSpan.FromMinutes(90), c.Duration.Value); Equal(PowerAction.Sleep, c.Action); Equal(true, c.ScreenOn); });
        Test("never and display on", delegate { var c = Parse("-n", "-screen_on"); Equal(false, c.Duration.HasValue); Equal(true, c.ScreenOn); });
        Test("extend decimal", delegate { var c = Parse("+0,5"); Equal(CommandKind.Extend, c.Kind); Equal(TimeSpan.FromMinutes(30), c.Duration.Value); });
        Test("case insensitive flags", delegate { Equal(true, Parse("-N", "-SCREEN_ON").ScreenOn); });
        foreach (string flag in new[] { "-help", "--help", "-h", "/?" }) { string copy = flag; Test("help " + flag, delegate { Equal(CommandKind.Help, Parse(copy).Kind); }); }
        Test("status and stop", delegate { Equal(CommandKind.Status, Parse("-status").Kind); Equal(CommandKind.Stop, Parse("-stop").Kind); });
        Test("pdoc is an explicit session override", delegate { Equal((bool?)true, Parse("1", "-pdoc").ProtectDocuments); Equal((bool?)null, Parse("1").ProtectDocuments); Equal((bool?)true, Parse("-n", "-pdoc").ProtectDocuments); });
        string[][] invalid = {
            new[]{"0"}, new[]{"-1"}, new[]{"NaN"}, new[]{"Infinity"}, new[]{"1e3"}, new[]{"1.2,3"}, new[]{"876001"}, new[]{"0.00001"},
            new[]{"1", "-n"}, new[]{"1", "-shut", "-sleep"}, new[]{"-n", "-shut", "-sleep"}, new[]{"-sleep"}, new[]{"-screen_on"},
            new[]{"-sleep", "1"}, new[]{"1", "2"}, new[]{"1", "-wat"}, new[]{"-n", "-n"}, new[]{"1", "-screen_on", "-screen_on"},
            new[]{"+0"}, new[]{"+"}, new[]{"++1"}, new[]{"+1", "-screen_on"}, new[]{"-stop", "-n"}, new[]{"-status", "1"}, new[]{"-help", "1"},
            new[]{"-pdoc"}, new[]{"1", "-pdoc", "-pdoc"}, new[]{"+1", "-pdoc"}
        };
        foreach (var args in invalid) { var copy = args; Test("reject " + string.Join(" ", args), delegate { Throws(delegate { CommandLine.Parse(copy); }); }); }
        Test("never holds until stopped, no action", delegate { var f = new Fixture(); f.Session.Start(Parse("-n", "-screen_on")); f.Clock.Advance(1000000); f.Session.Tick(); Equal(0, f.Power.Actions.Count); Equal(true, f.Power.Held); f.Session.Stop(); Equal(false, f.Power.Held); Equal(false, f.Session.GetSnapshot().Active); });
        Test("screen off exactly at 5 seconds, once", delegate { var f = new Fixture(); f.Session.Start(Parse("-n")); f.Clock.Advance(4.99); f.Session.Tick(); Equal(0, f.Power.ScreenOff); f.Clock.Advance(.01); f.Session.Tick(); f.Session.Tick(); Equal(1, f.Power.ScreenOff); Equal(true, f.Power.Held); });
        Test("screen on suppresses display-off", delegate { var f = new Fixture(); f.Session.Start(Parse("-n", "-screen_on")); f.Clock.Advance(10); f.Session.Tick(); Equal(0, f.Power.ScreenOff); Equal(true, f.Power.ScreenOn); });
        Test("stop cancels action and pending screen-off", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Session.Stop(); f.Clock.Advance(9999); f.Session.Tick(); Equal(0, f.Power.ScreenOff); Equal(0, f.Power.Actions.Count); });
        Test("replacement cancels old action and screen-off", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.Advance(2); f.Session.Start(Parse("-n", "-screen_on")); f.Clock.Advance(9999); f.Session.Tick(); Equal(0, f.Power.ScreenOff); Equal(0, f.Power.Actions.Count); });
        Test("replacement restarts from now", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.Advance(1000); f.Session.Start(Parse("1")); Equal(TimeSpan.FromHours(1), f.Session.GetSnapshot().Remaining.Value); });
        Test("three-hour boundary, both warnings once", delegate { var f = new Fixture(); f.Session.Start(Parse("3", "-screen_on")); f.Clock.Advance(9899); f.Session.Tick(); Equal(0, f.Warnings.Count); f.Clock.Advance(1); f.Session.Tick(); f.Session.Tick(); Equal("15", string.Join(",", f.Warnings)); f.Clock.Advance(840); f.Session.Tick(); f.Session.Tick(); Equal("15,1", string.Join(",", f.Warnings)); });
        Test("under three hours skips 15-minute warning", delegate { var f = new Fixture(); f.Session.Start(Parse("2.99")); f.Clock.Advance(2.99 * 3600 - 900); f.Session.Tick(); Equal(0, f.Warnings.Count); f.Clock.Advance(840); f.Session.Tick(); Equal("1", string.Join(",", f.Warnings)); });
        Test("short duration warns immediately", delegate { var f = new Fixture(); f.Session.Start(Parse("0.01")); f.Session.Tick(); Equal("1", string.Join(",", f.Warnings)); });
        Test("deadline releases and executes shutdown once", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.Advance(3600); f.Session.Tick(); f.Session.Tick(); Equal(false, f.Power.Held); Equal(1, f.Power.Actions.Count); Equal(PowerAction.Shutdown, f.Power.Actions[0]); Equal(false, f.Session.GetSnapshot().Active); });
        Test("sleep executes once even after resume", delegate { var f = new Fixture(); f.Session.Start(Parse("1", "-sleep")); f.Clock.Advance(3600); f.Session.Tick(); f.Clock.Advance(3600); f.Session.Tick(); Equal(PowerAction.Sleep, f.Power.Actions.Single()); });
        Test("extension preserves action and display, adds to remaining", delegate { var f = new Fixture(); f.Session.Start(Parse("1", "-sleep", "-screen_on")); f.Clock.Advance(900); Equal(true, f.Session.Extend(TimeSpan.FromHours(1))); var s = f.Session.GetSnapshot(); Equal(TimeSpan.FromMinutes(105), s.Remaining.Value); Equal(PowerAction.Sleep, s.Action); Equal(true, s.ScreenOn); });
        Test("extension leaves never untouched", delegate { var f = new Fixture(); f.Session.Start(Parse("-n")); Equal(false, f.Session.Extend(TimeSpan.FromHours(1))); Equal(false, f.Session.GetSnapshot().Remaining.HasValue); });
        Test("extension without session fails", delegate { var f = new Fixture(); Throws(delegate { f.Session.Extend(TimeSpan.FromHours(1)); }); });
        Test("extension re-arms warnings after crossing back", delegate { var f = new Fixture(); f.Session.Start(Parse("3")); f.Clock.Advance(10740); f.Session.Tick(); f.Session.Extend(TimeSpan.FromHours(1)); f.Clock.Advance(2760); f.Session.Tick(); f.Clock.Advance(840); f.Session.Tick(); Equal("1,15,1", string.Join(",", f.Warnings)); });
        Test("extension qualifies combined three-hour session", delegate { var f = new Fixture(); f.Session.Start(Parse("2")); f.Session.Extend(TimeSpan.FromHours(1)); f.Clock.Advance(9900); f.Session.Tick(); Equal("15", string.Join(",", f.Warnings)); });
        Test("extension rejects overflow without mutation", delegate { var f = new Fixture(); f.Session.Start(Parse("876000")); Throws(delegate { f.Session.Extend(TimeSpan.FromHours(1)); }); Equal(TimeSpan.FromHours(876000), f.Session.GetSnapshot().Remaining.Value); });
        Test("clock change does not affect elapsed countdown", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.WallOffset = TimeSpan.FromHours(24); Equal(TimeSpan.FromHours(1), f.Session.GetSnapshot().Remaining.Value); });
        Test("missed ticks do not postpone action", delegate { var f = new Fixture(); f.Session.Start(Parse("3")); f.Clock.Advance(15000); f.Session.Tick(); Equal(1, f.Power.Actions.Count); Equal(0, f.Warnings.Count); });
        Test("native failure ends session without retry", delegate { var f = new Fixture(); f.Power.FailExecute = true; string error = null; f.Session.Failed += delegate(string value) { error = value; }; f.Session.Start(Parse("1")); f.Clock.Advance(3600); f.Session.Tick(); f.Session.Tick(); Equal(false, f.Session.GetSnapshot().Active); Equal(1, f.Power.Actions.Count); Equal(true, error != null); });
        Test("failed replacement keeps previous schedule", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Power.FailHold = true; Throws(delegate { f.Session.Start(Parse("-n")); }); Equal(TimeSpan.FromHours(1), f.Session.GetSnapshot().Remaining.Value); });
        Test("duration formatting over 24 hours", delegate { Equal("30:00:01", StatusText.Duration(TimeSpan.FromHours(30) + TimeSpan.FromMilliseconds(10))); });
        Test("saved protection is used by unflagged session", delegate { var f = new Fixture(); var s = AdvancedSettings.Defaults(); s.ProtectDocuments = true; f.Session.Start(Parse("1"), s); f.Clock.Advance(3600); f.Session.Tick(); Equal(true, f.Power.Protected.Single()); });
        Test("pdoc reaches the power backend", delegate { var f = new Fixture(); f.Session.Start(Parse("1", "-pdoc")); f.Clock.Advance(3600); f.Session.Tick(); Equal(true, f.Power.Protected.Single()); });
        Test("default shutdown remains forced", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.Advance(3600); f.Session.Tick(); Equal(false, f.Power.Protected.Single()); });
        Test("saving protection updates active session without restarting", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Clock.Advance(200); var s = AdvancedSettings.Defaults(); s.ProtectDocuments = true; f.Session.Configure(s); Equal(TimeSpan.FromSeconds(3400), f.Session.GetSnapshot().Remaining.Value); f.Clock.Advance(3400); f.Session.Tick(); Equal(true, f.Power.Protected.Single()); });
        Test("saved changes cannot turn off a pdoc override", delegate { var f = new Fixture(); f.Session.Start(Parse("1", "-pdoc")); f.Session.Configure(AdvancedSettings.Defaults()); Equal(true, f.Session.GetSnapshot().ProtectDocuments); });
        Test("extension preserves pdoc and replacement drops it", delegate { var f = new Fixture(); f.Session.Start(Parse("1", "-pdoc")); f.Session.Extend(TimeSpan.FromHours(1)); Equal(true, f.Session.GetSnapshot().ProtectDocuments); f.Session.Start(Parse("1")); Equal(false, f.Session.GetSnapshot().ProtectDocuments); });
        Test("empty warning list suppresses every warning", delegate { var f = new Fixture(); f.Session.Start(Parse("3"), new AdvancedSettings()); f.Clock.Advance(9900); f.Session.Tick(); f.Clock.Advance(840); f.Session.Tick(); Equal(0, f.Warnings.Count); });
        Test("custom warnings replace default thresholds", delegate { var f = new Fixture(); var s = new AdvancedSettings(); s.Warnings.Add(new WarningRule { BeforeMinutes = 7 }); s.Warnings.Add(new WarningRule { BeforeMinutes = 2 }); f.Session.Start(Parse("1"), s); f.Clock.Advance(3180); f.Session.Tick(); f.Clock.Advance(300); f.Session.Tick(); Equal("7,2", string.Join(",", f.Warnings)); });
        Test("warning minimum session duration is respected", delegate { var f = new Fixture(); var s = new AdvancedSettings(); s.Warnings.Add(new WarningRule { BeforeMinutes = 5, MinimumSessionHours = 2 }); f.Session.Start(Parse("1"), s); f.Clock.Advance(3300); f.Session.Tick(); Equal(0, f.Warnings.Count); });
        Test("saving new overdue warning shows it on next tick", delegate { var f = new Fixture(); f.Session.Start(Parse("1"), new AdvancedSettings()); f.Clock.Advance(3500); var s = new AdvancedSettings(); s.Warnings.Add(new WarningRule { BeforeMinutes = 3 }); f.Session.Configure(s); f.Session.Tick(); Equal("3", string.Join(",", f.Warnings)); });
        Test("removing warning prevents it from firing", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); f.Session.Configure(new AdvancedSettings()); f.Clock.Advance(3550); f.Session.Tick(); Equal(0, f.Warnings.Count); });
        Test("saving unrelated setting does not repeat fired warning", delegate { var f = new Fixture(); var s = AdvancedSettings.Defaults(); f.Session.Start(Parse("3"), s); f.Clock.Advance(9900); f.Session.Tick(); s.ProtectDocuments = true; f.Session.Configure(s); f.Session.Tick(); Equal("15", string.Join(",", f.Warnings)); });
        Test("invalid warnings do not replace a running session", delegate { var f = new Fixture(); f.Session.Start(Parse("1")); var s = new AdvancedSettings(); s.Warnings.Add(new WarningRule { BeforeMinutes = 0 }); Throws(delegate { f.Session.Start(Parse("2"), s); }); Equal(TimeSpan.FromHours(1), f.Session.GetSnapshot().Remaining.Value); });
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
    private sealed class FakeClock : IClock
    {
        public TimeSpan Now { get; private set; }
        public TimeSpan WallOffset;
        public DateTimeOffset UtcNow { get { return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) + Now + WallOffset; } }
        public void Advance(double seconds) { Now += TimeSpan.FromSeconds(seconds); }
    }
    private sealed class FakePower : IPower
    {
        public bool Held, ScreenOn, FailExecute, FailHold;
        public int ScreenOff;
        public readonly List<PowerAction> Actions = new List<PowerAction>();
        public readonly List<bool> Protected = new List<bool>();
        public void Hold(bool screenOn) { if (FailHold) throw new InvalidOperationException("hold failure"); Held = true; ScreenOn = screenOn; }
        public void Release() { Held = false; }
        public void TurnScreenOff() { ScreenOff++; }
        public void Execute(PowerAction action, bool protectDocuments) { if (Held) throw new Exception("power hold not released"); Actions.Add(action); Protected.Add(protectDocuments); if (FailExecute) throw new Exception("action failure"); }
        public void Dispose() { Release(); }
    }
    private sealed class Fixture
    {
        public readonly FakeClock Clock = new FakeClock();
        public readonly FakePower Power = new FakePower();
        public readonly List<int> Warnings = new List<int>();
        public readonly Session Session;
        public Fixture() { Session = new Session(Clock, Power); Session.Warning += Warnings.Add; }
    }
}
