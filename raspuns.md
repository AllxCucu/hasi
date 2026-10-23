# HASI – 0.11, pasul 1: RHVoice.dll construit în CI

Pasul 1 e gata. M-am oprit aici, la oprirea prevăzută, și aștept ascultarea ta comparativă. Programul HASI nu s-a schimbat.

## Ce s-a făcut

- **Workflow-ul nou** `.github/workflows/rhvoice.yml` construiește RHVoice.dll x64 din sursa oficială RHVoice, la commitul exact al tag-ului 1.18.4 (`fa9dd196…`):
  - folosește SCons, cum ai aprobat;
  - aduce doar submodulele necesare: Boost, datele românești și Paul, din cele peste 100 ale depozitului;
  - construiește **doar biblioteca x64**, fără nimic pentru x86, în circa 70 de secunde;
  - [rularea reușită](https://github.com/AllxCucu/hasi/actions/runs/40018458844).
- **Ce rezultă:** un artifact cu DLL-ul, Paul, datele românești, textele de licență și un `BUILD-INFO.txt` (commitul, SHA-256 al DLL-ului, versiunea). Artifactul nu se publică nicăieri.
- **Utilitarul de verificare** `tools/RhVoiceCheck` nu face parte din program. Verifică automat, în CI, că DLL-ul e x64, că are vocea Paul și că rostește cele 5 fraze din „Comparare voci”, pe care le salvează ca WAV.

## Ce am găsit

1. **Sunetul e identic bit cu bit.** Am comparat cele 5 fraze rostite de 1.18.2 (al tău) cu cele din build-ul din CI: aceleași eșantioane, la toate cinci.
2. **De ce e la fel:**
   - 1.18.2 e ultima versiune RHVoice publicată pentru Windows (add-on-ul NVDA pe care îl folosești); 1.18.3 și 1.18.4 au fost doar pentru Android;
   - între 1.18.2 și 1.18.4 codul bibliotecii nu s-a schimbat deloc;
   - Paul și româna au intrat în depozitul RHVoice după 1.18.2, iar la 1.18.4 fișierele lor sunt identice cu ce ai instalat de mână.
3. **Versiunea afișată e „1.19.2”.** Scriptul RHVoice declară „1.19.2” la toate tag-urile 1.18.x, așa că build-ul nostru apare în jurnal ca 1.19.2. E doar eticheta: sursa e 1.18.4.
4. **Două piedici în SCons, rezolvate:**
   - SCons citește și scripturile installer-ului RHVoice, chiar dacă cerem doar DLL-ul, și se oprea cu o eroare. Opțiunea `sapi_dev_core=true` o evită: schimbă doar installer-ele lor SAPI, nu biblioteca.
   - Conversia capetelor de rând (CRLF) e oprită în CI, ca fișierele de date să rămână identice cu ale RHVoice.
5. **Corectură de licență, important.** Câteva fișiere din bibliotecă sunt sub **LGPL 3.0 sau ulterioară**, iar auditul meu din ADR 0004 nu le prinsese. DLL-ul se poate distribui în întregime sub GPL-3.0-or-later, deci rămâne compatibil cu HASI. Textul din „Despre HASI” și din `THIRD-PARTY-NOTICES.md` trebuie însă corectat, odată cu includerea DLL-ului în installer (pasul 2). Nu l-am schimbat acum, pentru că pasul 1 nu atinge programul.
6. **Proba de pornire** (fără taste, 8 secunde), cu HASI pornit pe build-ul din CI: HASI îl încarcă, spune bun venit cu RHVoice direct, nicio eroare.

## Ascultarea comparativă (W-01…W-04)

Ți-am pregătit totul pe calculator; folderul tău `rhvoice` a rămas neatins:
- build-ul din CI: `%LOCALAPPDATA%\HASI\rhvoice-ci-1.18.4`;
- frazele A/B: `%LOCALAPPDATA%\HASI\comparatie-rhvoice`.

Cele două variante de pornire:
- **A (1.18.2, ca până acum):** `dotnet run --project src\Hasi.App -- --diagnostic`
- **B (din CI):** `dotnet run --project src\Hasi.App -- --diagnostic --rhvoice-path "%LOCALAPPDATA%\HASI\rhvoice-ci-1.18.4"`

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| W-01 | În `comparatie-rhvoice` asculți pe rând `fraza-1-A-actual-1.18.2.wav` și `fraza-1-B-din-CI.wav`, apoi frazele 2…5 | fiecare pereche sună la fel: aceeași voce, viteză, intonație, aceleași pauze |
| W-02 | Pornești B și cauți în jurnal rândul `RHVOICE version` | `RHVOICE version 1.19.2, voices Paul` și `dll=…rhvoice-ci-1.18.4\lib\x64\RHVoice.dll` |
| W-03 | Cu B: pașii 1–23 din `regresie-faza-0.md` | totul ca în A: bun venit fără întârziere, oprire imediată a vorbirii, nicio pocnitură |
| W-04 | Cu B: Setări → Diagnostic → Comparare voci, Enter pe fiecare frază | varianta doi, „Paul prin RHVoice direct”, sună ca până acum |

Scenariile sunt și în `docs/manual-tests/faza-0.md`. Detaliile tehnice sunt în ADR 0004, ultima secțiune. Planul 0.11 are pasul 1 marcat „făcut” și deciziile tale noi (8–12) trecute în tabel.

## Ce urmează

Aștept rezultatele W-01…W-04 și ale regresiei. Pașii 2–4 nu încep până nu îi aprobi.
