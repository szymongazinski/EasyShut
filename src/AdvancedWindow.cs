using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EasyShut
{
    internal sealed class AdvancedWindow : Form
    {
        private AdvancedSettings draft;
        private readonly ListView rules = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill };
        private readonly NumericUpDown before = new NumericUpDown { Minimum = 1, Maximum = 52560000, Value = 5, Width = 110, ThousandsSeparator = true };
        private readonly NumericUpDown minimum = new NumericUpDown { Minimum = 0, Maximum = 876000, DecimalPlaces = 2, Increment = .5m, Width = 110 };
        private readonly CheckBox protect = new CheckBox { Text = "Chroń niezapisane dokumenty przy wyłączaniu komputera", AutoSize = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(560, 0) };
        private readonly Button edit = new Button { Text = "Zmień", AutoSize = true, Enabled = false };
        private readonly Button remove = new Button { Text = "Usuń", AutoSize = true, Enabled = false };

        public AdvancedWindow(AdvancedSettings settings, Action<AdvancedSettings> save)
        {
            draft = settings.Clone();
            Text = "EasyShut — zaawansowane";
            Font = new Font("Segoe UI", 9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 525); MinimumSize = new Size(590, 540);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 7) };
            root.Controls.Add(tabs, 0, 0);

            var warnings = new TabPage("Ostrzeżenia") { UseVisualStyleBackColor = true, Padding = new Padding(14) };
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(new Label { Text = "Kiedy pokazywać ostrzeżenia przed wyłączeniem lub uśpieniem?\nPusta lista oznacza brak ostrzeżeń.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            rules.Columns.Add("Czas przed akcją", 185); rules.Columns.Add("Dla sesji trwających", 280);
            body.Controls.Add(rules, 0, 1);
            var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 12, 0, 8) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.Controls.Add(Label("Pokaż ostrzeżenie:"), 0, 0); fields.Controls.Add(before, 1, 0); fields.Controls.Add(Label("minut przed akcją"), 2, 0);
            fields.Controls.Add(Label("Minimalny czas sesji:"), 0, 1); fields.Controls.Add(minimum, 1, 1); fields.Controls.Add(Label("godzin (0 = każda sesja)"), 2, 1);
            body.Controls.Add(fields, 0, 2);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            var add = new Button { Text = "Dodaj", AutoSize = true };
            var reset = new Button { Text = "Przywróć domyślne", AutoSize = true };
            actions.Controls.Add(add); actions.Controls.Add(edit); actions.Controls.Add(remove); actions.Controls.Add(reset);
            body.Controls.Add(actions, 0, 3); warnings.Controls.Add(body); tabs.TabPages.Add(warnings);

            var other = new TabPage("Inne") { UseVisualStyleBackColor = true, Padding = new Padding(18) };
            var otherBody = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            otherBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            protect.Checked = draft.ProtectDocuments; protect.Margin = new Padding(0, 2, 0, 14); otherBody.Controls.Add(protect);
            otherBody.Controls.Add(new Label { Text = "Po zaznaczeniu EasyShut nie wymusza zamknięcia aplikacji. Windows może wstrzymać wyłączenie, aby umożliwić zapisanie dokumentów. Program nie zapisuje ich automatycznie.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 18) });
            otherBody.Controls.Add(new Label { Text = "W terminalu flaga -pdoc włącza ochronę dla danej sesji. Bez flagi obowiązuje zapisane ustawienie. Sesja rozpoczęta z -pdoc pozostaje chroniona także po odznaczeniu tej opcji.", AutoSize = true, Dock = DockStyle.Fill });
            other.Controls.Add(otherBody); tabs.TabPages.Add(other);
            root.Controls.Add(error, 0, 1);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 198));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.Controls.Add(new Label { Text = "Zapisz stosuje zmiany teraz\ni zachowuje je na kolejne sesje.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText }, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            var saveButton = new Button { Text = "Zapisz", Size = new Size(90, 32) };
            var cancel = new Button { Text = "Anuluj", Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(saveButton); buttons.Controls.Add(cancel); footer.Controls.Add(buttons, 1, 0); root.Controls.Add(footer, 0, 2);
            Controls.Add(root); CancelButton = cancel; // Enter in an editor must not save accidentally.
            rules.SelectedIndexChanged += delegate
            {
                edit.Enabled = remove.Enabled = rules.SelectedItems.Count == 1;
                if (rules.SelectedItems.Count == 1)
                {
                    var rule = (WarningRule)rules.SelectedItems[0].Tag;
                    before.Value = rule.BeforeMinutes; minimum.Value = rule.MinimumSessionHours;
                }
            };
            add.Click += delegate { ChangeRule(false); };
            edit.Click += delegate { ChangeRule(true); };
            remove.Click += delegate { if (rules.SelectedItems.Count == 1) { draft.Warnings.Remove((WarningRule)rules.SelectedItems[0].Tag); RefreshRules(); } };
            reset.Click += delegate { draft.Warnings = AdvancedSettings.Defaults().Warnings; RefreshRules(); };
            saveButton.Click += delegate
            {
                try { draft.ProtectDocuments = protect.Checked; draft.Validate(); save(draft.Clone()); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex) { error.Text = ex.Message; }
            };
            RefreshRules();
        }
        private static Label Label(string text) { return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 10, 5) }; }
        private void ChangeRule(bool replacing)
        {
            var selected = replacing && rules.SelectedItems.Count == 1 ? (WarningRule)rules.SelectedItems[0].Tag : null;
            if (replacing && selected == null) return;
            if (draft.Warnings.Any(r => r != selected && r.BeforeMinutes == (int)before.Value)) { error.Text = "Ostrzeżenie o tym czasie już istnieje. Zaznacz je i kliknij Zmień."; return; }
            if (!replacing && draft.Warnings.Count >= 100) { error.Text = "Możesz ustawić maksymalnie 100 ostrzeżeń."; return; }
            if (selected != null) draft.Warnings.Remove(selected);
            draft.Warnings.Add(new WarningRule { BeforeMinutes = (int)before.Value, MinimumSessionHours = minimum.Value });
            RefreshRules();
        }
        private void RefreshRules()
        {
            rules.Items.Clear();
            foreach (var rule in draft.Warnings.OrderByDescending(r => r.BeforeMinutes))
            {
                var item = new ListViewItem(rule.BeforeMinutes.ToString("N0") + " min") { Tag = rule };
                item.SubItems.Add(rule.MinimumSessionHours == 0 ? "Każda sesja" : "Co najmniej " + rule.MinimumSessionHours.ToString("0.##") + " godz.");
                rules.Items.Add(item);
            }
            edit.Enabled = remove.Enabled = false; error.Text = "";
        }
    }
}
