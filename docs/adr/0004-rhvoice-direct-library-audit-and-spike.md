# ADR 0004 – RHVoice.dll directly: license audit and spike

- Status: **Proposed** – waiting for the project owner's decision (do not switch from SAPI before it)
- Date: 2026-10-23
- Follows: ADR 0002 (RHVoice through SAPI in phase 0)

## Context

ADR 0002 kept RHVoice "Paul" behind SAPI5 until two questions were answered:
1. Can a GPL-3.0-or-later program legally load and ship `RHVoice.dll`?
2. Does the direct C API give HASI something SAPI does not?

## License audit

Source audited: `github.com/RHVoice/RHVoice`, commit `1f0c575522a75ccd60adfee5a55dd4387b5eb4c8` (2026-10-22),
plus the Romanian data repositories. The header of every C/C++ file was classified.

| What | License found | Built into RHVoice.dll? |
|---|---|---|
| `src/lib/lib.cpp` (the C API entry points) | LGPL-2.1-or-later | yes |
| `src/core` (42 files) | LGPL-2.1-or-later | yes (`RHVoice_core`) |
| `src/core/hts_vocoder_wrapper.cpp`, `model_answer_cache.cpp`, `str_hts_engine_impl.cpp` (+ 4 headers in `src/include/core`) | **GPL-2.0-or-later** | yes |
| `src/hts_engine` (modified HTS engine, 11 of 12 files) | **GPL-2.0-or-later** | yes (`libhts_engine`, linked by `RHVoice_core`) |
| `src/third-party` (rapidxml, utf8, sonic, tclap) | MIT/BSD-like | partly (headers) |
| `src/audio` (PortAudio/libao/Pulse playback) | GPL-2.0-or-later | no – HASI plays audio itself |
| `src/sapi` (the SAPI server) | LGPL-2.1-or-later | no |
| `LICENSE.md` at the repository root | GPL-2.0 text | – |
| Romanian language data (`RHVoice/romanian-bin`, `romanian-src`) | LGPL (LICENSE file) | data, loaded at run time |
| Voice "Paul" (`RHVoice/paul-ron`) | CC BY-SA 4.0 | data, loaded at run time |

No file was found under **GPL-2.0-only**. The library as distributed is therefore, as a whole,
**GPL-2.0-or-later** (LGPL parts combined with GPL-2.0-or-later parts), not LGPL.

Consequences for HASI (GPL-3.0-or-later):
- **Compatible.** GPL-2.0-or-later code may be used under GPL-3.0, so HASI can load and ship `RHVoice.dll`.
- **Obligations when shipping it:** provide the Corresponding Source of the exact RHVoice build
  (source, build scripts, version) and keep all license notices; attribute Paul (CC BY-SA 4.0) and the
  Romanian data (LGPL). Linking a stock upstream binary and pointing to the upstream commit is legally
  possible, but building it ourselves in CI from a pinned commit is the clean way to guarantee that the
  source matches the binary.
- **SignPath:** upstream binaries may be included unsigned in a signed package; the official
  `RHVoice.dll` is not signed (checked: `NotSigned`).

## Spike

Code: `spikes/RhvoiceDirect` (outside the solution and CI). Binaries: official `RHVoice-1.18.201.nvda-addon`
(engine 1.18.2, `lib/x64/RHVoice.dll`, 2.0 MB) and `RHVoice-voice-Romanian-Paul-4.2.1001.12.nvda-addon`
(voice + language data, 16.0 MB), downloaded to a temporary folder, not committed.
Machine: the project owner's Windows 11 PC. Phrase: "Meniu principal. Patru opțiuni. Selectat: Setări, unu din patru."
(4.5 s of audio).

| Measurement | RHVoice.dll direct | SAPI5 (System.Speech, to memory) |
|---|---|---|
| Engine load + first synthesis | 291 ms | (part of first run: 410 ms) |
| Time to first audio | **4 ms** | not measurable (no per-chunk callback) |
| Total synthesis, warm | 131–134 ms | 130–135 ms |
| Real-time factor | 0.03 | 0.03 |
| Stop after the callback says "stop" | **< 1 ms** (returning 0 from `play_speech`) | – (tester heard SAPI stops as "instant") |

The C API works from .NET with plain P/Invoke and function pointers; voices and profiles are listed
("Paul (ro)"); audio comes in small chunks, so playback can start almost immediately.

## What switching would bring – and cost

Gains:
1. **One installer for the end user.** No separate Paul/SAPI installer, no 32/64-bit registration
   questions; HASI ships the voice (≈ 18 MB) and works out of the box. For our users this is the biggest gain.
2. **Full control of audio:** our own queue, immediate stop, volume and pitch (`absolute_pitch`), and
   word/sentence callbacks (useful later for reading documents word by word).
3. **Independent of the SAPI installation state** (repairs, uninstalls, other programs).

Costs:
1. HASI must play audio itself (a small WASAPI/waveOut player with a queue) – new code to test by ear.
2. GPL obligations for the shipped binary (source correspondence; ideally build RHVoice in CI with MSVC).
3. Updates of the engine and voice become our job.
4. Speed: no gain measured; SAPI is already fast enough.

## Options

- **A. Stay on SAPI** (today). Zero new code; the user installs Paul separately.
- **B. Direct library as the default, SAPI and OneCore as fallbacks.** Needs the audio player and the
  GPL/CI work above. Removes the separate voice install.
- **C. Hybrid:** use SAPI Paul if it is installed, otherwise the bundled direct library.

## Recommendation (for the owner to decide)

**B, implemented in two steps:** (1) a second spike in phase 0.5 or 0.6 with real-time playback through our
own audio player, so the tester can judge stop latency and sound by ear against SAPI; (2) if the tester
prefers it, the switch plus the CI build of RHVoice before the installer milestone (0.11), where the
single-installer gain matters. Until then HASI stays on SAPI (option A).

**Decision:** pending – the owner decides whether to proceed with step (1).
