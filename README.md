# easyshut

Mała aplikacja dla Windows 10/11, która na czas bieżącej sesji blokuje automatyczne usypianie. Może też wyłączyć lub uśpić komputer po wybranym czasie. Ma klasyczne okno Windows i obsługę z terminala.

**[Pobierz gotowy program](https://github.com/szymongazinski/easyshut/releases/latest)** · [Licencja MIT](LICENSE)

## Instalacja

1. Pobierz `easyshut-1.0.0-windows.zip` z Releases i wypakuj cały pakiet.
2. W folderze pakietu uruchom PowerShell i wpisz:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

3. Otwórz nowe okno terminala. Polecenie `easyshut` działa teraz z dowolnego katalogu. W menu Start pojawi się też skrót **easyshut**.

Instalacja nie wymaga administratora. Pliki trafiają do `%LOCALAPPDATA%\Programs\easyshut`, a instalator dodaje ten katalog do PATH bieżącego użytkownika. Opcja `-ExecutionPolicy Bypass` dotyczy wyłącznie uruchamianego procesu instalatora; nie zapisuje zmiany zasad PowerShell.

Można też korzystać bez instalacji: `easyshut-window.exe` otwiera okno, a `easyshut.exe` obsługuje terminal. Oba pliki należy trzymać w jednym katalogu. Program korzysta z .NET Framework 4.8 dostępnego w aktualnych instalacjach Windows 10/11. Paczka nie zawiera dodatkowego środowiska, reklam ani telemetrii. Pliki EXE nie są podpisane certyfikatem wydawcy.

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
easyshut                              Otwórz okno (lub przywołaj już otwarte).
easyshut 0.25                         Wyłącz za 15 minut; zgaś ekran po 5 s.
easyshut 1.5 -sleep                    Uśpij za 90 minut; zgaś ekran po 5 s.
easyshut 1,5 -sleep -screen_on          Uśpij za 90 minut; utrzymuj ekran włączony.
easyshut -n                            Nigdy nie wyłączaj; zgaś ekran po 5 s.
easyshut -n -screen_on                 Blokuj usypianie i utrzymuj ekran włączony.
easyshut +1                            Dodaj godzinę do bieżącego odliczania.
easyshut +0,5                          Dodaj pół godziny.
easyshut -status                       Sprawdź stan i planowaną godzinę akcji.
easyshut -stop                         Anuluj sesję.
easyshut -help                         Wyświetl wszystkie flagi.
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

Polecenia z argumentami działają w tle i oddają terminal. Zamknięcie terminala nie anuluje sesji. Aby ją zakończyć, użyj `-stop` lub otwórz główne okno przez `easyshut` i zamknij je. Nie ma ikony w zasobniku ani autostartu.

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

Zakończ sesję i zamknij easyshut, a następnie:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\easyshut\uninstall.ps1"
```

Usuwane są pliki pakietu, skrót w menu Start i wpis w PATH. Inne pliki w katalogu pozostają nietknięte.

## Budowanie i testy

Kompilacja korzysta z `csc.exe` dostarczanego z .NET Framework. Nie pobiera pakietów NuGet i nie wymaga dodatkowego SDK.

```powershell
.\scripts\build.ps1              # EXE w build/release
.\scripts\test.ps1               # testy parsera i logiki czasu, z atrapą zasilania
.\scripts\integration-test.ps1   # rzeczywiste procesy CLI/GUI i IPC, atrapa zasilania
.\scripts\native-smoke.ps1       # rzeczywista blokada/zwolnienie, bez wyłączania PC
.\scripts\package.ps1            # gotowy ZIP i SHA256SUMS.txt w dist
```

Testy integracyjne używają osobno kompilowanego backendu (`TESTING`) i osobnego kanału IPC. Ten backend nie zawiera kodu wyłączania, usypiania ani gaszenia ekranu; zapisuje tylko zdarzenia i pozwala symulować upływ czasu. Produkcyjne pliki EXE nie zawierają zegara testowego ani przełącznika wyłączającego rzeczywiste akcje.

`Core.cs` zawiera parser i maszynę stanów sesji, `Ipc.cs` komunikację w ramach tego samego konta i sesji logowania, `Cli.cs` polecenia terminalowe, `Gui.cs` okna i obsługę w tle, a `NativePower.cs` wywołania Windows. Licznik i żądanie zasilania należą do jednego wątku; nie ma zapisanych harmonogramów, zadań systemowych, usług ani połączeń sieciowych.
