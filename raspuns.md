# HASI – Faza 1: corecturile din 0.11 și modulul 1 (Ceas, dată și calendar)

Modulul 1 (ceas, dată și calendar) și corecturile din 0.11 sunt gata pentru testul tău după ureche. Build-ul trece fără avertismente, iar toate cele **456 de teste** trec. Tastele exacte și textele le poți vedea și corecta în `tests/Hasi.Transcripts.Tests/Transcripts/11-ceas-si-calendar.transcript`.

Pasul 0 și partea A le-a scris Codex, iar eu am citit tot diff-ul și am rulat testele. Partea B am scris-o eu, cum ai cerut, pentru că lui Codex i se terminase limita de folosire.

Deciziile tale sunt trecute în `docs/plan-faza-1.md` (§4, §5, §6, §8), în ADR 0007 (deciziile 9–11) și în `docs/architecture.md` §6 (commit `0e7199f`).

## Ce poți testa

1. **Corecturile din 0.11** (`2c7b693`):
   - **Despre HASI:** vocea spune „RHVoice versiunea 1.18.4”, licența și „Sursa exactă este indicată în fișierul de licențe”, fără commit. Pe ecran commitul rămâne.
   - **Fără arhivă portabilă:** `pack.ps1` trimite acum `--noPortable`, deci arhiva nu se mai produce.
   - **RHVoice în build-ul de dezvoltare:** HASI pornit din build vorbește iar cu „Paul, RHVoice direct”. Copia de test stă în `%LOCALAPPDATA%\HASI.Dev\rhvoice`, luată din artifactul CI, iar HASI caută acolo doar în build-urile de dezvoltare.
2. **Partea A** (`103fe5c`):
   - **Ctrl+T** spune ora oriunde și apare în „Învață tastele” și în tabelele de taste.
   - **Ora fixă rostită** e în Setări, oprită implicit.
   - **Alegerea datei** e în nucleu, pentru calendar și, mai târziu, pentru agendă.
3. **Modulul 1** (`6f187c0`): „Ceas și calendar” e primul în meniul principal și are patru opțiuni:
   - **Ora** și **Data:** data spune și sărbătoarea legală, dacă e cazul.
   - **Calendar:** săgețile sus și jos schimbă ziua; Page Up/Page Down sau săgețile stânga și dreapta schimbă luna; Home/End duc la prima și ultima zi a lunii.
   - **Sărbători legale:** lista unui an, care pornește de la următoarea sărbătoare; stânga și dreapta schimbă anul.

## Decizii luate de mine, pe care să le verifici

- **Sărbătorile sunt cunoscute doar din 2017.** Pentru anii dinainte HASI nu spune nimic, ca să nu inventeze. Textul oficial al art. 139 arată că alin. (1) a fost rescris de Legea 176/2016, în vigoare din 16 octombrie 2016, deci regulile se pot verifica doar din 2017 încolo.
- **Lista pe 2026 se potrivește cu cea a Patriarhiei,** inclusiv 1 iunie, care e și Ziua Copilului, și a doua zi de Rusalii. Sursa e trecută în `docs/data-sources.md`.
- **Numele sărbătorilor** stau în `ro.json`, unde le poți schimba. Exemple: „Paștele ortodox, prima zi”, „Rusaliile ortodoxe, a doua zi”, „Vinerea Mare ortodoxă”.
- **Luna se schimbă și cu săgețile stânga și dreapta,** nu doar cu Page Up/Page Down. Pe multe laptopuri Page Up/Page Down cer tasta Fn.
- **Tastele 3 și 9 de pe tastatura numerică,** cu Num Lock pornit, schimbă luna în calendar. În câmpurile de text tot cifre scriu.
- **În „Despre HASI” am păstrat „de Olga Yakovleva și colaboratorii”.** Tu ai scris „doar versiunea, licența și sursa”, dar e atribuirea autorului. Dacă o vrei scoasă, o scot.
- **Ora fixă și pornirea HASI:** dacă pornești HASI în primele 2 minute după o oră fixă, cu setarea pornită, auzi ora după bun venit.

## Pentru testul tău

- **Scenariile modulului:** `docs/manual-tests/faza-1-modul-1.md`, de la C1-01 la C1-26. Primele trei sunt corecturile din 0.11.
- **Lista de regresie** se numește acum `docs/manual-tests/regresie.md`.
  - Pașii 1–5, 14, 28 și 29 s-au schimbat: meniul principal are 5 opțiuni, Setări 12, iar rândul RHVoice din Despre e cel nou.
  - Pașii 41–45 sunt noi, pentru ceas.
- **Cu NVDA pornit**, te rog verifică dacă **Ctrl+T** și **Page Up/Page Down** ajung la HASI.

## Rularea de verificare

Am pornit HASI 12 secunde cu `--diagnostic`, fără nicio tastă:
- motorul e `rhvoice-dll`, din `HASI.Dev\rhvoice`;
- bun venit: „Meniu principal. 5 opțiuni. Selectat: Ceas și calendar, 1 din 5.”;
- nicio eroare în jurnal, iar `errors.log` n-a primit nimic nou.

Aștept rezultatele tale înainte de modulul 2. La începutul lui îți arăt tastele exacte ale editorului.

## Surse

- [Codul muncii, forma la zi (legislatie.just.ro)](https://legislatie.just.ro/Public/DetaliiDocument/128647)
- [Zile și sărbători legale în care nu se lucrează (calendar.patriarhia.ro)](https://calendar.patriarhia.ro/zile-si-sarbatori-legale-in-care-nu-se-lucreaza/)
- [Nu se fac nunți 2026–2027 (calendar.patriarhia.ro)](https://calendar.patriarhia.ro/nu-se-fac-nunti-in-anul-2025/)
- [Digi24 – Paștele 2027](https://www.digi24.ro/stiri/actualitate/evenimente/cand-va-fi-pastele-in-2027-diferenta-majora-intre-datele-de-celebrare-la-ortodocsi-si-catolici-3707239)
- [Calendar ortodox 2027 (calendareortodoxe.ro)](https://calendareortodoxe.ro/calendar-ortodox-2027)
- [crestinortodox.ro – Paște 2027](https://www.crestinortodox.ro/paste-2027/paste-ortodox-2027/)
- [EVZ – Paștele până în 2030](https://evz.ro/cand-pica-pastele-ortodox-si-cel-catolic-intre-anii-2021-2030.html)
- [Playtech – Paștele în următorii ani](https://playtech.ro/2025/pe-ce-data-pica-pastele-ortodox-si-catolic-in-2026-si-in-urmatorii-ani-calendarul-complet/)
