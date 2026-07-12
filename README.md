# OwO Maker

The spiritual successor of [UwU Maker](https://github.com/Bappsack/UwU-Maker).

> **This is a fork** of [Bappsack/OwO-Maker](https://github.com/Bappsack/OwO-Maker) — see [What's different in this fork](#whats-different-in-this-fork) below.

### Disclaimer:

- using bots is never 100% safe! Use it at your own risk.
- Requires admin permissions since Gameforge launches NosTale with admin permissions for whatever reason.
- Requires [.NET 8.0 Desktop Runtime (x86)](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x86-installer) to be installed on your Computer

## Main Features:

- All Main Minigames are supported (Stone Quarry, Saw Mill, Shooting Range, Fish Pond).
- Completly working in background.
- All Resolutions supported.
- Memory based instead of Pixel/Image Detection.
- Production Coupons are supported from the Skillbar (0-9).
- Fail% Chance to simulate human gameplay
- A semi Humantime mode for Sawmill/Fishing Pond to slow down the gameplay.
- Selectable Level and amount of plays.
- Multiple Client usage possible.

## What's different in this fork

The upstream bot could only start and stop **all** bots at once, popped up a modal
message box every time a bot finished (annoying with 3+ clients), and forgot half of
your settings between runs. This fork focuses on quality of life when running several
bots for long sessions:

**Per-bot control**
- Right-click a bot in the *Running Bots* list to **Start / Pause / Resume / Stop** just
  that one — the others keep running. Pause takes effect at the next round tick.
- **Pause All / Resume All** buttons next to Start All / Stop All.
- Live **State** column (Created / Running / Paused / Stopped).

**Statistics**
- Every bot counts its attempts and successful rounds; a live **Success** column shows
  e.g. `15/20 (75 %)` and the **Progress** column renders as a progress bar.
- When a bot ends (done, out of points, error), the report includes the full run stats:
  `Attempts: 20, Successful: 15 (75 %), Avg round: 1:23, Total: 27:40` — paused time is
  excluded from the timing.

**Max mode**
- Check **Max** next to the amount field and the bot plays *until production points (or
  coupons) run out* instead of a fixed count — useful when you don't know how many games
  your points can cover. Progress shows `x/∞` and running dry ends as a normal *Done*
  with stats, not an error.

**Log instead of popups**
- Bot events (finished, out of points, client closed, …) go to a **log panel** with a
  notification sound instead of modal message boxes. Only input-validation errors still
  pop up.
- A closed game client is detected and ends the bot gracefully ("Client closed").

**Remembered settings**
- Amount of games, level and the Max checkbox now persist across restarts (previously
  the amount always reset to 20).

**Under the hood**
- New `OwO Maker.Core` library (plain .NET 8, no WinForms) with the per-bot state
  machine (`BotControl`) and statistics (`BotStats`), developed test-first — 45 xUnit
  tests run in CI (and on any OS) before the Windows build.
- Assorted bug fixes: stopped clients couldn't be re-added, wrong ListView row could be
  updated when IDs collided with point values, `Stop All` relied on `Thread.Interrupt`
  that never worked, crashes on out-of-range inputs, and more (see
  [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)).

### Notes

Back in 2020 i made [UwU Maker](https://github.com/Bappsack/UwU-Maker) out of boredom and never really worked on it again, it was based on pixel/image detection and worked fine for the most people but it was slow and didn't supported all games. 
Now i'm sitting here again having nothing better todo so i decided to port it over to be memory based and thought it would be better to have a seperate repo for this.

Thanks to [morsisko](https://github.com/morsisko) for his [Sawmill Bot](https://github.com/morsisko/SawmillBot) Structs.
