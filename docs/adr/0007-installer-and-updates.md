# ADR 0007 – Installer and updates with Velopack

- Status: **Accepted** (2026-10-23, owner's approval of phase 0.11 steps 2–3; plan in `docs/plan-0.11.md`)
- Deciders: project owner (tester), lead developer

## Context

Phase 0.11 needs an installer a blind user can run without administrator rights, and updates that are spoken
and never surprise the user. Velopack was chosen in the architecture (§1.1): per-user install in
`%LOCALAPPDATA%\<packId>`, no UAC, delta updates, GitHub Releases as a source. Facts checked in its
documentation and in the `Velopack` 1.2.161 package (MIT, published 2026-09-29):

- the program lives in `current`, which every update replaces; a launcher `hasi.exe` next to it has a path that
  survives updates;
- uninstalling deletes the whole `%LOCALAPPDATA%\<packId>` folder and the shortcuts, but says nothing about the
  `Run` key, which we therefore remove ourselves;
- `VelopackApp.Build().Run()` must run first in `Main`; hooks: `OnFirstRun`, `OnRestarted`, and
  `OnBeforeUninstallFastCallback` (no UI, ended after 30 s);
- by default Velopack applies a downloaded update at the next start (`SetAutoApplyOnStartup`).

## Decision

1. **Package:** id `HASI.App` (so `%LOCALAPPDATA%\HASI`, HASI's logs and data, is not Velopack's folder), title
   "HASI", self-contained `win-x64`: .NET and WPF ship inside, so nothing else is installed and no
   administrator prompt appears. Default shortcuts: Desktop and Start menu. Built by `installer/pack.ps1`
   with `vpk` pinned in `dotnet-tools.json`.
2. **RHVoice:** only HASI's CI build is packed (`rhvoice\` next to `hasi.exe`, with `BUILD-INFO.txt`, ADR 0004);
   the script refuses a folder without `BUILD-INFO.txt`. "Despre HASI" and `THIRD-PARTY-NOTICES.md` name
   RHVoice 1.18.4 and its commit, and the corrected licence (GPL-3.0-or-later as a whole, with LGPL-3.0 parts).
   The library itself reports "1.19.2" (upstream's `SConstruct`); the diagnostic log prints both.
3. **Entry point** `Program.Main` runs Velopack before anything else:
   - `SetAutoApplyOnStartup(false)`: HASI never restarts into an update by itself;
   - first run: autostart is turned on, and HASI says once that it will start with Windows (owner's decision);
   - restarted after an update: HASI says the new version first, and waits for the previous instance to end
     instead of handing over to it (single instance, 0.10);
   - before uninstall: the `Run` value, `%LOCALAPPDATA%\HASI` and `%APPDATA%\HASI` are deleted (owner's decision:
     a full removal; the installation guide and the README say so).
4. **Autostart** points to the launcher (`%LOCALAPPDATA%\HASI.App\hasi.exe`) when HASI runs installed.
5. **Updates** (`IUpdateService` in the core, `VelopackUpdates` in the app, `UpdateFlow` for what is said):
   - source from `settings.json` (`UpdateSource`) or `--update-source`; default GitHub Releases of
     `AllxCucu/hasi`; `folder:<path>` for tests without publishing;
   - at startup, if automatic checking is on: a version is offered **once** (remembered in
     `UpdateOfferedVersion`); Escape says where to find it later; only a newer version is offered again.
     Offline, not installed or failed: nothing is said at startup;
   - Settings → Actualizări: check now (always answers, always offers), automatic check on or off, the installed
     version;
   - download progress every 25 %, then "Actualizarea este gata. HASI pornește din nou.", then the update is
     applied after the speech (`ApplyUpdatesAndRestart`).
6. **"Simulare oprire" is off by default** from now on (ADR 0006); files that saved it on keep it on.

7. **No portable archive** (owner's decision, 2026-10-24). Velopack builds `HASI.App-win-Portable.zip` by
   default; HASI never ships it, because a portable copy has no installer, no uninstall, no autostart path that
   survives updates and no update flow. `vpk` 1.2.161 has `--noPortable` (checked with `vpk pack -H`), so
   `installer/pack.ps1` passes it (from step 0 of Phase 1) and the archive is not produced at all. If a future `vpk` drops the option,
   the archive may be produced but is never uploaded or published.
8. **First public release postponed** (owner's decision, 2026-10-24): 0.1.0 is not published. Step 4 of
   `docs/plan-0.11.md` stays ready and runs for **0.2.0, after Phase 1 module 3** (Documents), with the release
   conditions of `docs/plan-faza-1.md` §6.
9. **Uninstall still removes everything, personal data included** (owner's decision, 2026-10-24, confirming
   decision 3): Phase 1 personal data (agenda, editor drafts, recent documents, reading positions, daily backups)
   lives in `%LOCALAPPDATA%\HASI\userdata` and is deleted with the rest. No folder is kept on purpose. Documents the
   user saved elsewhere (by default the Windows Documents folder) are never touched. The agenda offers "Fă o copie
   de siguranță" and "Adu datele dintr-o copie"; the installation guide and the README say that uninstalling also
   deletes the agenda and that a backup must be made first.
10. **Reminders only while HASI runs** (owner's decision, 2026-10-24): no Windows Task Scheduler task, so the
    installer and the uninstall hook create and remove nothing beyond decision 3. Missed reminders are announced
    at the next start or resume, and closing HASI with a reminder due in the next 24 hours says so.
11. **Development copy of RHVoice** (owner's request, 2026-10-24): the X-10 uninstall also deleted
    `%LOCALAPPDATA%\HASI\rhvoice`, the developer copy used by `dotnet run`. Debug builds therefore also look in
    `%LOCALAPPDATA%\HASI.Dev\rhvoice`, which uninstall never touches; Release builds (the installed program) do not
    look there.

## Consequences

- `Shell.Show` applies results that arrive later (a check or a download), with an optional queued priority so
  an offer does not cut the welcome; `ScreenResult.Confirm` takes the text said after Escape.
- The installer is unsigned until SignPath (step 4 and later): Windows SmartScreen warns before installing.
- Not verified by me: installing and uninstalling on a real account (it would change the owner's autostart and
  shortcuts), and an update between two installed versions. They are the owner's scenarios X-01…X-09.
