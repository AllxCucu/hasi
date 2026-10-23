# HASI – Răspuns după al doilea experiment RHVoice.dll

Toate cele **208 teste automate** trec, local și pe GitHub:

| Proiect de test | Teste |
|---|---|
| nucleu | 140 |
| normalizator | 22 (fișiere cu peste 200 de cazuri) |
| motoare de voce | 23 (proiect nou) |
| transcrieri | 14 |
| arhitectură | 9 |

Mă opresc aici, cum ai cerut: **asculți și decizi** dacă trecem la RHVoice.dll (secțiunea 4).

---

## 1. Comenzile de pornire

Din folderul proiectului:

**A – SAPI (varianta implicită de azi):**
```
dotnet run --project src\Hasi.App -- --diagnostic --rhvoice-path "C:\Users\cucua\AppData\Local\HASI\rhvoice-test"
```

**B – RHVoice direct:**
```
dotnet run --project src\Hasi.App -- --diagnostic --engine rhvoice-dll --rhvoice-path "C:\Users\cucua\AppData\Local\HASI\rhvoice-test"
```

Ce trebuie știut despre ele:
- **Folderul `rhvoice-test`.** Am pus acolo `RHVoice.dll` 1.18.2 (x64) și datele vocii Paul, luate din pachetele oficiale RHVoice pentru NVDA. Folderul e în afara depozitului și nimic binar nu e în git.
- **`--rhvoice-path` apare în ambele comenzi**, pentru că ecranul „Comparare voci” are nevoie de ambele voci. Fără opțiune, HASI pornește ca înainte, doar cu SAPI.
- **Opțiuni noi**, valabile pentru toate motoarele:
  - `--pitch -10..10` (tonul);
  - `--volume 0..100`;
  - `--rate -10..10` (viteza), ca înainte.

  SAPI nu are reglaj de ton, deci acolo `--pitch` nu are efect.

**Arhitectura DLL-ului față de HASI.** HASI e compilat și rulează pe **x64**. Pachetul oficial conține `RHVoice.dll` atât pentru x86, cât și pentru x64. HASI citește antetul PE al fișierului și îl încarcă doar pe cel x64. Dacă găsește doar varianta greșită, scrie în jurnal ce a găsit, spune „Motorul de voce cerut nu a pornit…” și folosește următoarea voce.

---

## 2. Ce am implementat

- **Deciziile tale:**
  - **Clear → Enter:** tasta 5 de pe tastatura numerică, cu Num Lock oprit, funcționează acum ca Enter. Insert rămâne nelegat.
  - **Dicționarul de genuri MULTEXT-East 4.0:** 109.550 de forme, în `src\Hasi.Text.Ro\Data\substantive.txt`, cu atribuirea în „Despre HASI” și în THIRD-PARTY-NOTICES.
  - **Fișierul tău** `src\Hasi.Text.Ro\Data\genuri.txt` (`formă => m | f | n`) are prioritate. Editează-l în `src`, nu în `bin`: la compilare, fișierul din `bin` e suprascris.
  - **Strategia de rezervă:** toți pașii din ADR 0005, cum ai ales („păstrează doi”). Riscul acceptat: „Pagina 2 goală” devine „Pagina două goală”. Genurile ghicite care schimbă cuvintele ajung în jurnal ca `LEXICON? cuvânt`.
  - **P-13** rămâne „unu din patru”.
- **Motorul `RhVoiceDllEngine`:** sintetizează pe un fir separat și rămâne cel mult 3 secunde înaintea redării, ca o citire lungă să nu umple memoria. SAPI rămâne implicit și rămâne în lanțul de rezervă.
- **Biblioteca audio: NAudio 3.1.0** (licență MIT, compatibilă cu GPL-3.0). Am ales-o pentru că playerul ei WASAPI poate **urma dispozitivul implicit**: Windows însuși mută sunetul pe dispozitivul nou când scoți căștile, același mecanism ca la sunetele sistemului. Am luat 3.1.0 și nu 3.3.0, care are doar patru zile.
- **Oprirea:** un fade-out de 8 ms, ca să nu pocnească, apoi coada se golește, iar RHVoice primește semnalul de oprire.
- **Căștile Bluetooth:** fluxul audio stă deschis permanent și redă liniște între mesaje, deci dispozitivul nu adoarme și prima silabă nu se taie. Dacă fluxul trebuie redeschis, se adaugă întâi 150 ms de liniște.
- **Schimbarea dispozitivului și revenirea din repaus:** le preia Windows, prin rutarea fluxului. Dacă fluxul se oprește totuși cu eroare, HASI îl reface o dată; dacă nu reușește, trece la vocea următoare.
- **Formatul sunetului.** Paul are 24 kHz, mono. HASI îl reeșantionează la frecvența dispozitivului (la tine, 48 kHz) și îl trimite pe ambele canale din față. Dispozitivul tău raportează 8 canale, iar fără această conversie vocea s-ar fi putut auzi doar din difuzorul central.
- **Pozițiile cuvintelor** apar în jurnal ca rânduri `WORD`. Am corectat o problemă: RHVoice dă pozițiile în octeți UTF-8, nu în caractere. Nu am construit nimic peste ele.
- **Jurnalul de diagnostic**, pentru ambele motoare, are acum rândurile:
  - `LATENCY … first-sound` (primul sunet) și `LATENCY … stop` (oprirea);
  - `PERF` (procesor și memorie după fiecare mesaj);
  - `RHVOICE` (DLL-ul, datele și dispozitivul audio).
- **Ecranul „Comparare voci”** (Setări → Diagnostic): cinci fraze fixe (numere, diacritice, frază lungă, întrebare, dată și oră), rostite pe rând prin „Paul prin SAPI” și „Paul prin RHVoice direct”. Săgeata stânga redă doar prima variantă, săgeata dreapta doar a doua, iar orice tastă oprește.

---

## 3. Măsurătorile (și în ADR 0004)

Le-am făcut cu `spikes\SpeechBench`: fiecare motor în propriul proces, aceleași texte, redare reală la volum 25, pe calculatorul tău. Testul a însemnat 5 fraze scurte, o citire lungă de aproximativ 40 de secunde și 5 opriri în mijlocul vorbirii.

| | SAPI (Paul) | RHVoice.dll (Paul) |
|---|---|---|
| Primul sunet (mediană) | 14 ms | 12 ms |
| Oprire (mediană) | 33 ms | **5 ms** |
| Procesor, în timpul citirii lungi | 0,35% din toate nucleele | 0,35% |
| Memorie maximă în timpul citirii | 90 MB | **70 MB** |

Cum se citesc cifrele:
- Ambele variante exclud memoria tampon a plăcii de sunet. La DLL, aceasta adaugă cel mult 40 ms.
- În HASI, primul mesaj după pornire include și încărcarea vocii: 280–400 ms la DLL.

**Recomandarea mea (ADR 0004):** DLL-ul e cel puțin la fel de bun ca SAPI la toate măsurătorile, iar câștigul principal rămâne installer-ul unic. Recomand **trecerea la RHVoice direct ca variantă implicită**, cu SAPI și OneCore ca rezerve. Condiția: ascultarea ta (R-01…R-08) să nu găsească pocnituri, silabe tăiate pe Bluetooth sau blocaje la schimbarea dispozitivului. Compilarea DLL-ului în CI, dintr-un commit fixat, e descrisă în ADR **doar ca plan**; nu am implementat-o.

---

## 4. Scenariile R-xx (și în `docs/manual-tests/faza-0.md`)

| ID | Ce faci | Ce urmărești |
|---|---|---|
| R-01 | Navighezi 2–3 minute cu B, apoi la fel cu A | aceeași voce Paul. Notează orice diferență de timbru, volum, viteză sau claritate. |
| R-02 | Setări → Diagnostic → Comparare voci; Enter pe fiecare frază; săgețile stânga și dreapta | „Varianta unu, Paul prin SAPI. …”, apoi „Varianta doi, Paul prin RHVoice direct. …”; orice tastă oprește |
| R-03 | Cu B: F1, apoi săgeata jos în timpul vorbirii; de 10 ori | oprire imediată |
| R-04 | Cu B: Control de mai multe ori în timpul vorbirii; asculți și începutul mesajelor | nicio pocnitură, nicio silabă tăiată |
| R-05 | Căști Bluetooth ca dispozitiv implicit; B, 30 de secunde de liniște, apoi săgeata jos | prima silabă („Î” din „Învață tastele”) se aude întreagă. Repetă cu A. |
| R-06 | Cu B, în timpul unui mesaj lung, scoți căștile sau schimbi dispozitivul implicit | vorbirea continuă pe dispozitivul nou, fără blocaj |
| R-07 | Cu B deschis: repaus, trezire, apoi săgeata jos | HASI vorbește normal |
| R-08 | B cu `--rate -5`, `--rate 5`, `--pitch 3`, `--volume 50` | se schimbă viteza, tonul și volumul. Cu A se schimbă doar viteza și volumul. |
| R-09 | B cu `--rhvoice-path C:\nu-exista` | „Motorul de voce cerut nu a pornit. Folosesc vocea Paul. Detaliile sunt în jurnalul de diagnostic.” |
| R-10 | După o sesiune cu B, deschizi jurnalul de diagnostic | rânduri `LATENCY`, `WORD` și `PERF`. Trimite-mi-l. |
| R-11 | Cu Num Lock oprit, în meniu, tasta 5 de pe tastatura numerică | același efect ca Enter |
| R-12 | Test pronunție: `2 autobuze`, apoi `2 smartfoane` | „două autobuze”; „doi smartfoane” (gen ghicit, greșit) și `LEXICON? smartfoane` în jurnal. Adaugi `smartfoane => f` în `genuri.txt`, repornești și auzi „două smartfoane”. |

---

## 5. Ce nu am putut testa singur

| Ce | De ce |
|---|---|
| Calitatea sunetului, pocniturile | trebuie judecate după ureche (R-01…R-04) |
| Căștile Bluetooth | nu am un dispozitiv Bluetooth conectat (R-05) |
| Scoaterea căștilor și revenirea din repaus | cer acțiuni fizice (R-06, R-07) |
| Tastarea și tastele în fereastra reală | regula interzice inputul simulat. Rularea mea a fost fără taste: am pornit HASI de două ori, câte 6–8 secunde, iar `SpeechBench` a redat sunet real, la volum 25, aproximativ un minut pentru fiecare motor. |
| Comparația cu motorul OneCore | nu am cerut-o; OneCore rămâne în lanț |

---

## 6. Adresa de e-mail în git

Am setat adresa noreply GitHub **doar pentru acest depozit** (`12409211+AllxCucu@users.noreply.github.com`), cum ai ales. Commit-urile din această rundă o folosesc deja. Configurarea ta globală a rămas neschimbată.

**Pentru commit-urile deja publicate** (primele 20, cu adresa Gmail), ai patru opțiuni. Nu am făcut nimic fără acordul tău.

1. **Le lași așa.** Adresa e deja publică, iar orice clonă sau copie o păstrează. Nu costă nimic.
2. **Rescrii istoricul** (`git filter-repo` cu un fișier mailmap care înlocuiește adresa în toate commit-urile), apoi `git push --force`.
   - Toate commit-urile primesc alte identificatoare. Linkurile vechi către commit-uri și rulările CI nu mai corespund.
   - Clonele existente trebuie refăcute.
   - GitHub poate păstra o vreme commit-urile vechi, accesibile după identificator, iar ce a fost deja copiat de alții nu dispare.
   - În același pas se poate scoate și calea locală din versiunea veche a fișierului `raspuns.md`.
3. **Setări GitHub, recomandat oricum:** Settings → Emails → „Keep my email addresses private” și „Block command line pushes that expose my email”. Asta blochează pe viitor orice push cu adresa Gmail, dar nu schimbă trecutul.
4. **Ștergi și recreezi depozitul**, cu istoricul rescris. E cea mai radicală variantă și pierde CI-ul și eventualele stele. Nici ea nu șterge copiile făcute între timp.

Recomandarea mea: **3 acum**. Plus **2**, doar dacă vrei neapărat ca adresa să dispară din istoric. Atunci spune-mi explicit, iar eu pregătesc rescrierea și ți-o arăt înainte de `push --force`.
