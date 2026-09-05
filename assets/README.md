# Ikona EasyShut

Wybrany znak: **D5 z lekko grubszą i dłuższą kreską zasilania**. Białe E i S, zaokrąglone linie, przezroczyste tło. Kreska zasilania pozostaje oddzielona od S.

- `EasyShut.svg` — źródło wektorowe, identyczne z wybranym wariantem w `icon-concepts/v5`.
- `EasyShut.png` — eksport 1024 × 1024.
- `EasyShut.ico` — 32-bitowe RGBA w rozmiarach 16, 20, 24, 32, 40, 48, 64, 96, 128 i 256 px.
- `EasyShut-black.svg`, `EasyShut-black.png`, `EasyShut-black.ico` — czarny wariant, generowany z tego samego SVG; geometria i przezroczystość pozostają identyczne.

Oba warianty są osadzone w aplikacji, instalatorze i deinstalatorze. Mała ikona paska tytułu jest czarna na jasnym tle; przy kolorowym pasku lub wysokim kontraście wariant dobierany jest do koloru tła. Duża ikona działającego okna (pasek zadań / Alt+Tab) jest biała przy ciemnym motywie systemowym i czarna przy jasnym. Ikony są odświeżane po zmianie motywu, aktywacji okna i zmianie DPI, bez zmiany ustawień Windows.

Statyczna ikona pliku EXE i skrótów pozostaje biała: Windows nie przekazuje jej koloru tła pulpitu ani konkretnego widoku Eksploratora. Dobór koloru opisany powyżej dotyczy okien działającej aplikacji.

Budowanie aplikacji nie wymaga Node.js. Opcjonalne odtworzenie obu wariantów ICO i PNG ze źródłowego SVG: `node scripts/generate-icon.mjs` (pakiet `sharp`).

Starsze propozycje są zachowane w `icon-concepts` jako archiwum projektu.
