# ADR 0001 – Language, runtime and UI toolkit

- Status: Accepted (2026-10-21)
- Deciders: project owner (tester), lead developer

## Context

HASI is a self-voicing shell for blind and low-vision Romanian users, many of them elderly and with
no computer experience. It must run on Windows 11 and Windows 10, talk to SAPI5, OneCore and RHVoice,
install and update itself without help, and start fast. Candidates were C#/.NET and Python 3.12+.
The brief named .NET 8, but .NET 8 and .NET 9 reach end of support on 2026-11-10.

## Decision

- **C# on .NET 10 LTS** (supported until November 2028). The SDK is pinned in `global.json`
  (`10.0.402`, `rollForward: latestPatch`).
- Pure libraries (`Hasi.Core`, modules) target `net10.0`. Windows-specific projects target
  `net10.0-windows10.0.19041.0` (gives direct WinRT access for OneCore voices, Windows 10 2004+).
- **WPF** for the single full-screen shell window.
- **xUnit** for tests, without assertion libraries under commercial licenses (no FluentAssertions 8+).
- **Velopack** (MIT) for installer and auto-update (phase 0.11).

## Consequences

- SAPI5 is reached through `System.Speech` (MIT) for phase 0. A raw COM `ISpVoice` binding stays
  possible if interruption latency turns out to be insufficient.
- OneCore needs no extra packages (`Windows.Media.SpeechSynthesis` via the Windows TFM).
- RHVoice can be loaded with P/Invoke later (see ADR 0002).
- Antivirus false positives and slow start, which are typical of frozen Python apps, are avoided.
- Contributors need the .NET 10 SDK; CI uses `actions/setup-dotnet` with `global.json`.

## Alternatives considered

- **Python 3.12+**: proven RHVoice integration via `ctypes`, but packaging (PyInstaller/Nuitka)
  causes frequent antivirus alarms and slow start, and auto-update would be homemade.
- **.NET 8**: out of support three weeks after the decision.
- **WinUI 3**: requires the Windows App SDK runtime; more deployment friction for no benefit here.
- **WinForms**: weaker scaling of very large text.
