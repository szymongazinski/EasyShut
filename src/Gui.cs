using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

namespace EasyShut
{
    internal static class Gui
    {
        [STAThread]
        private static void Main(string[] args)
        {
            AppIdentity.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var instance = new InstanceLock())
            {
                if (!instance.Acquired)
                {
                    if (args.Length == 0)
                    {
                        try { Ipc.ShowExisting(); }
                        catch (Exception ex) { MessageBox.Show(ex.Message, "EasyShut", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    return;
                }
                try
                {
#if TESTING
                    IPower power = new TestPower();
                    IClock clock = new TestClock();
#else
                    IPower power = new NativePower();
                    IClock clock = new Clock();
#endif
                    using (power)
                    using (var context = new HostContext(new Session(clock, power), args.Length == 0))
                        Application.Run(context);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "EasyShut — błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }

    internal sealed class HostContext : ApplicationContext
    {
        private readonly Session session;
        private readonly Control dispatcher = new Control();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 200 };
        private readonly Stopwatch lifetime = Stopwatch.StartNew();
        private readonly PipeServer pipe;
        private MainWindow window;
        private WarningWindow warning;
        private long idleSince;
        private bool closing;
        private readonly SettingsStore settingsStore = SettingsStore.ForCurrentUser();
        private AdvancedSettings settings;
        private string settingsError;

        public HostContext(Session session, bool show)
        {
            this.session = session;
            try { settings = settingsStore.Load(); }
            catch (Exception ex) { settings = AdvancedSettings.Defaults(); settings.ProtectDocuments = true; settingsError = ex.Message; }
            session.Configure(settings);
            IntPtr handle = dispatcher.Handle;
            session.Warning += ShowWarning;
            session.Completed += delegate { CloseWarning(); idleSince = lifetime.ElapsedMilliseconds; RefreshWindow(); };
            session.Failed += delegate(string message)
            {
                CloseWarning(); ShowWindow(); window.ShowError(message); RefreshWindow();
            };
            pipe = new PipeServer(Dispatch);
            timer.Tick += delegate
            {
                session.Tick();
                RefreshWindow();
                if (warning != null && !warning.IsDisposed) warning.UpdateState(session.GetSnapshot());
                if (window == null && !session.GetSnapshot().Active && lifetime.ElapsedMilliseconds - idleSince > 10000) ExitThread();
            };
            timer.Start();
            if (show || settingsError != null) ShowWindow();
        }
        private Reply Dispatch(string[] args)
        {
            if (closing) return Reply.Error("EasyShut kończy pracę. Spróbuj ponownie.");
            // Invoke marshals every session operation, including power requests, to the UI thread.
            try { return (Reply)dispatcher.Invoke(new Func<Reply>(delegate { return Handle(CommandLine.Parse(args)); })); }
            catch (Exception ex) { return Reply.Error(ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
        }
        private Reply Handle(Command command)
        {
            if (closing) return Reply.Error("EasyShut kończy pracę. Spróbuj ponownie.");
            switch (command.Kind)
            {
                case CommandKind.Show: ShowWindow(); return Reply.Success("Otwarto okno EasyShut.");
                case CommandKind.Start:
                    settings = settingsStore.Load();
                    session.Start(command, settings); CloseWarning();
                    if (window != null) window.LoadState(session.GetSnapshot());
                    break;
                case CommandKind.Extend:
                    bool changed = session.Extend(command.Duration.Value);
                    if (changed)
                    {
                        CloseWarning();
                        if (window != null) window.LoadState(session.GetSnapshot());
                    }
                    RefreshWindow();
                    return Reply.Success((changed ? "Wydłużono odliczanie.\n" : "Tryb Nigdy — bez zmian.\n") + StatusText.Describe(session.GetSnapshot()));
                case CommandKind.Stop: StopSession(); break;
                case CommandKind.Help: return Reply.Success(CommandLine.Help);
            }
            RefreshWindow();
            return Reply.Success(StatusText.Describe(session.GetSnapshot()));
        }
        private void StopSession()
        {
            session.Stop(); CloseWarning(); idleSince = lifetime.ElapsedMilliseconds; RefreshWindow();
        }
        private void ShowWindow()
        {
            if (window == null || window.IsDisposed)
            {
                window = new MainWindow(delegate(Command command)
                {
                    try { Handle(command); } catch (Exception ex) { window.ShowError(ex.Message); }
                }, StopSession, delegate { return settings.Clone(); }, SaveSettings);
                window.FormClosed += delegate { window = null; ExitThread(); };
                window.LoadState(session.GetSnapshot());
                window.Show();
                if (settingsError != null) window.ShowError(settingsError);
            }
            if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
            window.ActivateCurrentWindow();
        }
        private void SaveSettings(AdvancedSettings next)
        {
            settingsStore.Save(next);
            settings = next.Clone(); settingsError = null;
            session.Configure(settings); CloseWarning(); RefreshWindow();
        }
        private void RefreshWindow() { if (window != null && !window.IsDisposed) window.UpdateStatus(session.GetSnapshot()); }
        private void ShowWarning(int minutes)
        {
#if TESTING
            TestPower.Log("warning:" + minutes);
#endif
            CloseWarning();
            warning = new WarningWindow(minutes, StopSession);
            warning.UpdateState(session.GetSnapshot());
            warning.Show();
            System.Media.SystemSounds.Exclamation.Play();
        }
        private void CloseWarning()
        {
            if (warning != null) { WarningWindow old = warning; warning = null; old.Close(); old.Dispose(); }
        }
        protected override void ExitThreadCore()
        {
            if (closing) return;
            closing = true; timer.Stop(); session.Stop(); CloseWarning(); pipe.Dispose();
            if (window != null) { MainWindow old = window; window = null; old.Close(); }
            base.ExitThreadCore();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); pipe.Dispose(); dispatcher.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class MainWindow : IconForm
    {
        private readonly RadioButton quarter = Choice("15 minut"), hour = Choice("1 godzina"), six = Choice("6 godzin"), never = Choice("Nigdy"), custom = Choice("Własny czas:");
        private readonly TextBox hours = new TextBox { Width = 86, Text = "1,5", AccessibleName = "Liczba godzin", Enabled = false };
        private readonly RadioButton shutdown = Choice("Wyłączenie"), sleep = Choice("Uśpienie");
        private readonly CheckBox screenOff = new CheckBox { Text = "Wyłącz ekran 5 sekund po kliknięciu Uruchom", AutoSize = true };
        private readonly GroupBox actionGroup;
        private readonly Label countdown = new Label { AutoSize = true, Font = new Font("Segoe UI", 18), Text = "Nieaktywne" };
        private readonly Label details = new Label { AutoSize = true, MaximumSize = new Size(472, 0) };
        private readonly Label error = new Label { AutoSize = true, MaximumSize = new Size(492, 0), ForeColor = Color.Firebrick, Visible = false };
        private readonly Button start = new Button { Text = "Uruchom", AutoSize = true, MinimumSize = new Size(128, 32) };
        private readonly Button stop = new Button { Text = "Anuluj sesję", AutoSize = true, MinimumSize = new Size(104, 32), Enabled = false };
        private readonly Label forceNotice;
        private readonly Func<AdvancedSettings> getSettings;
        private readonly Action<AdvancedSettings> saveSettings;
        private HelpWindow helpWindow;
        private AdvancedWindow advancedWindow;
        private Snapshot lastState = new Snapshot();

        public MainWindow(Action<Command> onStart, Action onStop, Func<AdvancedSettings> getSettings, Action<AdvancedSettings> saveSettings)
        {
            this.getSettings = getSettings; this.saveSettings = saveSettings;
            Text = "EasyShut";
#if TESTING
            Text += " — TEST (bez sterowania zasilaniem)";
#endif
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            var root = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(18), Dock = DockStyle.Fill };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 500));
            root.Controls.Add(new Label { Text = "EasyShut", AutoSize = true, Font = new Font(Font.FontFamily, 15, FontStyle.Bold), Margin = new Padding(0, 0, 0, 5) });
            root.Controls.Add(new Label { Text = "Wstrzymaj automatyczne usypianie na czas tej sesji.", AutoSize = true, Margin = new Padding(0, 0, 0, 16) });

            var times = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(10, 7, 10, 10) };
            for (int i = 0; i < 4; i++) times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            times.Controls.Add(quarter, 0, 0); times.Controls.Add(hour, 1, 0); times.Controls.Add(six, 2, 0); times.Controls.Add(never, 3, 0);
            times.Controls.Add(custom, 0, 1); times.Controls.Add(hours, 1, 1);
            var hoursHint = new Label { Text = "godzin (np. 0,5)", AutoSize = true, Anchor = AnchorStyles.Left };
            times.Controls.Add(hoursHint, 2, 1); times.SetColumnSpan(hoursHint, 2);
            root.Controls.Add(Group("Za jaki czas?", times));
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(10, 7, 10, 10) };
            actions.Controls.Add(shutdown); actions.Controls.Add(sleep);
            actionGroup = Group("Co zrobić po upływie czasu?", actions);
            root.Controls.Add(actionGroup);
            screenOff.Margin = new Padding(3, 12, 0, 3); root.Controls.Add(screenOff);
            root.Controls.Add(new Label { Text = "Bez zaznaczenia ekran pozostaje włączony przez całą sesję.", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(23, 0, 0, 14) });

            var status = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10) };
            status.Controls.Add(countdown); status.Controls.Add(details);
            root.Controls.Add(Group("Aktualna sesja", status));
            forceNotice = new Label { Text = "Wyłączenie zamyka też niezapisane dokumenty.\nOstrzeżenia: 15 min przed dla sesji ≥ 3 h i 1 min przed zawsze.", AutoSize = true, MaximumSize = new Size(492, 0), Margin = new Padding(3, 12, 0, 8) };
            root.Controls.Add(forceNotice);
            root.Controls.Add(error);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 8, 0, 6) };
            var help = new Button { Text = "Pomoc / terminal", AutoSize = true, MinimumSize = new Size(118, 32) };
            var advanced = new Button { Text = "Zaawansowane", AutoSize = true, MinimumSize = new Size(120, 32) };
            buttons.Controls.Add(start); buttons.Controls.Add(stop); buttons.Controls.Add(help); buttons.Controls.Add(advanced);
            root.Controls.Add(buttons);
            root.Controls.Add(new Label { Text = "Zamknięcie okna kończy sesję i anuluje odliczanie.", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 4, 0, 0) });
            Controls.Add(root);
            never.Checked = true; shutdown.Checked = true;
            foreach (RadioButton radio in new[] { quarter, hour, six, never, custom }) radio.CheckedChanged += delegate { UpdateOptions(); };
            shutdown.CheckedChanged += delegate { UpdateOptions(); };
            start.Click += delegate
            {
                error.Visible = false;
                try
                {
                    TimeSpan? duration = never.Checked ? (TimeSpan?)null : quarter.Checked ? TimeSpan.FromMinutes(15) : hour.Checked ? TimeSpan.FromHours(1) : six.Checked ? TimeSpan.FromHours(6) : CommandLine.ParseHours(hours.Text);
                    onStart(new Command { Kind = CommandKind.Start, Duration = duration, Action = sleep.Checked ? PowerAction.Sleep : PowerAction.Shutdown, ScreenOn = !screenOff.Checked });
                }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            stop.Click += delegate { onStop(); error.Visible = false; };
            help.Click += delegate { ShowHelp(); };
            advanced.Click += delegate { ShowAdvanced(); };
            AcceptButton = start;
            UpdateOptions();
        }
        private static RadioButton Choice(string text) { return new RadioButton { Text = text, AutoSize = true, Margin = new Padding(3, 5, 12, 6) }; }
        private static GroupBox Group(string text, Control child)
        {
            var group = new GroupBox { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(3, 8, 3, 3) };
            group.Controls.Add(child); return group;
        }
        private void UpdateOptions()
        {
            actionGroup.Visible = !never.Checked; hours.Enabled = custom.Checked;
            forceNotice.Visible = !never.Checked;
            AdvancedSettings current = getSettings();
            bool protection = lastState.Active ? lastState.ProtectDocuments : current.ProtectDocuments;
            forceNotice.Text = (sleep.Checked ? "Uśpienie zachowuje otwarte aplikacje i dokumenty." : protection ?
                "Ochrona dokumentów jest włączona; aplikacje mogą zatrzymać wyłączenie." : "Wyłączenie zamyka też niezapisane dokumenty.") +
                "\nOstrzeżenia: " + current.Warnings.Count + ". Zmień ustawienia w Zaawansowanych.";
        }
        public void LoadState(Snapshot state)
        {
            if (state.Active)
            {
                if (!state.Remaining.HasValue) never.Checked = true;
                else
                {
                    double value = state.Total.Value.TotalHours;
                    if (value == 0.25) quarter.Checked = true;
                    else if (value == 1) hour.Checked = true;
                    else if (value == 6) six.Checked = true;
                    else { custom.Checked = true; hours.Text = value.ToString("0.########", System.Globalization.CultureInfo.CurrentCulture); }
                }
                sleep.Checked = state.Action == PowerAction.Sleep;
                shutdown.Checked = !sleep.Checked;
                screenOff.Checked = !state.ScreenOn;
            }
            UpdateStatus(state);
        }
        public void UpdateStatus(Snapshot state)
        {
            lastState = state;
            countdown.Text = !state.Active ? "Nieaktywne" : state.Remaining.HasValue ? StatusText.Duration(state.Remaining.Value) : "Bez limitu";
            details.Text = !state.Active ? "Kliknij Uruchom, aby wstrzymać automatyczne usypianie."
                : (state.Remaining.HasValue ? (state.Action == PowerAction.Sleep ? "Uśpienie: " : "Wyłączenie: ") + state.EndsAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : "Automatyczne usypianie jest zablokowane.") +
                    "\n" + (state.ScreenOn ? "Ekran pozostaje włączony." : "Ekran wyłączany po 5 s od uruchomienia sesji.") +
                    "\nOchrona dokumentów: " + (state.ProtectDocuments ? "włączona." : "wyłączona.");
            stop.Enabled = state.Active; start.Text = state.Active ? "Uruchom od nowa" : "Uruchom";
            UpdateOptions();
        }
        public void ShowError(string message) { error.Text = message; error.Visible = true; }
        private void ShowHelp()
        {
            if (helpWindow != null && !helpWindow.IsDisposed) { helpWindow.Activate(); return; }
            using (helpWindow = new HelpWindow()) helpWindow.ShowDialog(this);
            helpWindow = null;
        }
        private void ShowAdvanced()
        {
            if (advancedWindow != null && !advancedWindow.IsDisposed) { advancedWindow.Activate(); return; }
            using (advancedWindow = new AdvancedWindow(getSettings(), saveSettings)) advancedWindow.ShowDialog(this);
            advancedWindow = null;
        }
        public void ActivateCurrentWindow()
        {
            Form owned = OwnedForms.LastOrDefault(f => f.Visible && !f.IsDisposed);
            Form target = owned ?? this;
            if (target.WindowState == FormWindowState.Minimized) target.WindowState = FormWindowState.Normal;
            target.BringToFront(); target.Activate();
        }
    }

    internal sealed class WarningWindow : IconForm
    {
        private readonly Label time = new Label { AutoSize = true, Font = new Font("Segoe UI", 20) };
        private readonly Label detail = new Label { AutoSize = true, MaximumSize = new Size(420, 0) };
        public WarningWindow(int minutes, Action cancel)
        {
            Text = "EasyShut — ostrzeżenie";
            Font = new Font("Segoe UI", 9);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StartPosition = FormStartPosition.CenterScreen; TopMost = true;
            var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Padding = new Padding(22) };
            root.Controls.Add(new Label { Text = "Ostrzeżenie: " + minutes + " min przed zaplanowaną akcją.", AutoSize = true });
            root.Controls.Add(time); root.Controls.Add(detail);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 15, 0, 8) };
            var cancelButton = new Button { Text = "Anuluj sesję", AutoSize = true, MinimumSize = new Size(125, 34) };
            var dismiss = new Button { Text = "Rozumiem", AutoSize = true, MinimumSize = new Size(125, 34) };
            cancelButton.Click += delegate { cancel(); };
            dismiss.Click += delegate { Close(); };
            row.Controls.Add(cancelButton); row.Controls.Add(dismiss); root.Controls.Add(row);
            root.Controls.Add(new Label { Text = "Zamknięcie tego ostrzeżenia nie anuluje odliczania.", AutoSize = true, ForeColor = SystemColors.GrayText });
            Controls.Add(root);
            AcceptButton = dismiss;
        }
        public void UpdateState(Snapshot state)
        {
            if (!state.Active || !state.Remaining.HasValue) { Close(); return; }
            time.Text = StatusText.Duration(state.Remaining.Value);
            detail.Text = state.Action != PowerAction.Shutdown ? "Komputer przejdzie w stan uśpienia." : state.ProtectDocuments ?
                "EasyShut poprosi Windows o wyłączenie komputera.\nOchrona dokumentów jest włączona — aplikacje mogą\nwstrzymać wyłączenie, aby umożliwić zapisanie pracy." :
                "Komputer zostanie wyłączony. Otwarte aplikacje zostaną\nzamknięte, także jeśli mają niezapisane dokumenty.";
        }
    }
}
