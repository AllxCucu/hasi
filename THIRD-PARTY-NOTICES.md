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
