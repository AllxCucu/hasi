# ADR 0004 – RHVoice.dll directly: license audit and spike

- Status: **Accepted** (2026-10-23) – RHVoice.dll is the default engine, SAPI5 and OneCore are fallbacks. The CI build of RHVoice.dll is implemented before phase 0.11 (installer).
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

**Decision (2026-10-23):** step (1) approved. No definitive switch yet: after the second experiment the owner
listens and decides.

## Second experiment: HASI's own audio output (2026-10-23)

### What was built

- `RhVoiceDllEngine` (`src/Hasi.Speech/RhVoice`), an `ISpeechEngine`, selected with `--engine rhvoice-dll`
  and `--rhvoice-path <folder>`. SAPI stays the default and stays in the fallback chain behind it.
- **Binaries:** the official `RHVoice.dll` 1.18.2 (from `RHVoice-1.18.201.nvda-addon`) and Paul's data, copied
  outside the repository to `%LOCALAPPDATA%\HASI\rhvoice-test`. Nothing binary is in git.
- **Architecture:** HASI is built and runs as **x64** (`PlatformTarget x64`). The add-on contains both an x86
  and an x64 `RHVoice.dll`; HASI reads the PE header and loads only the one matching its own process
  (x64, machine type 0x8664); a mismatch is reported in the log and the next voice is used.
- **Audio library: NAudio 3.1.0** (`NAudio.Core` + `NAudio.Wasapi`, MIT, compatible with GPL-3.0-or-later).
  Chosen because its `WasapiPlayer` supports *default-device stream routing*: Windows itself moves the stream
  when the default device changes or is unplugged, the same mechanism system sounds use. 3.1.0 rather than
  the four-day-old 3.3.0. Alternatives: NAudio 2.x (device changes would have to be handled by hand),
  WinRT `AudioGraph` (no dependency, but rebuilding the graph on device loss is ours too).
- **Stop without clicks:** 8 ms linear fade-out, then the queue is dropped and RHVoice is told to stop
  (the audio callback returns 0).
- **Bluetooth first syllable:** the stream is opened once and plays silence between messages, so the
  device never sleeps; if the stream has to be rebuilt, 150 ms of silence precede the next speech.
- **Device change / resume:** handled by stream routing; if the stream still stops with an error, it is
  rebuilt once, then the fallback chain takes over.
- **Format:** Paul is 24 kHz mono; HASI resamples to the device mix rate (48 kHz here) and duplicates mono to
  stereo (this machine reports an 8-channel mix; without this, mono might come only from the centre).
- **Rate, pitch, volume:** one scale for all engines (`--rate`/`--pitch` -10..10, `--volume` 0..100):
  RHVoice `absolute_rate`/`absolute_pitch` = value/10; volume is a linear gain in HASI's output.
  SAPI has no pitch setting (ignored there); OneCore maps all three.
- **Word positions:** RHVoice reports them in UTF-8 bytes of the message; HASI converts them to characters
  and logs `WORD` lines (nothing is built on them yet).
- **Diagnostic log:** `LATENCY first-sound`, `LATENCY stop`, `WORD`, and `PERF` (CPU and memory after
  each message) for both engines; `RHVOICE` lines describe the DLL, the data and the audio device.
- **Diagnostic → Comparare voci:** five fixed phrases (numbers, diacritics, a long text, a question, date and
  time), each played through Paul/SAPI and Paul/DLL in turn, or only one with the left/right arrows.

### Measurements

`spikes/SpeechBench`, each engine in its own process, same texts, real playback at volume 25, owner's PC
(16 logical cores, default device 48 kHz). Five short phrases, one long reading (≈ 40 s), five stops
1.5 s into speech.

| Measurement | SAPI5 (Paul) | RHVoice.dll (Paul) |
|---|---|---|
| Time to first sound, median (min–max) | 14.1 ms (13.0–14.9) | 12.4 ms (4.7–13.5) |
| Stop, median (min–max) | 33.1 ms (29.4–34.9) | **5.1 ms** (2.0–10.0) |
| CPU during the long reading | 0.35 % of all cores (5.7 % of one core) | 0.35 % (5.5 % of one core) |
| Peak working set / private memory during the reading | 90 MB / 47 MB | **70 MB / 41 MB** |
| Memory after loading the engine | 54 MB / 16 MB | 34 MB / 9 MB |

How to read them: SAPI's "first sound" is its first word event and its "stop" is the cancellation event;
the DLL's are measured where samples are handed to the sound card. Both exclude the device buffer, which
adds up to 40 ms for the DLL output (SAPI's own buffer is not visible). Inside HASI the first message after
start also includes loading the voice (≈ 280–400 ms for the DLL).

Not measured by me (needs the owner's ears and devices): sound quality, clicks, Bluetooth wake-up,
unplugging headphones, resume from sleep.

### CI build of RHVoice.dll – plan only (not implemented)

1. Add RHVoice as a git submodule pinned to a release tag (e.g. 1.18.4) and its commit hash.
2. (Implemented with SCons instead of CMake, see the last follow-up.)
   A GitHub Actions job on `windows-latest` (MSVC available) builds only the library (`RHVoice_core`,
   `libhts_engine`, `RHVoice`) for x64 with CMake, Release, and uploads `RHVoice.dll` as an artifact.
3. Paul's data comes from the pinned `paul-ron` release, checked by SHA-256.
4. The installer (phase 0.11) ships the DLL and data, with the RHVoice license texts, the exact source commit
   and a link to the corresponding source (our submodule), as GPL requires. `THIRD-PARTY-NOTICES.md` and
   "Despre HASI" list RHVoice as shipped.
5. SignPath: our binaries are signed; `RHVoice.dll` is included unsigned (allowed for upstream binaries).

### Recommendation

The direct library is at least as good as SAPI on every measurement: same first-sound time and CPU, a stop
about six times faster, less memory. Its main gain stays the single installer. I recommend **option B**
(direct library by default, SAPI and OneCore as fallbacks) **if the owner's listening tests (R-01…R-08)
find no click, no cut first syllable on Bluetooth and no freeze on device changes**, with the CI build above
done before the installer milestone. If any of those fails, stay on SAPI and fix it first.

**Decision (2026-10-23, owner):** switch. RHVoice.dll is the default engine, with SAPI5 and OneCore as fallbacks.
The CI build from a pinned commit (plan above) is implemented before phase 0.11, not now. Until then HASI looks
for the DLL and Paul's data in `<HASI folder>\rhvoice` and `%LOCALAPPDATA%\HASI\rhvoice` (or `--rhvoice-path`).

2026-10-24: The default search now checks `<exe>\rhvoice` first, then only in Debug builds
`%LOCALAPPDATA%\HASI.Dev\rhvoice`, outside the folder removed by uninstall (ADR 0007, decision 11).
`%LOCALAPPDATA%\HASI\rhvoice` is no longer searched; `--rhvoice-path` still overrides the defaults.

## Follow-up (2026-10-23): idle stream and voice preloading

**Problem raised by the owner:** a stream kept open forever (playing silence) can stop the computer from going to
sleep and drains laptop and Bluetooth headphone batteries. Windows counts an active render stream as an audio
power request (`powercfg /requests` lists it under `SYSTEM`/`EXECUTION` as an audio stream in use).

**Change:** the output closes the stream after a configurable time without speech, **one minute by default**
(`--audio-idle-seconds N`, `0` = always open; in the settings file from phase 0.6). The next message re-opens it
with 150 ms of silence in front, so the first syllable is still not cut. Closing never happens while speech is
queued or playing. Verified on the owner's PC with `--audio-idle-seconds 3`: both streams (main voice and the
comparison voice) closed 3 s after their last message (`AUDIO … stream closed` in the diagnostic log).
Not verified by me: `powercfg /requests` needs an elevated prompt, and re-opening needs a key press; both are in
the owner's test scenarios (R-13, R-14).

**Trade-off:** after a minute of silence the next message costs the re-open (tens of milliseconds) plus the
150 ms of silence. Bluetooth headphones that have gone to sleep in the meantime may still need longer to wake
than 150 ms; if R-14 shows a cut syllable, the silence can be made longer for Bluetooth devices only.

**Preloading:** the voice is loaded on a background task as soon as the engine is created (a word is
synthesized and discarded), and the synthesis thread waits for it before the first real message. Measured on
the owner's PC: preload 275 ms, done while the window is being built; the welcome message's first sound
went from 280–400 ms to **16 ms**.

## Follow-up (2026-10-23): CI build implemented (phase 0.11, step 1)

`.github/workflows/rhvoice.yml` builds `RHVoice.dll` for x64 from upstream commit
`fa9dd196fd2dac3b0bf089a2d80fc8477c2380e3` (tag 1.18.4). Findings, all checked:

- **Pin.** 1.18.2 is the last upstream release with Windows binaries (`RHVoice-1.18.201.nvda-addon`, the DLL the
  owner uses); 1.18.3 and 1.18.4 are Android releases. Between 1.18.2 and 1.18.4 nothing changed in `src/core`,
  `src/lib` or `src/hts_engine`. Romanian and Paul joined upstream after 1.18.2 (`New language: Romanian`), as
  submodules; at 1.18.4 their files are byte for byte the ones the owner installed by hand. So the voice and the
  language data come from the same commit, with no separate download.
- **Build.** Upstream's own Windows CI uses SCons, and so do we. The repository also has a `CMakeLists.txt`, but
  we follow the path upstream proves. SCons reads upstream's installer scripts even for one target. With WiX on the runner they
  stop unless `sapi_dev_core=true`, which only changes upstream's SAPI installers. The target
  `build/windows/x86_64/lib/RHVoice.dll` builds **only** the x64 library (no x86 objects): about 70 s.
  Only the needed submodules are fetched (Boost, Romanian, Paul), not the other 100 or so. `core.autocrlf` is off, so
  the data files keep upstream's bytes.
- **Check.** `tools/RhVoiceCheck` loads the built DLL, requires x64 and the voice Paul, synthesizes the five
  "Comparare voci" phrases and writes them as WAV. Compared with the owner's 1.18.2: **every phrase is
  bit-identical** (same samples). HASI started with `--rhvoice-path` to the CI build speaks normally.
- **Version string.** At every 1.18.x tag the SCons script reports version `1.19.2` (`get_version`), so our build
  says "1.19.2" in the log; the official 1.18.2 add-on says "1.18.2". `BUILD-INFO.txt` records the tag, the
  commit, the reported version and the SHA-256 of the DLL.
- **Licence, correction to the audit above.** Some library files are **LGPL-3.0-or-later**:
  - `src/include/core/bpf.h`, used by `src/core/bpf.cpp`;
  - `src/hts_engine/utils.h`, used by `HTS_misc.c`.

  Together with the GPL-2.0-or-later and LGPL-2.1-or-later files, the DLL as a whole can be distributed under
  **GPL-3.0-or-later**, still compatible with HASI. When HASI starts shipping the DLL (phase 0.11, step 2), three
  places must say so, in the same commit:
  - "Despre HASI";
  - `THIRD-PARTY-NOTICES.md`;
  - the comment in `RhVoiceLibrary.cs`.

  The artifact ships GPL-2.0, GPL-3.0, LGPL-2.1, LGPL-3.0 and CC BY-SA 4.0 texts.
