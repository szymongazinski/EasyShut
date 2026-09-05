# EasyShut

Mała aplikacja dla Windows 10/11, która na czas bieżącej sesji blokuje automatyczne usypianie. Może też wyłączyć lub uśpić komputer po wybranym czasie. Ma klasyczne okno Windows i obsługę z terminala.

**[Pobierz gotowy program](https://github.com/szymongazinski/EasyShut/releases/latest)** · [Licencja MIT](LICENSE)

Od wersji **1.2.1** program, okna i instalator używają wybranej ikony ES (D5 z dłuższą i grubszą kreską zasilania).

## Instalacja

1. **[Pobierz EasyShut-Setup.exe](https://github.com/szymongazinski/EasyShut/releases/latest/download/EasyShut-Setup.exe).**
2. Otwórz pobrany plik **dwuklikiem** i kliknij **Zainstaluj**.
3. Kliknij **Zakończ**. Domyślnie otworzy się okno EasyShut.

Nie trzeba otwierać terminala ani uruchamiać plików `.ps1`. Instalator zawiera cały program i działa bez pobierania dodatkowych plików. Przy aktualizacji najpierw zamknij EasyShut.

Instalacja nie wymaga administratora. Pliki trafiają do `%LOCALAPPDATA%\Programs\EasyShut`. Instalator dodaje skrót **EasyShut** do menu Start, polecenie `EasyShut` do PATH bieżącego użytkownika i pozycję EasyShut na liście zainstalowanych aplikacji Windows. Aby używać polecenia, otwórz **nowe okno terminala**. Wielkość liter w poleceniach Windows nie ma znaczenia, więc dotychczasowy zapis małymi literami nadal działa.

Można też korzystać bez instalacji: pobierz `EasyShut-1.2.1-windows.zip` i wypakuj cały pakiet. `EasyShut-window.exe` otwiera okno, a `EasyShut.exe` obsługuje terminal. Pliki należy trzymać w jednym katalogu. Program korzysta z .NET Framework 4.8 dostępnego w aktualnych instalacjach Windows 10/11. Nie zawiera reklam ani telemetrii. Pliki EXE nie są podpisane certyfikatem wydawcy.

## Okno

- Czas: **15 minut**, **1 godzina**, **6 godzin**, **Nigdy** lub własna liczba godzin z ułamkiem (`1,5` albo `1.5`).
- Dla odliczania: **Wyłączenie** (domyślnie) albo **Uśpienie**. Przy **Nigdy** wybór akcji jest schowany.
- **Wyłącz ekran 5 sekund po kliknięciu Uruchom**. Bez zaznaczenia ekran pozostaje włączony przez całą sesję.
- Domyślne ustawienia: **Nigdy**, ekran włączony. Samo otwarcie okna nie uruchamia blokady; kliknij **Uruchom**.
- **Anuluj sesję** zatrzymuje odliczanie i zwalnia blokadę usypiania. Okno pozostaje otwarte.
- **Zamknięcie głównego okna kończy cały program i zapomina sesję**, także rozpoczętą wcześniej w terminalu. Zminimalizowanie okna pozostawia sesję aktywną.
- Ponowne **Uruchom** zaczyna sesję od nowa z wybranymi ustawieniami.
- **Pomoc / terminal** otwiera czytelną instrukcję z zakładkami: szybki start, flagi, przykłady do skopiowania i zasady działania. Okno można powiększać; długie opisy zawijają się automatycznie.
- **Zaawansowane**, obok pomocy, otwiera osobne okno ustawień ostrzeżeń i ochrony dokumentów.
- Kolejne uruchomienie przywołuje istniejące okno, również gdy otwarta jest pomoc lub ustawienia zaawansowane. W obrębie jednego konta i sesji logowania działa tylko jedna instancja, także przy uruchamianiu kopii z różnych folderów.

## Ustawienia zaawansowane

W zakładce **Ostrzeżenia** można dodawać, zmieniać i usuwać ostrzeżenia. Podaj liczbę minut przed wyłączeniem lub uśpieniem. Opcjonalny **Minimalny czas sesji** ogranicza ostrzeżenie do dłuższych sesji; `0` oznacza każdą sesję. Usunięcie wszystkich pozycji wyłącza ostrzeżenia. **Przywróć domyślne** odtwarza wyłącznie listę ostrzeżeń.

W zakładce **Inne** opcja **Chroń niezapisane dokumenty przy wyłączaniu komputera** wyłącza wymuszanie zamknięcia aplikacji. Windows może wtedy wstrzymać wyłączenie, aby umożliwić zapisanie pracy. EasyShut nie zapisuje dokumentów automatycznie. Domyślnie ochrona jest wyłączona, zgodnie z dotychczasowym działaniem programu.

**Zapisz** stosuje oba ustawienia do bieżącej sesji bez zerowania odliczania i zachowuje je na przyszłość. **Anuluj** lub zamknięcie okna odrzuca niezapisane zmiany. Ustawienia są przechowywane dla bieżącego użytkownika w `%LOCALAPPDATA%\EasyShut\settings.xml`, wspólnie dla instalacji i wersji przenośnej. Samo odliczanie nie jest zapisywane ani wznawiane po ponownym uruchomieniu.

Flaga **`-pdoc`** włącza ochronę wyłącznie dla uruchamianej sesji; nie zmienia pliku ustawień. Bez flagi obowiązuje zapisany wybór. Sesja uruchomiona z `-pdoc` pozostaje chroniona także po zapisaniu odznaczonej opcji w oknie zaawansowanym. Kolejne uruchomienie sesji bez tej flagi ponownie korzysta z zapisanego wyboru.

## Terminal

```text
EasyShut                              Otwórz okno (lub przywołaj już otwarte).
EasyShut 0.25                         Wyłącz za 15 minut; zgaś ekran po 5 s.
EasyShut 1.5 -sleep                    Uśpij za 90 minut; zgaś ekran po 5 s.
EasyShut 1,5 -sleep -screen_on          Uśpij za 90 minut; utrzymuj ekran włączony.
EasyShut 1 -pdoc -screen_on            Wyłącz za godzinę z ochroną dokumentów.
EasyShut -n                            Nigdy nie wyłączaj; zgaś ekran po 5 s.
EasyShut -n -screen_on                 Blokuj usypianie i utrzymuj ekran włączony.
EasyShut +1                            Dodaj godzinę do bieżącego odliczania.
EasyShut +0,5                          Dodaj pół godziny.
EasyShut -status                       Sprawdź stan i planowaną godzinę akcji.
EasyShut -stop                         Anuluj sesję.
EasyShut -help                         Wyświetl wszystkie flagi.
```

| Argument | Znaczenie |
| --- | --- |
| `godziny` | Dodatnia liczba godzin jako **pierwszy** argument. Ułamki z kropką lub przecinkiem. |
| `-n` | Sesja bez limitu; nie można łączyć z liczbą godzin. |
| `-shut` | Wyłączenie po czasie, domyślny wybór. |
| `-sleep` | Uśpienie po czasie; nie można łączyć z `-shut`. |
| `-screen_on` | Ekran włączony przez całą sesję. Bez flagi: zgaś po 5 sekundach od startu sesji. |
| `-pdoc` | Chroń niezapisane dokumenty w tej sesji. Bez flagi: użyj zapisanego ustawienia. |
| `+godziny` | Dodaj czas do pozostałego odliczania; samodzielne polecenie, tylko w terminalu. |
| `-status` | Stan sesji, pozostały czas, przewidywany termin, zachowanie ekranu, ochrona dokumentów i liczba ustawionych ostrzeżeń. |
| `-stop` | Anulowanie i zwolnienie blokady usypiania. |
| `-help` | Pomoc. Działają też `--help`, `-h` i `/?`. |

Polecenia z argumentami działają w tle i oddają terminal. Zamknięcie terminala nie anuluje sesji. Aby ją zakończyć, użyj `-stop` lub otwórz główne okno przez `EasyShut` i zamknij je. Nie ma ikony w zasobniku ani autostartu.

Kolejne polecenie z czasem lub `-n` **zastępuje** ustawienia i zaczyna od nowa. `+godziny` dodaje czas do aktualnego terminu, zachowując akcję i zachowanie ekranu; w trybie **Nigdy** nic nie zmienia. Bez aktywnej sesji zgłasza błąd. Błędne argumenty nie zmieniają bieżącej sesji.

Minimalny czas wynosi 1 sekundę; maksymalny 876000 godzin (100 lat), także po dodawaniu czasu. Kody zakończenia CLI: `0` sukces, `1` błąd działania, `2` błędne argumenty.

## Ostrzeżenia i koniec odliczania

- Domyślnie: ostrzeżenie **15 minut przed akcją** dla łącznego zaplanowanego czasu **co najmniej 3 godziny** oraz **1 minutę przed akcją** dla każdej sesji. Listę i warunki można zmienić w Zaawansowanych.
- Jeśli przy rozpoczęciu lub zmianie ustawień pozostały czas jest już krótszy od progu, ostrzeżenie pojawia się od razu. Gdy jednocześnie przekroczono kilka progów, pokazywane jest tylko najpilniejsze ostrzeżenie. Tryb **Nigdy** nie pokazuje ostrzeżeń.
- Ostrzeżenia mają aktualny licznik i przycisk **Anuluj sesję**. **Rozumiem** i krzyżyk w oknie ostrzeżenia jedynie chowają ostrzeżenie — odliczanie trwa dalej.
- Dodanie czasu powyżej danego progu ponownie uzbraja odpowiadające mu ostrzeżenie. Minimalny czas sesji uwzględnia czas dodany przez `+godziny`.
- **Bez ochrony dokumentów wyłączenie jest wymuszone: aplikacje z niezapisanymi dokumentami także zostają zamknięte, co oznacza utratę niezapisanych zmian.** Włącz ochronę w Zaawansowanych lub dodaj `-pdoc`, aby aplikacje mogły wstrzymać wyłączenie. Uśpienie zachowuje otwarte aplikacje.
- Po wykonaniu akcji sesja jest anulowana. Program nie powtórzy uśpienia po wznowieniu komputera.

## Zachowanie Windows

Program używa czasowego żądania [`SetThreadExecutionState`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate). Nie modyfikuje planu zasilania, czasu automatycznego usypiania ani rejestru z ustawieniami zasilania. Po zakończeniu sesji lub procesu ponownie obowiązują dotychczasowe ustawienia Windows. Bezczynny proces w tle kończy się po około 10 sekundach; blokada jest zwalniana od razu po `-stop`.

Wyłączenie ekranu jest jednorazowe, po 5 sekundach od startu sesji. Mysz lub klawiatura może go później obudzić; odliczanie i blokada usypiania działają dalej. Powiadomienia nie wymuszają obudzenia zgaszonego ekranu, ale emitują systemowy dźwięk ostrzeżenia, jeśli dźwięki są włączone.

Ręczne wyłączenie, wylogowanie i restart kończą proces oraz sesję. Program nie blokuje ręcznego uśpienia ani działań wymuszanych przez Windows (np. krytycznej baterii). Jeśli ręcznie uśpisz komputer przed terminem i później wznowisz, czas nadal jest liczony; miniony termin zostanie obsłużony po wznowieniu. Zmiana zegara systemowego nie skraca ani nie wydłuża odliczania.

Obsługa usypiania i ekranu zależy też od sprzętu, sterowników oraz polityk Windows. Błąd przy końcowej akcji jest pokazywany w głównym oknie. Uśpienie korzysta z [`SetSuspendState`](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-setsuspendstate); wyłączenie z [`InitiateSystemShutdownEx`](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-initiatesystemshutdownexw) z zerowym opóźnieniem i parametrem `bForceAppsClosed` zależnym od ochrony dokumentów: `false` przy włączonej ochronie, `true` przy wyłączonej.

## Odinstalowanie

Zamknij EasyShut, znajdź go w **Ustawienia → Aplikacje → Zainstalowane aplikacje** i wybierz **Odinstaluj**. Możesz też dwukrotnie kliknąć `EasyShut-uninstall.exe` w katalogu instalacji.

Usuwane są pliki programu, skrót w menu Start i wpis w PATH. Inne pliki w katalogu pozostają nietknięte. Wersję przenośną z ZIP wystarczy zamknąć i usunąć jej wypakowany folder. Zapisane ustawienia zaawansowane pozostają w `%LOCALAPPDATA%\EasyShut`; aby je zresetować, po zamknięciu programu usuń `settings.xml` z tego folderu.

## Budowanie i testy

Kompilacja korzysta z `csc.exe` dostarczanego z .NET Framework. Nie pobiera pakietów NuGet i nie wymaga dodatkowego SDK.

```powershell
.\scripts\build.ps1              # EXE w build/release
.\scripts\test.ps1               # parser, logika czasu i zapis ustawień, atrapa zasilania
.\scripts\integration-test.ps1   # rzeczywiste procesy CLI/GUI i IPC, atrapa zasilania
.\scripts\instance-test.ps1      # równoczesny start wielu kopii, trwałość ustawień
.\scripts\native-smoke.ps1       # rzeczywista blokada/zwolnienie, bez wyłączania PC
.\scripts\installer-test.ps1     # instalacja, aktualizacja, wycofanie błędu i usuwanie plików
.\scripts\package.ps1            # instalator EXE, wersja przenośna ZIP i SHA256SUMS.txt
```

Testy integracyjne używają osobno kompilowanego backendu (`TESTING`) i osobnego kanału IPC. Ten backend nie zawiera kodu wyłączania, usypiania ani gaszenia ekranu; zapisuje tylko zdarzenia i pozwala symulować upływ czasu. Produkcyjne pliki EXE nie zawierają zegara testowego ani przełącznika wyłączającego rzeczywiste akcje.

`Core.cs` zawiera parser i maszynę stanów sesji, `Ipc.cs` komunikację w ramach tego samego konta i sesji logowania, `Cli.cs` polecenia terminalowe, `Gui.cs` okna i obsługę w tle, `AdvancedWindow.cs` edytor ustawień, `Settings.cs` ich atomowy zapis, `NativePower.cs` wywołania Windows, a `Setup.cs` instalator i deinstalator. Licznik i żądanie zasilania należą do jednego wątku; nie ma zapisanych harmonogramów, zadań systemowych, usług ani połączeń sieciowych. Instalator pozwala też na wdrożenie przez `EasyShut-Setup.exe --install-silent` (kod wyjścia 0 oznacza sukces).
