# ADR 0002 – RHVoice "Paul" through SAPI5 in phase 0; direct library only after a license audit

- Status: Superseded by ADR 0004 (2026-10-23): RHVoice.dll is now the default; SAPI5 remains a fallback
- Deciders: project owner (tester), lead developer

## Context

RHVoice with the Romanian voice "Paul" is the intended default voice. Two integration paths exist:

1. **SAPI5**: the official installer `RHVoice-voice-Romanian-Paul-v4.2.1001.23-setup.exe`
   (github.com/RHVoice/paul-ron) registers Paul as a SAPI5 voice. HASI talks to it like any SAPI voice.
2. **Direct library**: load `RHVoice.dll` with P/Invoke (`RHVoice_new_tts_engine`, `RHVoice_speak`, …)
   and play the audio through our own output. This gives instant stop, word positions, a per-user
   install without admin rights, and a bundled voice.

Licensing facts verified on 2026-10-21:

- `src/include/RHVoice.h` carries **LGPL-2.1-or-later**.
- The repository root `LICENSE.md` contains the **GPL-2.0** text. Distributions label the package
  inconsistently (LGPL-2.1+, GPL-2.0, GPL-3.0+). The licensing is therefore per file and mixed.
- Voice Paul: **CC BY-SA 4.0**, © 2026 Stefan Moisei, Zvonimir Stanecic, Paul Bogorin;
  equalization by Tomasz Bilecki. Romanian language data: LGPL-2.1 (per Arch packaging).

HASI is GPL-3.0-or-later. GPL-2.0-only code linked into our process would be incompatible with it.

## Decision

- **Phase 0 uses path 1 (SAPI5).** RHVoice stays a separate, unmodified program, installed by its own
  installer. HASI does not ship, link or modify it, so no linking question arises.
- HASI selects the voice whose name contains "Paul". If it is missing, HASI falls back to another
  Romanian voice, then to the system default voice, and says so aloud.
- **Path 2 is a later, separate step** (phase 0.5 spike). Before any `RHVoice.dll` is shipped or
  loaded in-process:
  1. audit every source file compiled into `RHVoice.dll` (core, HTS engine, language and voice data)
     and record each license;
  2. confirm that no GPL-2.0-only file is included;
  3. document how we meet LGPL obligations (dynamic linking, source offer or link, replaceable DLL);
  4. record the outcome in a new ADR that supersedes this one.
- If the audit finds GPL-2.0-only code in the DLL, HASI stays on SAPI5 for RHVoice.

## Consequences

- The user must install Paul separately in phase 0. The SAPI installer may require administrator
  rights (to be confirmed by the tester).
- Interruption goes through SAPI, which is slower than a direct engine. Stop latency is logged
  in diagnostic mode so we can measure it.
- A 64-bit HASI only sees voices registered for 64-bit SAPI. If Paul shows up only for 32-bit
  programs, HASI falls back and announces it. The tester reports this, and we revisit.
- Attribution for RHVoice and Paul is shown in "Despre HASI" and `THIRD-PARTY-NOTICES.md`.
