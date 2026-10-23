# HASI – Răspuns după corecturile din 0.4 și etapa 0.5

Toate cele **165 de teste automate** trec, local și pe GitHub:

| Proiect de test | Teste |
|---|---|
| Hasi.Core.Tests | 131 |
| Hasi.Text.Ro.Tests | 15 (fișiere cu peste 180 de cazuri) |
| Hasi.Architecture.Tests | 9 |
| Hasi.Transcripts.Tests | 10 |

Depozitul public este **https://github.com/AllxCucu/hasi**, cu CI verde. Mă opresc aici: îți cer **trei decizii** (secțiunea 1), apoi urmează raportul tău pentru scenariile din secțiunea 7.

---

## 1. Deciziile pe care ți le cer

1. **RHVoice.dll direct, în loc de SAPI** (ADR 0004, secțiunea 5). Recomand să trecem, în doi pași:
   - un al doilea experiment, cu redare audio proprie, pe care să-l judeci după ureche;
   - apoi trecerea propriu-zisă, înainte de installer.

   Până decizi, HASI rămâne pe SAPI.
2. **Dicționarul de genuri** (ADR 0005, secțiunea 4). Pot folosi lexiconul MULTEXT-East (licență CC BY-SA 4.0)? Și vrei să cer acordul celor de la dexonline?
3. **Tastele 5 și 0 de pe tastatura numerică, cu Num Lock oprit** (secțiunea 3). Pot lega tasta „Clear” (5) de Enter? E o modificare a KeyMap-ului aprobat, deci nu am făcut-o fără tine.

Și o preferință pe care n-ai completat-o: la **P-13**, rândul a rămas „prefer [varianta HASI / varianta vocii]”. Până îmi spui, rămâne varianta HASI („unu din patru”).

---

## 2. Ce am făcut din raport

- **Jurnal și nume de depozit.** În raport au rămas `[cale exactă a fișierului]` și `[AllxCucu/hasi sau alt nume]`. Am folosit numele confirmat data trecută, **AllxCucu/hasi**.
- **Ce s-a publicat odată cu depozitul.** Commit-urile poartă numele și adresa de e-mail din configurarea ta git (cucumarianalexandru@gmail.com), iar istoricul conține și calea locală a proiectului dintr-o versiune mai veche a acestui fișier.
- **CI:** trece pe `windows-latest`. Am actualizat acțiunile GitHub (`checkout@v7`, `setup-dotnet@v6`), pentru că versiunile vechi foloseau Node 20, care e învechit.
- **Orele întregi** se spun acum fără „fix”: „14:00” devine „ora paisprezece”. „21:20” rămâne „ora douăzeci și unu și douăzeci de minute”.
- **Cazurile tale** (`ora 2:00`, `12:15`, `22:10`, `1:00`) sunt în `tests\Hasi.Text.Ro.Tests\Cases\04-date-si-ore.cases` și trec toate. „ora 2:00” devine „ora două”, fără „fix”, după alegerea ta.

---

## 3. Cerințele noi

### 3.1 Acronimele (gata)

Fișierul editabil este `Data\acronime.txt`, lângă `hasi.exe`; în sursă e `src\Hasi.Text.Ro\Data\acronime.txt`. Formatul este același cu al fișierelor de cazuri:

```
CFR => litere          → „ce fe re”
ANAF => cuvânt         → „Anaf”
RATB => ra te be       → exact cum e scris
```

Merg și formele articulate: „CFR-ul” devine „ce fe re-ul”. Literele au numele românești: be, ce, de, fe, ge, haș, je, ca, le, me, ne, pe, chiu, re, se, șe, te, țe, ve, dublu ve, ics, i grec, ze. Acronimele necunoscute, de exemplu NATO, rămân la vocea Paul. După ce modifici fișierul, repornește HASI.

Cum am trecut lista ta (te rog să le verifici după ureche):
- **pe litere:** CFR, SRL, PFA, CNP, CASMB („ce a se me be”), STB, ANM, BCR, BRD, UE („u e”);
- **ca un cuvânt:** ANAF, SUA;
- **pronunție explicită:** RATB („ra te be”).

Un efect secundar: „CNP 1850101…” spune acum „ce ne pe unu opt cinci…”.

### 3.2 Tastatura numerică în câmpurile de text (gata)

În câmpurile de text:
- cu **Num Lock pornit**, tastele 0–9 și tasta zecimală scriu cifre;
- cu **Num Lock oprit**, tastatura numerică navighează;
- la fiecare apăsare pe Num Lock, HASI spune „Tastatura numerică: cifre.” sau „Tastatura numerică: navigare.”.

**Afectează KeyMap-ul aprobat?**
- **Legăturile aprobate nu se schimbă.** În meniuri, tastatura numerică navighează ca înainte, indiferent de Num Lock, iar acolo apăsarea pe Num Lock nu spune nimic, pentru că nu schimbă nimic.
- **S-a adăugat o regulă de context, nu o legătură nouă.** Într-un câmp de text cu Num Lock pornit, NumPad 0–9 scriu cifre în loc de comenzile lor de navigare. Regula e în nucleu (`TextRouting`) și e testată.
- **Tastele / * - de pe tastatura numerică rămân comenzi globale** (ajutor, repetă, unde sunt), chiar și în câmpurile de text, deci nu scriu „/”, „*” sau „-”.
- **O problemă pe care nu am rezolvat-o.** Cu Num Lock oprit, Windows trimite pentru 5 tasta „Clear”, iar pentru 0 tasta „Insert”, iar acestea nu au legătură în KeyMap. În câmpuri, dar și în meniuri, 5 și 0 nu fac nimic atunci. Propunere: **Clear → Enter**, sigur, pentru că „Clear” există doar pe tastatura numerică. „Insert” nu-l leg: e și tasta de pe tastatura mare, și modificatorul NVDA. Aștept acordul tău.

---

## 4. Dicționarul de genuri: propunere, nimic integrat (ADR 0005)

Ce am verificat:

| Sursă | Licență | Verdict |
|---|---|---|
| **MULTEXT-East 4.0, lexiconul românesc** | **CC BY-SA 4.0**, compatibilă cu GPL-3.0 | **Recomandat.** Am descărcat fișierul într-un folder temporar doar ca să-l verific: 120.228 de forme de substantive, 18.705 lexeme. Genul de acord e marcat exact cum ne trebuie: neutrul apare ca masculin la singular („dosar”) și ca feminin la plural („dosare”). |
| Wiktionary (kaikki.org) | CC BY-SA 4.0 + GFDL | Completare bună pentru cuvintele noi |
| dexonline | Definițiile sunt GPL, dar **flexiunile nu sunt acoperite** de pagina de licență, iar site-ul interzice preluarea automată fără acord scris | Doar cu acordul lor; le pot scrie, dacă vrei |
| UniMorph (română) | CC BY-SA **3.0** | **Incompatibil** cu GPL-3.0 |

Cum l-aș folosi:
- un instrument rulat la build extrage tabelul `formă → gen` (aproximativ 2 MB), cu ş/ţ convertite în ș/ț;
- atribuirea apare în „Despre HASI”;
- plus un fișier `Data\genuri.txt` al tău, de exemplu `minut => n`, care are prioritate față de dicționar.

**Strategia de rezervă** pentru substantivele necunoscute:
1. fișierul tău, apoi dicționarul;
2. terminațiile sigure de plural (-uri, -ări, -ții, -uni, -ăți);
3. terminațiile de singular (-ă, -ie, -ea înseamnă feminin);
4. **„de” nu depinde de gen**: de la 20 în sus îl pun înaintea oricărui cuvânt care pare substantiv;
5. dacă genul rămâne necunoscut (contează doar la 1, 2, 12, 22…), folosesc forma masculină și scriu `LEXICON? cuvânt` în jurnalul de diagnostic, ca să mi-l raportezi.

---

## 5. Etapa 0.5: motoarele de voce

**Lanțul de rezervă (gata).** HASI pornește cu cea mai bună voce care funcționează: Paul, apoi Microsoft Andrei prin SAPI5, apoi Andrei prin OneCore, apoi orice altă voce. Dacă o voce se strică **în timpul folosirii**, HASI trece la următoarea și spune „Vocea Paul nu mai funcționează. Folosesc vocea Microsoft Andrei.”, apoi repetă ultimul mesaj, ca să nu se piardă nimic. Fără nicio voce, se aude tonul de eroare, iar mesajul apare pe ecran.

**OneCore** a fost verificat acum, pentru prima dată: pornește și vorbește cu Andrei, fără erori. Ca să-l asculți, folosește `--engine onecore`.

**Opțiuni noi pentru teste:**

| Opțiune | Efect |
|---|---|
| `--engine sapi5` / `--engine onecore` | forțează motorul |
| `--test-voice-failure` | vocea principală „se strică” după trei mesaje, ca să auzi trecerea la vocea următoare |

**Auditul de licență RHVoice (ADR 0004):**
- `RHVoice.dll` combină fișiere LGPL-2.1+ cu fișiere **GPL-2.0-or-later** (motorul HTS modificat și câteva fișiere din nucleu);
- **nu există niciun fișier GPL-2.0-only**, deci DLL-ul e compatibil cu HASI (GPL-3.0-or-later);
- dacă îl distribuim, trebuie să oferim sursele exacte. Cel mai curat e să-l compilăm noi în CI, dintr-un commit fixat;
- datele limbii române sunt LGPL, iar vocea Paul e CC BY-SA 4.0.

**Experimentul.** L-am rulat cu `RHVoice.dll` și vocea Paul din pachetele oficiale pentru NVDA, ținute într-un folder temporar, în afara depozitului.

| Măsurătoare | DLL direct | SAPI5 |
|---|---|---|
| Primul sunet | **4 ms** | nu se poate măsura |
| Sinteza unei fraze de 4,5 secunde | 131 ms | 130 ms |
| Oprire la cerere | **sub 1 ms** | „instant”, după raportul tău |

**Concluzie:** trecerea la DLL nu aduce viteză. Aduce altceva:
- **un singur installer**, fără instalarea separată a vocii Paul. Pentru utilizatorii noștri, asta e câștigul cel mai mare;
- control complet asupra sunetului (ton, volum, poziția cuvântului citit);
- independență de instalarea SAPI.

**Ce costă:**
- HASI trebuie să redea singur sunetul, cu cod nou care trebuie testat după ureche;
- obligațiile GPL pentru binarul distribuit;
- aproximativ 18 MB în plus în installer.

Recomandarea mea și opțiunile sunt în ADR 0004; aștept decizia ta (punctul 1 din secțiunea 1).

---

## 6. Cum pornești

```
dotnet run --project src\Hasi.App -- --diagnostic
```

Pentru scenariile V-02…V-05 adaugi `--engine onecore`, `--test-voice-failure` sau `--rate -3`, după tabel.

---

## 7. Scenariile (și în `docs/manual-tests/faza-0.md`)

**Corecturi și acronime** (în Test pronunție):

| ID | Ce scrii | Ce trebuie să auzi |
|---|---|---|
| A-01 | `14:00`, `ora 2:00`, `1:00` | „ora paisprezece”, „ora două”, „ora unu” |
| A-02 | `Bilet CFR la STB și RATB` | „Bilet ce fe re la se te be și ra te be” |
| A-03 | `Declarația la ANAF, CNP-ul meu` | „Declarația la Anaf, ce ne pe-ul meu” |

**Tastatura numerică:**

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| N-01 | În Test pronunție, cu Num Lock pornit, apeși 1, 2, 3 de pe tastatura numerică | „1”, „2”, „3” (se scriu cifre) |
| N-02 | Oprești Num Lock | „Tastatura numerică: navigare.” |
| N-03 | Cu Num Lock oprit, apeși 8 de pe tastatura numerică | „Textul scris: …” |
| N-04 | Pornești Num Lock | „Tastatura numerică: cifre.” |
| N-05 | În meniu, cu Num Lock pornit, apeși 2 și 8 de pe tastatura numerică; apoi Num Lock | navigare, ca înainte; Num Lock nu spune nimic |

**Motoarele de voce:**

| ID | Cum pornești / ce faci | Ce trebuie să auzi |
|---|---|---|
| V-01 | fără opțiuni noi | Paul, ca până acum |
| V-02 | `--engine onecore` | Andrei prin OneCore. Compară cu Paul oprirea vorbirii la apăsarea unei taste. |
| V-03 | `--test-voice-failure`, apoi săgeata jos de 3–4 ori | „Vocea Paul nu mai funcționează. Folosesc vocea Microsoft Andrei.” și ultimul mesaj, repetat cu Andrei; apoi totul continuă normal |
| V-04 | după V-03, F2 | ultimul mesaj, cu Andrei |
| V-05 | `--engine onecore --rate -3` | Andrei vorbește mai rar |

---

## 8. Ce nu e făcut sau nu e testat

| Ce | De ce |
|---|---|
| Vocea, viteza, volumul și tonul din Setări (cu salvare) | etapa 0.6. Până atunci, doar `--voice`, `--rate` și `--engine`. |
| Redarea audio proprie pentru RHVoice.dll | depinde de decizia ta (ADR 0004) |
| Integrarea dicționarului de genuri | depinde de decizia ta (ADR 0005) |
| Clear → Enter pe tastatura numerică | depinde de acordul tău (secțiunea 3.2) |
| Trecerea la altă voce, testată pe o defecțiune reală (de exemplu o voce dezinstalată în timpul rulării) | testată doar cu `--test-voice-failure` și cu teste automate |
| Tastarea cifrelor de pe tastatura numerică în fereastra reală | logica e testată automat; tastarea reală e testul tău (N-01…N-05), pentru că regula interzice inputul simulat |

În toată runda asta nu am trimis nicio tastă. Am pornit HASI de două ori, câte 6 secunde, fără input: o dată cu SAPI și o dată cu `--engine onecore`.
