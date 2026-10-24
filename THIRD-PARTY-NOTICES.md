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

### Open XML SDK (DocumentFormat.OpenXml, DocumentFormat.OpenXml.Framework 3.5.1)
- Copyright: .NET Foundation and Contributors (the package metadata says "© Microsoft Corporation")
- License: MIT
- Source: https://github.com/dotnet/Open-XML-SDK
- Reading and writing Word documents (.docx), in `Hasi.Documents`.

### System.IO.Packaging 10.0.2
- Copyright: .NET Foundation and Contributors
- License: MIT
- Source: https://github.com/dotnet/runtime
- The zip package layer under the Open XML SDK (a dependency of DocumentFormat.OpenXml.Framework). In the
  application the copy from the .NET Windows Desktop runtime above is used; the NuGet package only builds
  `Hasi.Documents` and its tests.

### PdfPig 0.1.16 (UglyToad.PdfPig and its parts: Core, Tokens, Tokenization, Fonts, DocumentLayoutAnalysis, Package)
- Copyright: Eliot Jones and contributors (UglyToad)
- License: Apache-2.0
- Source: https://github.com/UglyToad/PdfPig, tag `v0.1.16`, commit `a7bb35662bbbf405efddad50aedc9bcdcf515afc`
  (the commit the NuGet package names)
- Reading PDF files (text, pages, reading order), in `Hasi.Documents`. One NuGet package, no further dependencies
  on .NET 8 or later.
- Contains code derived from Apache PDFBox and FontBox (BSD-3-Clause, www.pdfbox.org), the Adobe Glyph List and
  Zapf Dingbats Glyph List, the Adobe Font Metrics of the 14 standard PDF fonts (their own permissive notice, which
  must travel with them) and Adobe CMaps (BSD-3-Clause). Shipped in `licente\` next to `hasi.exe`:
  `PdfPig-LICENSE.txt` (Apache-2.0 plus the licences of these components), `PdfPig-NOTICES.txt` (the NOTICE file)
  and `Adobe-AFM-MustRead.html` (the AFM notice), taken unmodified from the tag above.

### VersOne.Epub 3.3.6
- Author: vers (vers-one)
- License: Unlicense (public domain dedication)
- Source: https://github.com/vers-one/EpubReader, tag `v3.3.6`
- Reading EPUB books (reading order, table of contents), in `Hasi.Documents`. No dependencies on .NET Standard 2.0.
  Licence text shipped as `licente\VersOne.Epub-LICENSE.txt`.

### Html Agility Pack (HtmlAgilityPack 1.13.1)
- Copyright: ZZZ Projects Inc., Simon Mourrier, Jeff Klawiter, Stephan Grell and contributors
- License: MIT
- Source: https://github.com/zzzprojects/html-agility-pack, tag `v1.13.1`
- Extracting the text of the chapters of EPUB books, in `Hasi.Documents`. No dependencies on .NET 8.
  Licence text shipped as `licente\HtmlAgilityPack-LICENSE.txt`.
