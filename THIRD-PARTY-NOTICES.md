# Third-party notices

HASI is licensed under GPL-3.0-or-later. It uses or works with the components below.
Every entry here must also appear in the spoken "Despre HASI" screen.

## Shipped with HASI

### System.Speech
- Copyright: .NET Foundation and Contributors
- License: MIT
- Source: https://github.com/dotnet/runtime

## Used by HASI but not shipped (installed separately by the user)

### RHVoice
- Author: Olga Yakovleva and contributors
- License: the C API (`RHVoice.h`) is LGPL-2.1-or-later; the repository also contains GPL-2.0
  text, so licensing is per file (see `docs/adr/0002-rhvoice-through-sapi-in-phase-0.md`).
- Used through the SAPI5 interface only, as a separate, unmodified program.
- Source: https://github.com/RHVoice/RHVoice

### RHVoice Romanian voice "Paul"
- Copyright © 2026 Stefan Moisei (language pack), Paul Bogorin (speaker),
  Zvonimir Stanecic (voice training); equalization by Tomasz Bilecki.
- License: Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0),
  https://creativecommons.org/licenses/by-sa/4.0/
- Source: https://github.com/RHVoice/paul-ron
