# ADR 0006 – Restart and shutdown that never leave the user in silence

- Status: **Accepted** (2026-10-23, owner's approval)
- Deciders: project owner (tester), lead developer

## Context

Until now, "Repornește calculatorul" / "Oprește calculatorul" said goodbye, ran `shutdown.exe /r|/s /t 0`, checked
its exit code and closed HASI. `shutdown.exe` returns 0 as soon as Windows accepts the request, before Windows
asks the programs whether they agree. The owner reproduced the result with Notepad holding unsaved text: HASI was
already closed when Windows showed its full-screen "these apps are preventing restart" screen, so a blind user
heard nothing and did not know what had happened.

Windows sends `WM_QUERYENDSESSION` to the programs; a program that refuses, or does not answer within about
five seconds, makes Windows show the blocking screen ([Shutting Down](https://learn.microsoft.com/windows/win32/shutdown/shutting-down)).
If the user cancels there, the programs receive `WM_ENDSESSION` with `wParam = FALSE`.

## Decision

1. **Before the question,** HASI lists the other programs with open windows (visible, titled, unowned, not tool
   windows, not cloaked; Windows' own shell left out) and says their **names only**, never window titles, followed
   by "Dacă aveți modificări nesalvate, se pot pierde." HASI cannot know whether changes are unsaved, so it says
   "se pot pierde", not "se vor pierde".
2. **HASI stays open** after a real restart or shutdown is requested: it says "Calculatorul se repornește. Așteptați,
   vă rog." and lets Windows close it when the session really ends. HASI never blocks the session end (no
   `ShutdownBlockReasonCreate`, `SessionEnding` is never cancelled).
3. **The outcome is followed** by a pure state machine in the core (`PowerWatch`), fed by the host with
   `WM_QUERYENDSESSION` / `GetSystemMetrics(SM_SHUTTINGDOWN)`, `WM_ENDSESSION(FALSE)` and a one-second timer:
   - `shutdown.exe` fails: "Calculatorul nu a putut fi repornit. HASI rămâne deschis."
   - no session end after 10 s: "Calculatorul nu a început repornirea. …"
   - the session end began but the computer is still on after 10 s: "Repornirea așteaptă. Probabil un program are
     modificări nesalvate …", with the open programs, repeated every 30 s;
   - cancelled: "Repornirea a fost anulată.", with the programs still open and, if Windows took the keyboard,
     how to return to HASI.
   While waiting, the "HASI nu mai este în prim-plan" notice is held back and said with the outcome.
4. **No `/f` by default.** Forcing is offered only after an attempt in this session was blocked or cancelled and
   programs are still open, as a second, separate confirmation that says the unsaved changes are lost.
   Escape there cancels everything.
5. **Full shutdown.** HASI never passes `/hybrid`. Microsoft documents a full shutdown as the default of
   `shutdown.exe`, and Fast Startup as opt-in through `/hybrid`
   ([Microsoft](https://learn.microsoft.com/en-us/troubleshoot/windows-client/setup-upgrade-and-drivers/fast-startup-causes-system-hibernation-shutdown-fail));
   a restart is always a full boot. One secondary source claims a plain `/s` is hybrid when Fast Startup is on;
   the owner checks it on the real PC with the Kernel-Boot event 27 ("boot type 0x0" = full).
6. **"Simulare oprire"** (Settings → Diagnostic, `UserSettings.SimulatePower`), **on by default during development**.
   When on, the question adds "Mod de test: calculatorul nu va reporni." (or "nu se va opri."), HASI logs
   `POWER simulated: shutdown.exe …` and closes, the computer stays on. Turning it off makes the next start say once
   "Atenție: repornirea și oprirea calculatorului sunt reale." `--simulate-power` and `--real-power` override it
   for one session (`--simulate-power` wins if both are given). **Before phase 0.11 the default becomes off.**
   Done in phase 0.11 (ADR 0007): off by default; a settings file that saved it on keeps it on (no migration,
   the owner's decision).

## Consequences

- `ISystemActions` gains `RequestPower(action, force)`, `PowerIsSimulated` and `OpenPrograms()`; the shell owns the
  wording and the state, so every path is covered by `ShellHarness` tests with a fake clock.
- To be measured by ear on the owner's PC, then the wording is finalized: whether HASI's voice is heard while the
  blocking screen is shown, which keys work on that screen, whether `SM_SHUTTINGDOWN` is set while it is shown,
  and which window has the keyboard after "Anulare". The diagnostic log records every `POWER` step for this.
