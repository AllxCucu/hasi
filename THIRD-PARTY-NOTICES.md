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

### RHVoice 1.18.4
- Author: Olga Yakovleva and contributors
- Version: tag 1.18.4, commit `fa9dd196fd2dac3b0bf089a2d80fc8477c2380e3`. Built by HASI's CI
  (`.github/workflows/rhvoice.yml`), x64 library only; shipped in the installer as `rhvoice\lib\x64\RHVoice.dll`
  with `rhvoice\BUILD-INFO.txt` (commit, SHA-256 of the DLL). The library itself reports "1.19.2": upstream's
  build script (`SConstruct`, `get_version()`) returns that string at every 1.18.x tag.
- License: the DLL combines GPL-2.0-or-later files (e.g. the modified HTS engine), LGPL-2.1-or-later files and
  LGPL-3.0-or-later files (`src/include/core/bpf.h`, `src/hts_engine/utils.h`); as a whole it is distributed
  under **GPL-3.0-or-later**, like HASI. No GPL-2.0-only file (audit in
  `docs/adr/0004-rhvoice-direct-library-audit-and-spike.md`). The license texts ship in `rhvoice\licenses`.
- Corresponding source: https://github.com/RHVoice/RHVoice/tree/fa9dd196fd2dac3b0bf089a2d80fc8477c2380e3 plus
  `.github/workflows/rhvoice.yml` in this repository.
- Used directly through its C API (default engine) and, as a fallback, through SAPI5.

### RHVoice Romanian language data
- Stefan Moisei and contributors
- License: GNU LGPL (LICENSE file, shipped in `rhvoice\paul-langdata`)
- Source: submodule `data/languages/Romanian` of RHVoice at the commit above
  (https://github.com/RHVoice/romanian-bin, sources: https://github.com/RHVoice/romanian-src)

### RHVoice Romanian voice "Paul"
- Copyright © 2026 Stefan Moisei (language pack), Paul Bogorin (speaker),
  Zvonimir Stanecic (voice training); equalization by Tomasz Bilecki.
- License: Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0),
  https://creativecommons.org/licenses/by-sa/4.0/
- Source: submodule `data/voices/paul` of RHVoice at the commit above (https://github.com/RHVoice/paul-ron);
  shipped in `rhvoice\paul-data`.

### .NET runtime and Windows Desktop (WPF)
- Copyright: .NET Foundation and Contributors, Microsoft
- License: MIT
- Source: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf
- Shipped inside the installer (self-contained), so no .NET installation and no administrator prompt is needed.

### Velopack 1.2.161
- Copyright: Velopack Ltd, Caelan Sayler, Kevin Bost and contributors
- License: MIT
- Source: https://github.com/velopack/velopack
- The installer, the uninstaller and the updates (`Update.exe`, the `Velopack` library).
