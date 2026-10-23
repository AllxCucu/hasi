# HASI – Răspuns: corecturi, lista de regresie și ce lipsește pentru 0.11

Nu am început etapa 0.11. Toate cele **307 teste automate** trec:

| Proiect de test | Teste |
|---|---|
| nucleu | 186 |
| transcrieri și ecrane | 56 |
| motoare de voce | 29 |
| normalizator | 27 |
| arhitectură | 9 |

---

## 1. Ce am corectat

**Rezultatele tale au rămas necompletate.** În mesaj erau doar „...”. M-am uitat atunci în jurnalele tale de diagnostic de azi, de la 22:22 la 22:28. Din ele reiese că au mers:
- Control + Alt + H, după pierderea focusului;
- Alt + F4;
- Pornire automată → Nu;
- întrebarea de la Repornește, în mod de test.

**Problemă găsită și corectată: numele programelor se auzeau prost.** HASI a spus „Windows Terminal Host, 2 ferestre, Notepad.exe, Firefox, WhatsApp.Root”. Am verificat ce scriu programele astea în fișierele lor, pe calculatorul tău. Acum HASI alege un nume curat: „Windows Terminal”, „Notepad”, „Firefox”, „WhatsApp”. Cazurile sunt test automat, cu datele reale ale celor patru programe.

**Datele greșite:** am înlocuit „24 octombrie” cu „23 octombrie” peste tot:
- în `architecture.md`, `faza-0.md` și ADR 0003;
- într-un comentariu din teste.

**Regulile tale sunt acum în `CLAUDE.md`:**
- o cerere de propunere se oprește la propunere;
- o etapă înseamnă o oprire pentru testul tău, dacă nu îmi spui explicit să continui;
- înainte de fiecare predare pornesc HASI o dată, câteva secunde, fără taste, și verific jurnalul;
- îți vorbesc cu „tu”.

Pentru repornire și oprire, propunerea ți-am trimis-o și am implementat după „Da, aprob”. Greșeala a fost că am făcut trei etape într-o singură rundă și ți le-am predat fără să fi pornit HASI măcar o dată. Nu se mai întâmplă.

**Proba de pornire de azi** (22:40, fără taste, 8 secunde):
- pornește cu vocea salvată, „Paul, prin SAPI”;
- RHVoice direct se încarcă;
- bun venitul se rostește: în jurnal sunt `SAY`, `SPEAK` și primele cuvinte;
- nicio linie `ERROR`, nimic nou în `errors.log`.

Mesajul „HASI nu mai este în prim-plan” a apărut și el. E normal când HASI e pornit de mine: Windows nu dă tastele unei ferestre deschise din fundal.

---

## 2. Lista de regresie

Este în `docs/manual-tests/regresie-faza-0.md`:
- **40 de pași**, într-o singură sesiune, în ordinea în care îi parcurgi, fiecare cu „ce apăs / ce trebuie să aud”;
- grupați pe părți: pornire și navigare, setări, Test pronunție, diagnostic, Învață tastele și Despre, Ieșire, focus și o singură instanță, jurnal, pornire după eroare, Narator;
- la final, scenariile care nu încap într-o sesiune: repornirea Windows, Bluetooth, repausul, repornirea reală.

Pașii care se pot verifica fără sunet sunt și test automat (`08-regresie.transcript`), deci textele din listă sunt confirmate de cod.

Două lucruri de știut înainte să o rulezi:
- În setările tale e salvată vocea „Paul, prin SAPI”. Pașii 9–10 o readuc la „Paul, RHVoice direct”.
- În Test pronunție, Enter nu golește câmpul. Lista îți spune când să ieși și să intri din nou.

---

## 3. Ce mai lipsește pentru 0.11

### a) Înainte de 0.11 (din plan)

1. **Compilarea RHVoice.dll în CI** (ADR 0004). Pași:
   - RHVoice ca submodul git, fixat pe o versiune și un commit;
   - un job GitHub Actions pe Windows (MSVC) care construiește doar biblioteca, pe x64 și Release;
   - datele lui Paul dintr-o versiune fixată, verificate cu SHA-256;
   - în „Despre” și în `THIRD-PARTY-NOTICES.md`: commitul exact și legătura spre sursă, cum cere GPL.

   Am verificat azi depozitul RHVoice (ultima versiune: 1.18.4). Are și `CMakeLists.txt`, și `SConstruct`, dar propriul CI de Windows construiește cu **SCons** pe `windows-latest` (`pip install lxml scons`, apoi `scons`). Propun să facem la fel, nu CMake cum scria în planul ADR 0004, fiindcă varianta asta e deja dovedită pe același tip de mașină. Ce rămâne de aflat la implementare: dacă SCons poate construi doar biblioteca x64, fără restul pachetului NVDA/SAPI.
2. **„Simulare oprire” devine oprită implicit.** Pentru utilizatorii noi e suficient să schimb valoarea implicită. Fișierul tău de setări a salvat însă deja „pornită”, iar acolo schimbarea nu are efect. Ori o oprești tu din Setări, ori fac o migrare care o oprește o singură dată. Spune-mi ce preferi.
3. **Rezultatele tale încă lipsă:**
   - ecranul de blocare (O-07, O-08);
   - evenimentul 27 (O-10);
   - durata tăcerii la G-08;
   - preferința de la P-13, nici ea completată niciodată.

### b) Conținutul etapei 0.11

1. **Installer Velopack per-utilizator,** fără drepturi de administrator, cu dezinstalare.

   Am găsit o problemă de rezolvat înainte: Velopack instalează în `%LOCALAPPDATA%\<id>` și **la dezinstalare șterge tot acel folder** ([Velopack, dezinstalare](https://docs.velopack.io/integrating/uninstalling), [Velopack pe Windows](https://docs.velopack.io/packaging/operating-systems/windows)). Dacă id-ul ar fi „HASI”, ar șterge și `%LOCALAPPDATA%\HASI`: jurnalele, `errors.log` și RHVoice-ul tău de test. Propun un id diferit (de exemplu `HASI.App`), ca datele să rămână separate. Decizia e a ta, la planul 0.11.
2. **Actualizare cu anunț rostit** („Este disponibilă o versiune nouă…”, M0-31), din sursă configurabilă (`github`, `http` sau `folder`). „Setări → Actualizări” e acum doar un loc rezervat.
3. **„Pornire automată” să indice executabilul instalat.** Acum indică `hasi.exe` din folderul de build.
4. **Velopack (MIT)** adăugat în `THIRD-PARTY-NOTICES.md` și în „Despre HASI”, în același commit.
5. **Publicare de test în CI**, apoi **prima versiune nesemnată** (0.1.0) în GitHub Releases. Fiind o publicare, o fac numai după confirmarea ta.
6. **README:** secțiunea „Code signing policy”, rolurile echipei și o politică de confidențialitate scurtă. Ea spune că singura legătură la internet e verificarea actualizărilor, care se poate opri.

### c) SignPath: ce ai de făcut tu, concret

Am verificat termenii azi ([signpath.org/terms](https://signpath.org/terms)). Pagina e încă marcată „draft”.

1. **MFA:** pe GitHub o ai deja. Va trebui activată și pe contul SignPath.
2. **Rolurile:** tu ești autor, recenzent și aprobator. Termenii cer ca echipa care semnează să fie aceeași cu cea care dezvoltă, adică tu, cu contul tău. Confirmă-mi că vrei să apari așa în README.
3. **Prima versiune publică:** după 0.11 aprobi publicarea versiunii 0.1.0 nesemnate. SignPath acceptă doar proiecte deja publicate în forma care se semnează. Cât timp versiunea e nesemnată, Windows SmartScreen va avertiza la instalare.
4. **Formularul de aplicare:** completezi formularul de la signpath.org/apply. Nu i-am putut citi câmpurile (se încarcă dinamic), așa că îl facem împreună. Acceptarea e la discreția lor.
5. **După acceptare:**
   - îți faci cont SignPath, cu MFA;
   - instalezi aplicația SignPath pe depozitul GitHub;
   - îmi dai identificatorii organizației și ai proiectului;
   - eu adaug semnarea în GitHub Actions, iar tu aprobi manual fiecare semnare.

Ce e deja în regulă: licența GPL-3.0-or-later, CI-ul public, numele și versiunea produsului în metadatele fișierelor (cerință nouă în termeni, pe care o îndeplinim deja). RHVoice.dll poate fi inclus nesemnat.

### d) Altceva din plan

„Gata” pentru Faza 0 înseamnă toate scenariile trecute pe **Windows 11 și pe Windows 10**, cu și fără NVDA. Pentru Windows 10 ai nevoie de un calculator sau de o mașină virtuală. Spune-mi dacă ai unul.

---

**Următorul pas e la tine:** rulezi lista de regresie și îmi trimiți rezultatele, plus răspunsurile de la §3: migrarea pentru „Simulare oprire”, id-ul Velopack, rolurile SignPath și Windows 10. Abia apoi pregătesc planul 0.11, pe care ți-l propun înainte de orice cod.
