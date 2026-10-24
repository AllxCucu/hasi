# Data sources

Every online source HASI uses: what it is, its terms, its limits, and what happens if it disappears.

## GitHub Releases of HASI (updates, phase 0.11)

- What: the release feed of `https://github.com/AllxCucu/hasi`, read by Velopack (`GithubSource`) to find a newer
  version and download its package.
- When: once at every start (if "Verificare automată la pornire" is on) and when the user chooses
  "Caută actualizări acum". Nothing about the user is sent; GitHub sees the request like any web request.
- Terms: GitHub's public API and release downloads, under GitHub's Terms of Service; HASI's releases are our own
  content (GPL-3.0-or-later).
- Limits: unauthenticated API requests are limited to 60 per hour per IP address; one check per start stays far
  below. If the limit is hit, the check fails and HASI says nothing at startup.
- Fallback if it disappears or the project moves: the source is configuration, not code (`UpdateSource` in
  `settings.json`: `github:<other repository>`, `http:<URL>` of a static folder, or `folder:<path>`).

## Romanian legal holidays (`sarbatori-legale.txt`, Phase 1 module 1)

- What: the rules of `src/Modules/Hasi.Modules.Clock/Data/sarbatori-legale.txt` (fixed days and days counted from
  Orthodox Easter), taken from the Codul muncii (Legea nr. 53/2003, republished), art. 139 alin. (1), consolidated
  form of 27 April 2026 on legislatie.just.ro (`DetaliiDocument/128647`), with the amending laws named per line
  (Legea 220/2016, Legea 64/2018, Legea 52/2023). Alin. (1) was rewritten by Legea 176/2016 (in force 16 October
  2016), so every rule is applied from 2017 only; HASI says nothing about earlier years. The law gives no date for
  Adormirea Maicii Domnului and Crăciun: the Romanian Orthodox Church's dates (15 August, 25–26 December) are used.
  Orthodox Easter is computed (Meeus' Julian algorithm + 13 days, valid 1900–2099), not downloaded.
- Cross-check: the 2026 list matches the Romanian Patriarchate's "Zile și sărbători legale în care nu se lucrează"
  (calendar.patriarhia.ro); the Easter dates 2024–2030 match the published ones (tests in
  `tests/Hasi.Transcripts.Tests/LegalHolidaysTests.cs`).
- When: offline; nothing is fetched.
- Terms: official legislative texts are not protected by copyright in Romania (Legea 8/1996, art. 9 lit. b). This is
  the lead's reading, to be confirmed by the owner.
- Limits: none.
- If the law changes: edit the file (and add the spoken name to `ro.json` for a new holiday); it ships with the next
  update. Holidays of non-Christian cults (art. 139, last item) and the HG with extra days for the public sector
  (alin. 4) are not computed; the help says so for the cults.
