# EasyShut

Mała aplikacja dla Windows 10/11, która na czas bieżącej sesji blokuje automatyczne usypianie. Może też wyłączyć lub uśpić komputer po wybranym czasie. Ma klasyczne okno Windows i obsługę z terminala.

**[Pobierz gotowy program](https://github.com/szymongazinski/EasyShut/releases/latest)** · [Licencja MIT](LICENSE)

## Instalacja

1. **[Pobierz EasyShut-Setup.exe](https://github.com/szymongazinski/EasyShut/releases/latest/download/EasyShut-Setup.exe).**
2. Otwórz pobrany plik **dwuklikiem** i kliknij **Zainstaluj**.
3. Kliknij **Zakończ**. Domyślnie otworzy się okno EasyShut.

Nie trzeba otwierać terminala ani uruchamiać plików `.ps1`. Instalator zawiera cały program i działa bez pobierania dodatkowych plików. Przy aktualizacji najpierw zamknij EasyShut.

Instalacja nie wymaga administratora. Pliki trafiają do `%LOCALAPPDATA%\Programs\EasyShut`. Instalator dodaje skrót **EasyShut** do menu Start, polecenie `EasyShut` do PATH bieżącego użytkownika i pozycję EasyShut na liście zainstalowanych aplikacji Windows. Aby używać polecenia, otwórz **nowe okno terminala**. Wielkość liter w poleceniach Windows nie ma znaczenia, więc dotychczasowy zapis małymi literami nadal działa.

Można też korzystać bez instalacji: pobierz `EasyShut-1.1.0-windows.zip` i wypakuj cały pakiet. `EasyShut-window.exe` otwiera okno, a `EasyShut.exe` obsługuje terminal. Pliki należy trzymać w jednym katalogu. Program korzysta z .NET Framework 4.8 dostępnego w aktualnych instalacjach Windows 10/11. Nie zawiera reklam ani telemetrii. Pliki EXE nie są podpisane certyfikatem wydawcy.

## Okno

- Czas: **15 minut**, **1 godzina**, **6 godzin**, **Nigdy** lub własna liczba godzin z ułamkiem (`1,5` albo `1.5`).
- Dla odliczania: **Wyłączenie** (domyślnie) albo **Uśpienie**. Przy **Nigdy** wybór akcji jest schowany.
- **Wyłącz ekran 5 sekund po kliknięciu Uruchom**. Bez zaznaczenia ekran pozostaje włączony przez całą sesję.
- Domyślne ustawienia: **Nigdy**, ekran włączony. Samo otwarcie okna nie uruchamia blokady; kliknij **Uruchom**.
- **Anuluj sesję** zatrzymuje odliczanie i zwalnia blokadę usypiania. Okno pozostaje otwarte.
- **Zamknięcie głównego okna kończy cały program i zapomina sesję**, także rozpoczętą wcześniej w terminalu. Zminimalizowanie okna pozostawia sesję aktywną.
- Ponowne **Uruchom** zaczyna sesję od nowa z wybranymi ustawieniami.

## Terminal

```text
EasyShut                              Otwórz okno (lub przywołaj już otwarte).
EasyShut 0.25                         Wyłącz za 15 minut; zgaś ekran po 5 s.
EasyShut 1.5 -sleep                    Uśpij za 90 minut; zgaś ekran po 5 s.
EasyShut 1,5 -sleep -screen_on          Uśpij za 90 minut; utrzymuj ekran włączony.
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
| `+godziny` | Dodaj czas do pozostałego odliczania; samodzielne polecenie, tylko w terminalu. |
| `-status` | Stan sesji, pozostały czas, przewidywany termin i zachowanie ekranu. |
| `-stop` | Anulowanie i zwolnienie blokady usypiania. |
| `-help` | Pomoc. Działają też `--help`, `-h` i `/?`. |

Polecenia z argumentami działają w tle i oddają terminal. Zamknięcie terminala nie anuluje sesji. Aby ją zakończyć, użyj `-stop` lub otwórz główne okno przez `EasyShut` i zamknij je. Nie ma ikony w zasobniku ani autostartu.

Kolejne polecenie z czasem lub `-n` **zastępuje** ustawienia i zaczyna od nowa. `+godziny` dodaje czas do aktualnego terminu, zachowując akcję i zachowanie ekranu; w trybie **Nigdy** nic nie zmienia. Bez aktywnej sesji zgłasza błąd. Błędne argumenty nie zmieniają bieżącej sesji.

Minimalny czas wynosi 1 sekundę; maksymalny 876000 godzin (100 lat), także po dodawaniu czasu. Kody zakończenia CLI: `0` sukces, `1` błąd działania, `2` błędne argumenty.

## Ostrzeżenia i koniec odliczania

- Dla łącznego zaplanowanego czasu **co najmniej 3 godziny** ostrzeżenie pojawia się 15 minut przed akcją.
- Ostrzeżenie 1 minutę przed akcją pojawia się zawsze. Dla sesji krótszej niż minuta pojawia się od razu po uruchomieniu.
- Ostrzeżenia mają aktualny licznik i przycisk **Anuluj sesję**. **Rozumiem** i krzyżyk w oknie ostrzeżenia jedynie chowają ostrzeżenie — odliczanie trwa dalej.
- Dodanie czasu powyżej danego progu ponownie uzbraja odpowiadające mu ostrzeżenie. Próg 3 godzin uwzględnia czas dodany przez `+godziny`.
- **Wyłączenie jest wymuszone: aplikacje z niezapisanymi dokumentami także zostają zamknięte, co oznacza utratę niezapisanych zmian.** Uśpienie zachowuje otwarte aplikacje.
- Po wykonaniu akcji sesja jest anulowana. Program nie powtórzy uśpienia po wznowieniu komputera.

## Zachowanie Windows

Program używa czasowego żądania [`SetThreadExecutionState`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate). Nie modyfikuje planu zasilania, czasu automatycznego usypiania ani rejestru z ustawieniami zasilania. Po zakończeniu sesji lub procesu ponownie obowiązują dotychczasowe ustawienia Windows. Bezczynny proces w tle kończy się po około 10 sekundach; blokada jest zwalniana od razu po `-stop`.

Wyłączenie ekranu jest jednorazowe, po 5 sekundach od startu sesji. Mysz lub klawiatura może go później obudzić; odliczanie i blokada usypiania działają dalej. Powiadomienia nie wymuszają obudzenia zgaszonego ekranu, ale emitują systemowy dźwięk ostrzeżenia, jeśli dźwięki są włączone.

Ręczne wyłączenie, wylogowanie i restart kończą proces oraz sesję. Program nie blokuje ręcznego uśpienia ani działań wymuszanych przez Windows (np. krytycznej baterii). Jeśli ręcznie uśpisz komputer przed terminem i później wznowisz, czas nadal jest liczony; miniony termin zostanie obsłużony po wznowieniu. Zmiana zegara systemowego nie skraca ani nie wydłuża odliczania.

Obsługa usypiania i ekranu zależy też od sprzętu, sterowników oraz polityk Windows. Błąd przy końcowej akcji jest pokazywany w głównym oknie. Uśpienie korzysta z [`SetSuspendState`](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-setsuspendstate); wyłączenie z [`InitiateSystemShutdownEx`](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-initiatesystemshutdownexw) z wymuszeniem zamknięcia aplikacji i zerowym opóźnieniem.

## Odinstalowanie

Zamknij EasyShut, znajdź go w **Ustawienia → Aplikacje → Zainstalowane aplikacje** i wybierz **Odinstaluj**. Możesz też dwukrotnie kliknąć `EasyShut-uninstall.exe` w katalogu instalacji.

Usuwane są pliki programu, skrót w menu Start i wpis w PATH. Inne pliki w katalogu pozostają nietknięte. Wersję przenośną z ZIP wystarczy zamknąć i usunąć jej wypakowany folder.

## Budowanie i testy

Kompilacja korzysta z `csc.exe` dostarczanego z .NET Framework. Nie pobiera pakietów NuGet i nie wymaga dodatkowego SDK.

```powershell
.\scripts\build.ps1              # EXE w build/release
.\scripts\test.ps1               # testy parsera i logiki czasu, z atrapą zasilania
.\scripts\integration-test.ps1   # rzeczywiste procesy CLI/GUI i IPC, atrapa zasilania
.\scripts\native-smoke.ps1       # rzeczywista blokada/zwolnienie, bez wyłączania PC
.\scripts\installer-test.ps1     # instalacja, aktualizacja, wycofanie błędu i usuwanie plików
.\scripts\package.ps1            # instalator EXE, wersja przenośna ZIP i SHA256SUMS.txt
```

Testy integracyjne używają osobno kompilowanego backendu (`TESTING`) i osobnego kanału IPC. Ten backend nie zawiera kodu wyłączania, usypiania ani gaszenia ekranu; zapisuje tylko zdarzenia i pozwala symulować upływ czasu. Produkcyjne pliki EXE nie zawierają zegara testowego ani przełącznika wyłączającego rzeczywiste akcje.

`Core.cs` zawiera parser i maszynę stanów sesji, `Ipc.cs` komunikację w ramach tego samego konta i sesji logowania, `Cli.cs` polecenia terminalowe, `Gui.cs` okna i obsługę w tle, `NativePower.cs` wywołania Windows, a `Setup.cs` instalator i deinstalator. Licznik i żądanie zasilania należą do jednego wątku; nie ma zapisanych harmonogramów, zadań systemowych, usług ani połączeń sieciowych. Instalator pozwala też na wdrożenie przez `EasyShut-Setup.exe --install-silent` (kod wyjścia 0 oznacza sukces).
