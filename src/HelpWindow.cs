using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyShut
{
    internal sealed class HelpWindow : IconForm
    {
        private readonly Font headingFont = new Font("Segoe UI", 11, FontStyle.Bold);
        private readonly Font commandFont = new Font("Consolas", 10);
        private readonly Label feedback = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left };
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 7) };

        public HelpWindow()
        {
            Text = "EasyShut — pomoc";
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, 650);
            MinimumSize = new Size(660, 520);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18, 14, 18, 12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { Text = "Pomoc EasyShut", AutoSize = true, Font = new Font(Font.FontFamily, 16, FontStyle.Bold), Margin = new Padding(0, 0, 0, 16) }, 0, 0);
            root.Controls.Add(tabs, 0, 1);

            TableLayoutPanel start = Page("Szybki start");
            Section(start, "Obsługa okna", "Samo otwarcie EasyShut nie uruchamia blokady. Najpierw wybierz ustawienia i kliknij Uruchom.");
            Section(start, "1. Wybierz czas", "15 minut, 1 godzina, 6 godzin, Nigdy lub własna liczba godzin, np. 1,5. Przy odliczaniu wybierz wyłączenie albo uśpienie.");
            Section(start, "2. Zdecyduj o ekranie", "Aby ekran był cały czas włączony, pozostaw opcję Wyłącz ekran niezaznaczoną. Po jej zaznaczeniu ekran zgaśnie 5 sekund od uruchomienia sesji.");
            Section(start, "3. Kliknij Uruchom", "Status zmieni się na licznik czasu lub Bez limitu. Od tej chwili EasyShut blokuje automatyczne usypianie Windows.");
            Section(start, "Jak zakończyć?", "Kliknij Anuluj sesję albo zamknij główne okno. Zminimalizowanie okna pozostawia sesję aktywną.");
            Section(start, "Ustawienia zaawansowane", "Przycisk Zaawansowane obok pomocy otwiera ustawienia ostrzeżeń i ochrony dokumentów. Kliknij Zapisz, aby zastosować je teraz i zachować na kolejne uruchomienia.");
            Note(start, "W terminalu: EasyShut otwiera okno. Polecenia z czasem lub -n od razu uruchamiają sesję w tle.");

            TableLayoutPanel flags = Page("Flagi terminala");
            Paragraph(flags, "Godziny podaj jako pierwszy argument. Flagi można łączyć zgodnie z poniższymi zasadami.");
            var table = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 8, 0, 10) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Flag(table, "godziny", "Czas do akcji, np. 0.25 lub 1,5. Domyślną akcją jest wyłączenie.");
            Flag(table, "-n", "Nigdy nie wyłączaj; blokuj usypianie bez limitu. Nie łącz z liczbą godzin.");
            Flag(table, "-shut", "Wyłącz komputer po czasie. Nie łącz z -sleep.");
            Flag(table, "-sleep", "Uśpij komputer po czasie. Nie łącz z -shut.");
            Flag(table, "-screen_on", "Utrzymuj ekran włączony przez całą sesję. Bez tej flagi ekran zgaśnie po 5 sekundach.");
            Flag(table, "-pdoc", "Chroń niezapisane dokumenty w tej sesji. Bez flagi obowiązuje ustawienie zapisane w Zaawansowanych.");
            Flag(table, "+godziny", "Dodaj czas, np. +1 lub +0,5. Występuje samodzielnie. W trybie Nigdy nic nie zmienia.");
            Flag(table, "-status", "Wyświetl stan, pozostały czas i planowany termin akcji.");
            Flag(table, "-stop", "Anuluj sesję i zwolnij blokadę usypiania.");
            Flag(table, "-help", "Wyświetl pomoc w terminalu. Działają też --help, -h i /?.");
            flags.Controls.Add(table);
            Note(flags, "Godziny mogą mieć ułamek z kropką lub przecinkiem. Zakres: od 1 sekundy do 876000 godzin. Błędne polecenie nie zmienia aktywnej sesji.");

            TableLayoutPanel examples = Page("Przykłady");
            Paragraph(examples, "Skopiuj wybrane polecenie i wklej je do terminala. Przycisk Kopiuj nie uruchamia polecenia.");
            Example(examples, "Bez limitu, z włączonym ekranem", "EasyShut -n -screen_on");
            Example(examples, "Wyłączenie za 15 minut; ekran pozostaje włączony", "EasyShut 0.25 -screen_on");
            Example(examples, "Wyłączenie za godzinę z ochroną niezapisanych dokumentów", "EasyShut 1 -pdoc -screen_on");
            Example(examples, "Uśpienie za 90 minut; ekran zgaśnie po 5 sekundach", "EasyShut 1,5 -sleep");
            Example(examples, "Dodanie godziny do bieżącego odliczania", "EasyShut +1");
            Example(examples, "Sprawdzenie aktywnej sesji", "EasyShut -status");
            Example(examples, "Anulowanie sesji", "EasyShut -stop");

            TableLayoutPanel rules = Page("Zasady działania");
            Section(rules, "Zmiana czasu", "Nowe polecenie z czasem lub -n zastępuje bieżącą sesję. Polecenie +godziny wydłuża pozostały czas, zachowując akcję i ustawienie ekranu.");
            Section(rules, "Ostrzeżenia", "Listę ustawiasz w Zaawansowane → Ostrzeżenia. Domyślnie: 15 minut przed akcją dla sesji od 3 godzin i minutę przed dla każdej sesji. Można dodać własne czasy i usunąć wszystkie ostrzeżenia. Jeśli próg już minął, pojawi się najpilniejsze ostrzeżenie.");
            Section(rules, "Zamykanie okien i terminala", "Zamknięcie głównego okna anuluje sesję. Zamknięcie okna ostrzeżenia albo terminala nie anuluje odliczania. Możesz je zatrzymać przyciskiem Anuluj sesję lub poleceniem -stop.");
            Section(rules, "Wyłączenie a dokumenty", "Opcja ochrony w Zaawansowane → Inne lub flaga -pdoc pozwala aplikacjom zatrzymać wyłączenie, aby zapisać pracę. Bez ochrony EasyShut wymusza zamknięcie aplikacji i niezapisane zmiany mogą zostać utracone. Uśpienie zachowuje otwarte aplikacje.");
            Section(rules, "Jedna instancja i zapis ustawień", "Kolejne uruchomienie przywołuje istniejące okno, także gdy program uruchomiono z innego folderu. Ustawienia zaawansowane są zapisywane dla konta Windows; odliczanie nadal jest zapominane po zamknięciu programu.");
            Section(rules, "Ustawienia Windows", "EasyShut nie zmienia planu zasilania. Po zakończeniu sesji obowiązują dotychczasowe ustawienia. Program nie uruchamia się sam po restarcie i nie blokuje ręcznego uśpienia.");
            Section(rules, "Obudzenie ekranu", "Ekran jest gaszony jednorazowo, po 5 sekundach od startu sesji. Możesz go potem obudzić myszą lub klawiaturą. Blokada usypiania i odliczanie nadal działają.");

            var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            feedback.Text = "Wybierz zakładkę, aby przejść do instrukcji.";
            footer.Controls.Add(feedback, 0, 0);
            var close = new Button { Text = "Zamknij", AutoSize = true, MinimumSize = new Size(100, 34), DialogResult = DialogResult.Cancel };
            footer.Controls.Add(close, 1, 0);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);
            CancelButton = close;
            tabs.SelectedIndexChanged += delegate { feedback.Text = "Wybierz zakładkę, aby przejść do instrukcji."; };
        }

        private TableLayoutPanel Page(string title)
        {
            var page = new TabPage(title) { UseVisualStyleBackColor = true, Padding = new Padding(2) };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SystemColors.Window };
            var body = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(18, 14, 18, 12), BackColor = SystemColors.Window };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(body); page.Controls.Add(scroll); tabs.TabPages.Add(page);
            return body;
        }
        private void Section(TableLayoutPanel parent, string title, string text)
        {
            parent.Controls.Add(new Label { Text = title, Font = headingFont, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 5) });
            Paragraph(parent, text);
        }
        private static void Paragraph(TableLayoutPanel parent, string text)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16), UseMnemonic = false });
        }
        private static void Note(TableLayoutPanel parent, string text)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = SystemColors.Control, Margin = new Padding(0, 3, 0, 3), UseMnemonic = false });
        }
        private void Flag(TableLayoutPanel table, string flag, string text)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = flag, Font = commandFont, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 14) }, 0, row);
            table.Controls.Add(new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), UseMnemonic = false }, 1, row);
        }
        private void Example(TableLayoutPanel parent, string description, string command)
        {
            parent.Controls.Add(new Label { Text = description, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 5) });
            var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 14) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            row.Controls.Add(new TextBox { Text = command, ReadOnly = true, Font = commandFont, Dock = DockStyle.Fill, AccessibleName = description, BackColor = SystemColors.Window, Margin = new Padding(0, 3, 10, 0) }, 0, 0);
            var copy = new Button { Text = "Kopiuj", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0), AccessibleName = "Kopiuj: " + command };
            copy.Click += delegate
            {
                try { Clipboard.SetText(command); feedback.Text = "Skopiowano polecenie do schowka."; }
                catch (ExternalException) { feedback.Text = "Schowek jest zajęty. Zaznacz polecenie i naciśnij Ctrl+C."; }
            };
            row.Controls.Add(copy, 1, 0);
            parent.Controls.Add(row);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { headingFont.Dispose(); commandFont.Dispose(); }
        }
    }
}
