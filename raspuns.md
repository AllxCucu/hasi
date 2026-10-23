# HASI – Răspuns după corecturi și etapa 0.7

Toate cele **250 de teste automate** trec, local și pe GitHub:

| Proiect de test | Teste |
|---|---|
| nucleu | 154 |
| transcrieri și ecrane | 31 |
| motoare de voce | 29 |
| normalizator | 27 |
| arhitectură | 9 |

Mă opresc aici, cum ai cerut.

**Pornire:** `dotnet run --project src\Hasi.App -- --diagnostic`. Pentru testele de repornire fără risc, adaugă `--simulate-power`.

**De știut înainte să testezi:** în `%APPDATA%\HASI\settings.json` au rămas, de la testele S-xx, vocea **„Paul, prin SAPI”** și viteza **70** (`Rate: 4`). De aceea HASI pornește acum cu SAPI. Ca să revii la RHVoice direct, alegi din Setări → Voce „Paul, RHVoice direct”, iar din Setări → Viteză valoarea 50. Sau ștergi pur și simplu fișierul.

---

## 1. Corecturile raportate

- **R-12:** am scos `bitcoini => f`; „doi bitcoini” e corect, cum ai spus. Exemplul de suprascriere e acum **„postare”**:
  - am verificat că lipsește din MULTEXT-East;
  - terminația nu dă un gen sigur, așa că regula îl ghicește masculin: „1 postare” devine „un postare”, greșit, cu `LEXICON? postare` în jurnal;
  - cu `postare => f` în `genuri.txt` devine „o postare”. Am pus și un test automat pentru asta.

  În jurnalele tale nu era niciun rând `LEXICON?`, de aceea am ales un cuvânt verificat de mine.
- **C-04:** cazurile sunt acum `2 tramvaie`, `2 meciuri`, plus `cele 2 tramvaie`, care devine „cele două tramvaie”. Am trecut „cel, cea, cei, cele” printre cuvintele care nu sunt niciodată substantivul numărat.
- **Lista de voci** arată implicit doar vocile românești, iar la sfârșit apare **„Arată toate vocile”**. Dacă vocea aleasă nu e românească, rămâne totuși în listă.
- **Repausul sunetului** rămâne doar în `settings.json`, nu în meniu.

---

## 2. Etapa 0.7

- **„Învață tastele”:**
  - orice tastă își spune numele și funcția, fără să se execute. Asta include F1, F2, F3, Control, Alt+F4, tastatura numerică, Num Lock și literele tastate;
  - mesajul de intrare prezintă întâi alternativele care merg pe orice laptop (Control H, R, U);
  - Escape o dată doar explică; **Escape de două ori la rând** iese.
- **Meniul Ieșire:** „Închide HASI”, „Repornește calculatorul”, „Oprește calculatorul”, fiecare cu confirmare vocală.
  - Repornirea și oprirea pornesc după „La revedere”, prin `shutdown.exe` din Windows.
  - Dacă Windows refuză, HASI spune asta și rămâne deschis.
  - Cu **`--simulate-power`** (opțiune nouă, doar pentru teste), HASI scrie în jurnal ce ar fi făcut și se închide, fără să repornească sau să oprească calculatorul.
  - Enter ținut apăsat nu repornește niciodată calculatorul; e acoperit de un test.
- **Alt+F4 și închiderea din bara de activități** cer confirmare peste tot. În „Învață tastele”, Alt+F4 doar se explică, dar închiderea din bara de activități tot întreabă.
- **„Despre HASI”** are 9 rânduri:
  - licența RHVoice e corectată după audit: biblioteca e GPL 2 sau ulterioară, cu părți LGPL 2.1, pentru că acum o folosim direct;
  - am adăugat datele limbii române (Stefan Moisei, LGPL);
  - THIRD-PARTY-NOTICES e actualizat la fel.
- **F1 în meniul principal** se încheie cu „Ca să aflați ce face fiecare tastă, alegeți Învață tastele.”
- **F2, F3 și Alt+F4** există din etapa 0.3. Am verificat, cu teste și transcrieri, că funcționează la fel și pe ecranele noi (valori, alegeri, Ieșire).

---

## 3. Scenariile pentru 0.7 (și în `docs/manual-tests/faza-0.md`)

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| K-01 | În meniul principal, F1 | ajutorul, încheiat cu „Ca să aflați ce face fiecare tastă, alegeți Învață tastele.” |
| K-02 | Meniu principal → Învață tastele | „Învață tastele. Apăsați orice tastă ca să aflați ce face; nimic nu se execută. Pe orice tastatură, și pe laptop: Control H … Ca să ieșiți de aici, apăsați Escape de două ori.” |
| K-03 | Săgeata jos, săgeata sus, Enter, Control + săgeata sus | „Săgeata jos: trece la opțiunea următoare.” … „Control plus săgeata sus: duce la prima opțiune.” |
| K-04 | F1, Control H, F2, Control U, Control singur, Alt+F4 | fiecare tastă își spune funcția; nimic nu se execută, iar HASI nu se închide |
| K-05 | Tasta 8 de pe tastatura numerică; tasta 5 cu Num Lock oprit; Num Lock; Shift; F5 | „Tasta 8 de pe tastatura numerică: …”, „Tasta 5 de pe tastatura numerică, cu Num Lock oprit: …”, „Num Lock: …”, „Shift din stânga: se ține apăsată împreună cu altă tastă.”, „F5: nu are nicio funcție în HASI.” |
| K-06 | `ș`, `7`, `,` | „Litera ș: când scrieți, scrie acest caracter. În meniuri nu are nicio funcție.”, „Cifra 7: …”, „virgulă: …” |
| K-07 | Escape o dată; altă tastă; Escape de două ori la rând | explicația plus „Apăsați încă o dată Escape …”; după altă tastă, Escape doar explică din nou; la al doilea Escape la rând: „Meniu principal. Selectat: Învață tastele, 2 din 4.” |
| K-08 | Ții apăsată săgeata jos | descrierea o singură dată |
| E-01 | Ieșire; F1 | „Ieșire. 3 opțiuni. …”; ajutorul se încheie cu „Fiecare alegere de aici vă cere confirmare: …” |
| E-02 | Închide HASI → Escape | întrebarea, apoi „Anulat. Ieșire. Selectat: Închide HASI, 1 din 3.” |
| E-03 | Repornește → Escape; Oprește → Escape | „Sigur doriți să reporniți calculatorul? Programele deschise se vor închide. …”, „Anulat. …”; la fel pentru oprire |
| E-04 | Cu `--simulate-power`: Repornește → Enter; apoi la fel cu Oprește | „Calculatorul se repornește. La revedere.”; HASI se închide, calculatorul nu. În jurnal: `POWER simulated: shutdown.exe /r /t 0` (respectiv `/s /t 0`). |
| E-05 | (opțional, real, după ce salvezi tot) fără `--simulate-power`: Repornește → Enter | calculatorul repornește |
| E-06 | În Învață tastele: clic dreapta pe HASI în bara de activități → Închide fereastra | „Sigur doriți să închideți HASI? …” |
| E-07 | Pe „Repornește calculatorul”, ții Enter apăsat | doar întrebarea; calculatorul nu repornește |
| D-01 | Despre HASI, rând cu rând | 9 rânduri, cu licența RHVoice corectată (rândul 4) și datele limbii române (rândul 6) |
| H-01 | Setări → Viteză: F3, apoi F2 | „Sunteți în: Meniu principal, Setări, Viteză.”, apoi același mesaj |

Plus scenariile corectate: **R-12** (cu „postare”) și **C-04** (cu `2 tramvaie`, `2 meciuri`, `cele 2 tramvaie`).

---

## 4. Ce nu e făcut sau nu e testat

| Ce | Detalii |
|---|---|
| Repornirea și oprirea reale | Nu le-am rulat, ca să nu-ți repornesc calculatorul. Am verificat doar logica (teste) și varianta simulată. E-05 e testul real, dacă vrei să-l faci. |
| Închiderea din bara de activități (E-06) | Necesită un clic real. |
| Tastele în fereastra reală | Ca de obicei, fără input simulat. Am pornit HASI o singură dată, 5 secunde, fără nicio tastă. |
| Restul | Cititorul de ecran (0.9), pornirea automată și tasta globală Control+Alt+H (0.10), installer-ul și compilarea RHVoice în CI (0.11). |
