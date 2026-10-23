# CLAUDE.md – HASI (Human Assisted Information)

Approved architecture: `docs/architecture.md` (Romanian). Decisions: `docs/adr/`.

HASI is a self-voicing, menu-driven shell for blind and low-vision users in Romania, inspired by
the *concept* of WinGuido/GuidoAusili. It is **not** a screen reader. Target users: elderly people
who recently lost their sight, people with no computer experience, people with multiple disabilities.
The voice output is the source of truth; visuals are secondary.

## Non-negotiables

- **Do not copy code** from WinGuido/GuidoAusili (proprietary) or from NVDA. Reuse concepts only.
- **No code before the user approves the architecture** for a new phase or major component.
- **Never send keystrokes or simulated input to the real desktop** (SendInput, SendKeys, keybd_event,
  UI Automation, focus-stealing tricks) without asking the user for approval **for each run**.
  Automated tests use `ShellHarness` only; ear-testing is done by the user.
- **Ask before changing the plan.** Moving anything from one milestone to another, or changing scope,
  needs the user's approval first, not a notice afterwards.
- When unsure about a data source or its license: **verify and report**, never assume.
- No telemetry. User data stays local. Passwords only in Windows Credential Manager.

## Languages

- All **UI text in Romanian**, only in `src/Hasi.App/Resources/ro.json` (i18n-ready). Never hardcode
  user-facing strings in code. Use correct diacritics: ș ț (comma below, U+0219/U+021B), never ş ţ.
- **Code, comments, commit messages, ADRs: English.**
- Conversation with the user, manual test scenarios and user docs: Romanian.

## Tech stack

- C# / .NET 10 LTS, SDK pinned in `global.json` (`net10.0` for pure libraries,
  `net10.0-windows10.0.19041.0` for Windows parts).
- WPF for the shell window. xUnit for tests (no commercially licensed assertion libraries).
  Velopack for installer and auto-update; the update source is configuration, never hardcoded.
- Speech: `ISpeechEngine` implementations: RHVoice (voice "Paul", default), SAPI5, OneCore,
  Null. No screen-reader integration: phase 0.9 was removed (ADR 0003 superseded) because HASI works with
  NVDA with a single voice; only Narrator detection remains, in phase 0.10.

## Architecture rules

- `Hasi.Core` is pure .NET: no WPF, no Win32, no concrete modules. Enforced by `Hasi.Architecture.Tests`.
- Modules (`src/Modules/Hasi.Modules.*`) depend only on `Hasi.Core` and never on each other.
  A module exposes an `IModule` with a root `IScreen`. Only `Hasi.App` wires everything up.
- Screens never touch the window or the speech engine. They return a `ScreenResult`
  (`Say`, `Push`, `Pop`, `Replace`, `Confirm`, `Unhandled`); the `Shell` applies it.
- All output goes through `Announcer` → `ITextNormalizer` → `OutputHub` → `IOutputChannel`.
  Speech, visual and diagnostic are channels. Braille will be a future channel: **do not write any
  Braille code** until the user asks for it.
- The shell runs as a single-threaded event loop; timers post events into it.
- Network modules must degrade gracefully offline ("Nu există conexiune la internet.").

## UX rules (mandatory, test them)

1. Every screen announces on entry: where you are, how many options, which one is selected.
2. Same keys, same meaning everywhere. Escape = back, always. F1 = contextual help (spoken).
3. F2 repeats the last message; F3 says "where am I" (menu path). Ctrl stops speech; any key interrupts.
4. Every destructive action (delete, send, shut down) needs a spoken confirmation.
4a. Every global command also has a binding without F keys (laptops need Fn for F keys and often
    have no numeric keypad). All bindings live in `KeyMap` and are configurable:

    | Command    | Primary        | Laptop alternative | Numpad |
    |------------|----------------|--------------------|--------|
    | Help       | F1             | Ctrl+H             | /      |
    | RepeatLast | F2             | Ctrl+R             | *      |
    | WhereAmI   | F3             | Ctrl+U             | -      |
    | First/Last | Home/End       | Ctrl+Up/Ctrl+Down  | 7 / 1  |
    | Next/Prev  | Down/Up        | –                  | 2 / 8  |
    | Activate   | Enter          | –                  | 5      |
    | Back       | Escape         | –                  | 0      |
    | Diagnostic | Ctrl+Shift+F12 | Ctrl+Shift+D       | –      |
    | StopSpeech | Ctrl (alone)   | –                  | –      |
    | Bring HASI to front (global) | Ctrl+Alt+H | –     | –      |
4b. Lists do not wrap by default: an earcon plus "Sfârșitul listei"/"Începutul listei".
    Wrapping is a user setting (`ShellOptions.WrapLists`).
5. No unannounced pop-ups. Errors are spoken in plain Romanian, never with technical codes.
6. Key echo is configurable: character / word / none.
7. Every feature must be usable by someone who has never used a computer.

## Testing

- Every screen gets `ShellHarness` tests: simulated keys → asserted spoken text.
- Spoken-text golden transcripts live next to the tests. Wording changes must show up in the diff.
- Romanian normalization: table-driven tests. Every mispronunciation the user reports becomes a test case.
- At the end of each feature, give the user a short Romanian list of manual scenarios
  ("ce apăs / ce trebuie să aud") and add it to `docs/manual-tests/`.
- **The user's feedback about what is heard or what is confusing is a bug**, handled like an exception:
  reproduce it (ideally from their diagnostic log), add a failing test, fix it.
- Diagnostic mode logs every key, command, screen and utterance. Each utterance is logged with the
  original `text`, the `normalized` text, and the exact string sent to the engine (`SPEAK`), so a
  normalizer error can be told apart from a voice error. Utterances marked `Sensitive` are masked.
- Hard stop after each milestone that the user marks for ear-testing (currently after 0.3):
  deliver a runnable build plus scenarios, and wait for the user's report.

## Commits

- Small, focused commits, Conventional Commits style (`feat(core): ...`, `fix(speech): ...`).
- Build and all tests must pass before committing.

## Licensing

- Project license: **GPL-3.0-or-later**.
- RHVoice: dynamic linking/separate process only (LGPL-2.1+; the repo root also contains GPL-2.0 text,
  so audit any file built into a DLL we ship). Voice "Paul": CC BY-SA 4.0. Attribution:
  Stefan Moisei, Paul Bogorin, Zvonimir Stanecic; equalization: Tomasz Bilecki.
- Every third-party component must be added to `THIRD-PARTY-NOTICES.md` **and** the spoken
  "Despre HASI" screen in the same commit.

## Data sources

- Only official APIs, open data or public RSS feeds. No scraping where the terms forbid it.
- Record each source in `docs/data-sources.md`: license, usage limits, fallback plan if it disappears.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/Hasi.App                       # add: -- --diagnostic --voice "Paul" --rate 0
                                                        #      --engine sapi5|onecore|rhvoice-dll
                                                        #      --rhvoice-path <folder> --pitch 0 --volume 100
                                                        #      --audio-idle-seconds 60 --test-voice-failure
```

- User settings: `%APPDATA%\HASI\settings.json` (`UserSettings`, schema version 1, migrations in `SettingsFile`).
  Command-line options override them for the session and are saved only when changed in Settings.
- Default engine: RHVoice.dll from `<exe>\rhvoice` or `%LOCALAPPDATA%\HASI\rhvoice` (ADR 0004); the CI build of
  RHVoice.dll must be done before phase 0.11.

- RHVoice.dll binaries (until the CI build exists) live outside the repository, in
  `%LOCALAPPDATA%\HASI\rhvoice` on the owner's PC. Never commit them.
- Git author for this repository: the GitHub noreply address (local config). Never rewrite published
  history without the owner's explicit approval.

- Normalizer cases: `tests/Hasi.Text.Ro.Tests/Cases/*.cases` (`written => spoken`). Data files are edited in
  `src/Hasi.Text.Ro/Data/` and copied next to the executable on build (edits in `bin` are overwritten).
- `spikes/` holds throw-away experiments (not in the solution, not in CI). Third-party binaries used by a
  spike are downloaded to a temp folder and never committed.
- Scheduled: the CI build of RHVoice.dll from a pinned commit (ADR 0004), before phase 0.11 – not earlier.
- Editable data: `src/Hasi.Text.Ro/Data/acronime.txt`, `genuri.txt` (tester's noun genders, first);
  `substantive.txt` is generated by `tools/GenderTable` from MULTEXT-East (CC BY-SA 4.0) – do not edit it.

- Diagnostic transcripts: `%LOCALAPPDATA%\HASI\diagnostic\sesiune-*.log`; error log: `%LOCALAPPDATA%\HASI\logs\errors.log`.
- Golden transcripts: `tests/Hasi.Transcripts.Tests/Transcripts/*.transcript`
  (`> key`, `< expected text`, `[sunet]` = earcon, `! exit`). The tester may edit them;
  a failing transcript then means `ro.json` must follow the tester's wording.
- When launching HASI from an agent/background process, Windows does not give it the keyboard.
  Sending keystrokes to it requires the user's approval for that run (see Non-negotiables).
