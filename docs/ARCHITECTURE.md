# OwO Maker — Architecture Map

> Generated 2026-07-11. Keep updated when structure changes.

Windows-only C# WinForms app (net8.0-windows, x86) that automates four NosTale minigames
(Stone Quarry, Saw Mill, Shooting Range, Fish Pond) by reading the 32-bit game process memory
(signature scan + pointer chains) and injecting input via `PostMessage`.

## Build / CI

- Solution: `OwO Maker.sln` — single project `OwO Maker/OwO Maker.csproj`.
- `TargetFramework: net8.0-windows`, `UseWindowsForms`, Platforms `AnyCPU;x86` — runtime requires x86 (32-bit pointers in `MemLib`).
- CI: `.github/workflows/build.yml` — windows-latest, `dotnet build -c Release -p:Platform=x86`, uploads artifact. **No test step (yet).**
- User settings (`Properties/Settings`): Disclaimer, FailChance, HumanTime, UseProdCoupon, ProdKey, LastMinigame.

## Classes

### `Program` (Program.cs)
- `public static bool botRunning` (Program.cs:8) — **global** master run flag; every minigame loop is `while (Program.botRunning)`.
- `public static Form1 form` (Program.cs:9) — global form ref; worker threads call `Program.form.UpdateStatus(...)` / `RemoveBotFromList(...)`.

### `Form1` (Gui.cs + Gui.Designer.cs) — god-object UI + orchestrace
State (Gui.cs:19-24):
- `List<int> HWndList` — enumerated NosTale window handles.
- `List<IntPtr> WindowList` — client HWNDs already added (dedupe guard).
- `List<Tuple<Thread, nint>> BotList` — running bots: worker `Thread` + client HWND.
- `bool Reset`, `bool IsStarted`.

Methods/handlers:
- `RefreshHandle()` (Gui.cs:43) + `ShowWindowHandler` (Gui.cs:51) — finds "NosTale" windows, renames to `NosTale - (<hWnd>)`, fills `listBox1`.
- `Form1_Load` (Gui.cs:89) — requires Administrator, `LoadSettings()`, `RefreshHandle()`.
- `button1_Click` (Gui.cs:103) — Refresh Client List.
- `button5_Click` (Gui.cs:117) — **Add Bot**: validates, resolves `ButtonResolution` from client rect, parses BotID from window title (`Convert.ToInt32`, Gui.cs:175/189), creates `new Thread(() => new Minigames.XXX().RunTask(...))` (not started), adds tuple to `BotList` + row to `listView1` (Gui.cs:189-196).
- `GetWantedMinigame()` (Gui.cs:207) — radio → `Minigame` enum (bug: TypeWriter mapped 2×, ř. 214-215; Memory nemapováno).
- `button2_Click` (Gui.cs:266) — **Start All**: `Program.botRunning = true`, `Thread.Start()` pro všechny, `IsStarted = true`.
- `button4_Click` (Gui.cs:239) — **Stop / Delete All**: `Thread.Interrupt()` všem, `Program.botRunning = false`, clear `BotList`/`WindowList`/`listView1`.
- `UpdateStatus(botID, game, Level, points, prodPoints, useProdCoupon, humanTime, progress)` (Gui.cs:294) — Invoke-marshalled; přepíše řádek listView podle botID.
- `RemoveBotFromList(nint botID)` (Gui.cs:308) — Invoke-marshalled; odebere z `BotList` (match `x.Item2 == botID`), `listView1`, `WindowList`; když prázdno → `IsStarted = false`.
- `FindListViewItemByBotID(int)` (Gui.cs:326) — lineární scan sub-item textů.
- `SaveSettings`/`LoadSettings` (Gui.cs:335/371); `Form_Closed` (Gui.cs:108) — save + `botRunning = false`.

**Latentní bug:** `RemoveBotFromList` matchuje `BotList` podle client HWND (`Item2`), ale minigames předávají int `BotID` z titulku okna — jiná čísla, `BotList.Remove` typicky nic neodebere (ListView řádek se odebere přes text match).

### GUI ovládací prvky (Gui.Designer.cs)
- tabPage1 "Main": `listBox1` (klienti), `button1` Refresh, `button5` Add Bot; Play Settings: `t_Level` (ComboBox 1-5), `t_Times` (default "20"), `t_FailChance`, `ProductionCouponKey` (0-9), `HumanTime`, `ProductionCoupon`; radio minihry: `StoneQuarry`, `SawMill`, `ShootingRange`, `FishPond` (default), `TypeWriter`+`Memory` disabled.
- tabPage2 "Running Bots": `listView1` (Details) sloupce: BotID, Minigame, Level, Points, Prod Points, Use Prod. Coupon, Human Time, Progress (Designer:413-451). `button2` Start All, `button4` Stop / Delete All.

### Minigames (Minigames/*.cs) — 4 copy-paste třídy bez společné base
Každá: `private int playedGames = 0` (úspěchy), vlastní `Mem mem`,
`public async void RunTask(IntPtr hWnd, int Amount, ButtonResolution buttons, int BotID, int level, bool HumanTime, bool UseProdCoupon, int FailChance, uint ProductionsCouponKey)` — celé tělo bota, spouštěné na dedikovaném Threadu.

Loop: init Mem + 3 signatury (fail → RemoveBotFromList + MessageBox + return), pak `while (Program.botRunning)`:
- čti pointery, minigame typ, prod points; `UpdateStatus` každý tick,
- `Status.Playing` → timing/aim logika + klávesy (fail-chance může úmyslně kazit),
- konec hry: `points >= GetRequiredPoints` && GameEnd* → `CollectReward` + `playedGames++` (SawMill.cs:97, StoneQuarry.cs:111, ShootingRange.cs:115, FishPond.cs:120); jinak `FailTryAgain` (bez počítadla!),
- `playedGames >= Amount` → finální UpdateStatus, RemoveBotFromList, MessageBox "Bot: {BotID} Done!", return,
- failure cesty (RemoveBotFromList + MessageBox + return): došly prod points bez kuponu, kupon nezabral, arrow button nenalezen.

### Helpers
- `Mem` (Helpers/MemLib.cs, ns `OwOMaker.Helpers`) — RPM/WPM, `Init(Process)`, `FindProcessByHandle(hWnd)`, `ReadPointer` (4B, 32-bit), `ReadMemory<T>`, `FindPattern(s)`.
- `SigScan`/`SignatureEntity` (Helpers/SigScan.cs) — dump modulu + pattern match `x`/`?`.
- `Structs` (Helpers/Structs.cs) — čistá data: signatury (`TMiniGameManager`, `TMiniGamePoints`, `TArrowWidget`), enumy `Minigame`, `MinigameID`, `Status` (Nothing/GameStart/Playing/GameEnd/GameEnded1/GameEnded2/FishComboEvent=0xFF), `Arrow`, offset tabulky per hra.
- `BackgroundHelper` (Helpers/BackgroundHelper.cs) — `SendKey`/`SendClick` přes PostMessage, `KeyCodes`, `CheckIfCursorIsMovingInClient`.
- `ButtonResolutionHelper` (Helpers/ButtonResolution.cs) — `ButtonResolution` DTO + hard-coded souřadnice pro 8 rozlišení; jinak `null`.
- `SharedRoutines` (Helpers/SharedRoutines.cs) — statická sdílená logika: `CalculateFailChance`, `CollectReward`, `FailTryAgain`, `EnterMinigame`, `UseProductionCoupon`, `GetStatus`, `GetRequiredPoints(minigame, level)`, `GetCurrentMiniGameID`, `FindMinigameArrowButton`, `IsMinigameUseWidgetVisible`.

## Lifecycle bota (end-to-end) — po zavedení per-bot řízení (2026-07)
Projekt `OwO Maker.Core` (net8.0, bez WinForms) + testy `OwO Maker.Core.Tests` (xUnit, 33 testů, CI krok `dotnet test`):
- `BotState` — `Created → Running ⇄ Paused → Stopped` (Stopped terminální).
- `BotControl` — `Start/Pause/Resume/Stop` (bool = přechod proběhl), `ShouldContinue` (false jen ve Stopped), `WaitIfPaused()`/`WaitIfPausedAsync()` (bez pollingu, TCS), `event StateChanged`.
- `BotStats` — thread-safe `RecordSuccess/RecordFailure`, `Attempts/Successes/Failures/SuccessRate`, `GetSummary()` → "Attempts: N, Successful: M (P %)".

App: `BotEntry` (OwO Maker/BotEntry.cs) = BotId + ClientHwnd + Thread + Control + Stats + ThreadStarted; `BotList` je `List<BotEntry>`. `Program.botRunning` odstraněn.
- Add Bot → entry + thread (nestartuje), řádek v `listView1` s 9. sloupcem **State** (aktualizuje `StateChanged` přes BeginInvoke).
- Start All / kontextové menu Start → `Control.Start()` + `Thread.Start()` (poprvé); Start All pausnuté i resumuje.
- Kontextové menu na `listView1` (stavěné v konstruktoru Form1): Start/Pause/Resume/Stop per bot, enable podle stavu, lookup přes `GetSelectedBotEntry()` (SubItems[0] = BotID).
- Smyčky miniher: `while (control.ShouldContinue)` + `await control.WaitIfPausedAsync()` na začátku ticku (pauza platí od dalšího ticku). `RecordSuccess` u `playedGames++`; `RecordFailure` jen když status je GameEnd* (jinak by čekání na start hry nafukovalo pokusy). Všechny terminální MessageBoxy obsahují `stats.GetSummary()`.
- Stop (per bot i All) = `Control.Stop()`; smyčka tiše skončí na dalším ticku. `Thread.Interrupt` odstraněn (byl stejně neúčinný — `RunTask` je `async void`, po prvním awaitu běží na thread poolu).
- Opravené bugy: `RemoveBotFromList(int)` maže podle BotId a z `WindowList` odebírá `ClientHwnd` (dřív mazal BotID z listu HWNDů → klient nešel znovu přidat); `FindListViewItemByBotID` matchuje jen sloupec 0; `UpdateStatus` iteruje přes 8 polí row (nesahá na State sloupec); `IsStarted` odstraněn.

**QoL kolo 2 (2026-07-12):**
- `BotStats` navíc měří aktivní čas běhu: `StartRun/PauseRun/ResumeRun` (pauzy se nepočítají; GUI je volá ze `StateChanged`), `Elapsed`, `AverageRound`; `GetSummary()` pak končí `, Avg round: 1:23, Total: 27:40`. Injektovatelný `TimeProvider` (testy používají fake).
- Záložka Running Bots: tabulka zmenšená (115px) + pod ní **log panel** `logList` (500 záznamů, auto-scroll). `Form1.Log(msg)` a `NotifyBotEnded(botID, msg)` (log + `SystemSounds.Asterisk`). Všechny MessageBoxy z worker threadů miniher nahrazeny `NotifyBotEnded`; informační boxy v Gui (started/stopped/added) nahrazeny logem; validační chyby zůstaly popupy.
- Tlačítka: Start All (8), Pause All (234), Stop/Delete All (461), 140×27 na y=243, kotvená dole. (Resume All odebráno — Start All pausnuté resumuje.)
- Sloupce listView1: BotID, Minigame, Level, Points, Prod Points, Progress (5, owner-drawn zelený bar dle "a/b"; "x/∞" jen text — bez známého cíle bar nejde), Success (6, "15/20 (75 %)"), **Action (7)** — owner-drawn tlačítko (ButtonRenderer) s labelem dle stavu: Created→Start, Running→Pause, Paused→Resume; klik obsluhuje `listView1.MouseClick` + HitTest. `UpdateStatus(botID, game, level, points, prodPoints, progress, success)`. Sloupce Use Prod. Coupon a Human Time odstraněny. Pravý klik (kontextové menu) zůstává hlavně kvůli per-bot Stop.
- Potvrzení přidání bota: MessageBox (vrácen na žádost uživatele) + řádek v logu.
- Minihry: detekce zavřeného klienta (`proc.HasExited` na začátku ticku → "Client closed" + konec), `stats.StartRun()` před smyčkou, lokální `SuccessText()`.
- `GetWantedMinigame`: Memory správně mapuje na 5 (dřív dvakrát TypeWriter). TypeWriter/Memory zůstávají disabled — nejsou implementované (žádná bot třída ani offsety).

**Max režim (checkbox `MaxGames` u pole Times):** zamkne `t_Times` a bot hraje, dokud stačí produkční body — interně `Amount = int.MaxValue` (funguje přirozeně: `playedGames >= Amount` nikdy nenastane a `CollectReward` vždy kliká Try Again), progress se zobrazuje `X/∞` a dojití bodů je normální „Done! Ran out of production points." místo chyby. `RunTask` má poslední parametr `bool unlimited`. Checkbox se persistuje jako setting `MaxGames`; persistují se i `Times` a `Level`.

**Character-name probe (větev `feature/character-name-probe`, 2026-07-12):** základ pro profily botů (TODO #2).
- `Structs.Pattern.TPlayerManager` — signatura z NosSmooth.Local; operand instrukce `A1` je na +6 → statický ukazatel na PlayerManager. Offsety: `TPlayerManager.Player = 0x20` (0 = login/char select), `PlayerId = 0x24`, `TMapPlayer.NamePtr = 0x1EC` (Delphi AnsiString: int délka na ptr−4, ASCII data na ptr).
- `CharacterName` (Core + testy) — `TryDecode(length, bytes, out name)` a `IsPlausible`; garbage délka/nepotisknutelné bajty (= drift offsetu) vrací false.
- `PlayerInfoProbe.Run(Mem)` (Helpers) — krokovaná diagnostika (pattern → static → manager → player → name) včetně hex dumpu okolí `Player+0x1EC` pro odhalení driftu.
- Tlačítko **Read Memory** na Main tabu (vedle Refresh, `buttonReadMemory`): probne vybraného klienta (bez výběru všechny), loguje do log panelu a přepne na tab Running Bots.

Lokální vývoj na macOS: `~/.dotnet/dotnet` (SDK 8; systémový je v7). App jde zkompilovat i tady: `dotnet build "OwO Maker/OwO Maker.csproj" -p:EnableWindowsTargeting=true`.

## Testovatelnost
- Žádné testy, žádný test projekt, CI netestuje.
- Silný coupling: RunTask = memory I/O + PostMessage + `Program.form` statics + MessageBox z worker threadů. Žádné interfaces/DI.
- Čisté ostrůvky: `CalculateFailChance`, `GetRequiredPoints`, `GetButtonPositions`, `MakeLParam`, enumy/offsety.
- Lokální vývoj na macOS: net8.0-windows nelze buildit/spouštět → testovatelná logika patří do samostatné net8.0 class library (`OwO Maker.Core`), testy `dotnet test` běží i na macOS, CI Windows ověří celek.
