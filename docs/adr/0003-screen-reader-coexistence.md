# ADR 0003 – Coexistence with NVDA, Narrator and JAWS

- Status: **Superseded** (2026-10-24, owner's decision) – phase 0.9 is removed from Phase 0.
  Only the Narrator detection remains, moved to phase 0.10. The original decision is kept below for history.
- Deciders: project owner (tester), lead developer

## Why it was superseded (2026-10-24)

The owner tested HASI with NVDA running and heard **one voice only, HASI's**. Conditions of the test:
- NVDA restarted, **without** sleep mode active for HASI (no add-on);
- NVDA's **desktop** keyboard layout, using the numeric keypad in HASI;
- key echo while typing in "Test pronunție".

Everything worked: no double speech, and the keypad keys reached HASI. A likely reason, not verified
in NVDA's code: the HASI window has no editable control and does not move the focus between controls,
so NVDA has nothing of its own to announce; and the keys are handled by HASI's window. The spike planned
in this ADR is therefore not needed now.

**Dropped:** the "HASI pentru NVDA" add-on (`integrations/nvda-addon`), speaking through NVDA with the
NVDA Controller Client (never added to the code, to THIRD-PARTY-NOTICES or to "Despre HASI"), UI Automation
notifications for Narrator, and JAWS scripts.

**Kept, moved to phase 0.10:** at startup, if Narrator is running (`Narrator.exe`), HASI says
"Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta
Windows și Enter." Narrator has no per-application sleep mode, so the user has to turn it off.

### Risks still watched

These stay in `docs/architecture.md` §3 so we know what to look for if the problem comes back:

- **R1 – double speech.** A screen reader announces focus changes or echoes keys over HASI. Signs: two
  voices at once, or NVDA repeating HASI's messages. Could appear with another NVDA version, other NVDA
  settings (for example "speak typed characters" in a future editable HASI control), or another screen
  reader (Narrator, JAWS).
- **R2 – keys taken by the screen reader.** In NVDA's desktop layout the numeric keypad drives the review
  cursor, and Insert/CapsLock are NVDA modifiers. Signs: a keypad key does nothing in HASI and NVDA says
  something instead.

If either appears, this ADR is reopened; the original options below (sleep-mode app module, speaking
through NVDA, Narrator notifications) are the starting point.

## Original decision (2026-10-21, superseded)

## Context

HASI speaks with its own synthesizer. When a screen reader runs at the same time, the user hears two
voices: the screen reader announces focus changes and echoes keys over HASI. NVDA's desktop keyboard
layout also captures the numeric keypad and uses Insert/CapsLock as modifiers. Double speech and
stolen keys are the worst failure for our users, because they cannot tell what is happening.

## Decision

1. **Detection**, at startup and every 2 seconds:
   - NVDA: `nvdaController_testIfRunning` from the NVDA Controller Client (LGPL-2.1, dynamically loaded);
   - Narrator: `Narrator.exe` process plus the `SPI_GETSCREENREADER` flag;
   - JAWS: `jfw.exe` process.
2. **NVDA with the "HASI pentru NVDA" add-on (primary solution).** We ship a small NVDA add-on with
   an app module for `hasi.exe` that sets `sleepMode = True`. While HASI is in the foreground, NVDA is
   silent and passes all keys through, including the numeric keypad. It resumes automatically when the
   user switches to another application. HASI keeps its own voice.
   The add-on is Python, lives in `integrations/nvda-addon/`, is GPL-2.0 like NVDA add-ons generally,
   and is distributed as a separate program. It is written from scratch, not copied from NVDA.
3. **NVDA without the add-on.** HASI mutes its own voice and speaks through NVDA
   (`nvdaController_speakText` / `cancelSpeech`): one voice, NVDA's. It then offers, through NVDA,
   to install the add-on. While Windows is locked, HASI sends nothing sensitive through NVDA,
   because NVDA speaks on secure screens.
4. **Narrator.** There is no per-application sleep. HASI says that Narrator is running and how to stop
   it (Ctrl+Windows+Enter). The fallback (speaking through Narrator via UI Automation notification
   events) is decided after the spike.
5. **JAWS.** Phase 0 only detects it and gives a spoken message. JAWS scripts are a later extension.
6. **Quiet UI Automation tree.** The HASI window exposes one element with no redundant content, so a
   screen reader has nothing extra to read.
7. The behavior is configurable in Settings → "Cititor de ecran" (automatic / always own voice /
   always through the screen reader).

## Consequences

- A new `ISpeechEngine` implementation, `ScreenReaderPassthrough`, sits in the same fallback chain as
  the other engines. The core is unchanged.
- An `IScreenReaderMonitor` service (Windows platform project) reports changes into the shell event loop.
- Installing an NVDA add-on requires the user to confirm in NVDA and restart NVDA. HASI explains this
  aloud, step by step.
- The spike (phase 0.9, first task) must confirm on real machines that `sleepMode` passes the numeric
  keypad keys through, and must measure the Narrator behavior. If either fails, we come back to this
  ADR before implementing.
