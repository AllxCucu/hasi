# ADR 0003 – Coexistence with NVDA, Narrator and JAWS

- Status: Accepted (2026-10-21); implementation in phase 0.9, preceded by a spike
- Deciders: project owner (tester), lead developer

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
