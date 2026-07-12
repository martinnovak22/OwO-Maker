# TODO — větší úkoly (děláme po jednom, každý jako samostatná větev)

## 1. Škálování souřadnic — podpora libovolného rozlišení
`ButtonResolutionHelper.GetButtonPositions` má natvrdo souřadnice tlačítek pro 8 rozlišení
(1024x768 … 2560x1440); jiné rozlišení = bot odmítne klienta. Cíl: přepočítávat souřadnice
z jednoho referenčního rozlišení poměrem (nebo kotvit ke středu/okrajům podle toho, jak hra
UI umisťuje).

- Nejdřív ověřit, jak NosTale UI škáluje: vzít 2–3 podporovaná rozlišení z tabulky a zkusit
  jejich souřadnice vyjádřit jako funkci šířky/výšky (lineární? kotvené k okraji?).
  Data už v tabulce jsou — jde to analyzovat bez hry.
- `MinigameArrows` a `LevelButtons` mohou škálovat jinak než dialogová tlačítka
  (Reward/TryAgain/GameStart) — ověřit zvlášť.
- Čistý výpočet → patří do `OwO Maker.Core` s testy (vstup: rozlišení, výstup: sada bodů;
  testy proti stávajícím 8 známým tabulkám jako ground truth).
- Fallback: když se výpočet netrefí, nechat starou tabulku jako override.

## 2. Profily botů (pre-sety)
Uložit sestavu botů (minihra, level, počet/Max, kupóny, klávesa, human time) a jedním
klikem ji obnovit po restartu hry/aplikace.

- **Blokátor:** klient se identifikuje přes HWND/PID, které se mění při každém startu hry.
  Vazba na postavu vyžaduje přečíst jméno postavy z paměti klienta — prozkoumat, zda je
  v dosahu existujících signatur (`TMiniGameManager` okolí) nebo je potřeba nová signatura.
- Bez jména postavy jde udělat slabší varianta: profil bez vazby na klienta — uloží se
  jen Play Settings sestavy a při obnově se přiřadí klienti ručně v pořadí.
- Persistence: JSON vedle user settings (ne do Properties.Settings — pole s objekty se
  tam spravuje špatně).

## 3. BotManager — vyčlenit orchestraci z Form1
Čistý refactor, funkce se nemění. Form1 je god-object: drží `BotList`, `WindowList`,
vytváří vlákna, mapuje minihry, obsluhuje kontextové menu i owner-draw.

- `BotManager` v `OwO Maker.Core`: správa kolekce `BotEntry` (add/remove/start/pause/
  resume/stop jednotlivě i hromadně), eventy pro GUI (BotAdded/BotRemoved/BotStateChanged).
  Pokrýt testy.
- `BotEntry` přesunout do Core (Thread tam může zůstat, nebo nahradit `Task.Run`).
- Vazba Control→Stats (StartRun/PauseRun/ResumeRun ze StateChanged) přesunout z GUI do
  Core (poznámka ze standards review: Feature Envy).
- Při té příležitosti: `RunTask` má 12 parametrů → zabalit do run-context objektu;
  factory mapa `Minigame → bot` místo čtyř if-ů v `button5_Click`; pojmenované konstanty
  indexů sloupců (Progress=5, State=7).
- Nechat na dobu, kdy nebudou rozpracované jiné změny — sahá do všeho.

## Menší odložené nápady
- Historie běhů / perzistentní statistiky (CSV/JSON) — odloženo, málo užitku za tu práci.
- Windows toast notifikace místo zvuku.
- Implementace TypeWriter/Memory miniher (reverse engineering offsetů) — UI pro ně bylo
  odebráno, enum hodnoty v `Structs.Minigame` zůstávají.
