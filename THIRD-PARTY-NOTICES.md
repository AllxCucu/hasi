# Third-party notices

HASI is licensed under GPL-3.0-or-later. It uses or works with the components below.
Every entry here must also appear in the spoken "Despre HASI" screen.

## Shipped with HASI

### System.Speech
- Copyright: .NET Foundation and Contributors
- License: MIT
- Source: https://github.com/dotnet/runtime

### NAudio (NAudio.Core, NAudio.Wasapi 3.1.0)
- Copyright: Mark Heath and contributors
- License: MIT
- Source: https://github.com/naudio/NAudio
- Used for HASI's own audio output (RHVoice.dll experiment, ADR 0004).

### MULTEXT-East free lexicons 4.0 – Romanian lexicon (noun genders)
- Erjavec, T., Bruda, Ş., Derzhanski, I., Dimitrova, L., Garabík, R., Holozan, P., Ide, N., Kaalep, H.-J.,
  Kotsyba, N., Oravecz, C., Petkevič, V., Priest-Dorman, G., Shevchenko, I., Simov, K., Sinapova, L.,
  Steenwijk, H., Tihanyi, L., Tufiş, D., Véronis, J. (2010). Jožef Stefan Institute.
  http://hdl.handle.net/11356/1041
- License: Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)
- Shipped as `Data/substantive.txt`, adapted by `tools/GenderTable`: common nouns only, form → agreement
  gender, cedilla letters converted to comma-below, ambiguous forms and function words removed.
  The adapted table is distributed under CC BY-SA 4.0.

## Used by HASI but not shipped yet (the installer, phase 0.11, will ship them)

### RHVoice
- Author: Olga Yakovleva and contributors
- License: `RHVoice.dll` as a whole is GPL-2.0-or-later (LGPL-2.1-or-later files combined with
  GPL-2.0-or-later files, e.g. the modified HTS engine); no GPL-2.0-only file (audit in
  `docs/adr/0004-rhvoice-direct-library-audit-and-spike.md`). Compatible with HASI's GPL-3.0-or-later.
- Used directly through its C API (default engine) and, as a fallback, through SAPI5.
- Source: https://github.com/RHVoice/RHVoice

### RHVoice Romanian language data
- Stefan Moisei and contributors
- License: GNU LGPL (LICENSE file in the repository)
- Source: https://github.com/RHVoice/romanian-bin, https://github.com/RHVoice/romanian-src

### RHVoice Romanian voice "Paul"
- Copyright © 2026 Stefan Moisei (language pack), Paul Bogorin (speaker),
  Zvonimir Stanecic (voice training); equalization by Tomasz Bilecki.
- License: Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0),
  https://creativecommons.org/licenses/by-sa/4.0/
- Source: https://github.com/RHVoice/paul-ron
