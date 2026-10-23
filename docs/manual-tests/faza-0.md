# Teste manuale – Faza 0

Lista completă a scenariilor M0-01…M0-33 e în `docs/architecture.md` §4.2. Aici sunt scenariile
**adaptate la ce s-a implementat efectiv**, etapă cu etapă. Formulările exacte sunt și în
`tests/Hasi.Transcripts.Tests/Transcripts/`: dacă vrei altă formulare, o poți corecta direct acolo.

Convenții: „[sunet]” = un ton scurt înaintea mesajului.

## Etapa 0.3 – prima versiune care vorbește (M0-01…M0-12)

| ID | Ce apeși | Ce trebuie să auzi |
|---|---|---|
| M0-01 | Pornești HASI | „Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4.” Dacă Paul nu e instalat, urmează „Vocea Paul nu este instalată. Folosesc vocea Microsoft Andrei.” |
| M0-02 | Săgeata jos de 3 ori | „Învață tastele, 2 din 4.”, „Despre HASI, 3 din 4.”, „Ieșire, 4 din 4.” |
| M0-03 | Încă o dată săgeata jos | [sunet] „Sfârșitul listei. Ieșire, 4 din 4.” (lista nu sare la început) |
| M0-04 | Home, apoi End (pe laptop: Control + săgeată sus, apoi Control + săgeată jos) | „Setări, 1 din 4.”, apoi „Ieșire, 4 din 4.” |
| M0-05 | Home, apoi Enter pe Setări | „Setări. 12 opțiuni. Selectat: Voce, 1 din 12.” |
| M0-06 | Escape | „Meniu principal. Selectat: Setări, 1 din 4.” |
| M0-07 | Încă o dată Escape, în meniul principal | „Sunteți în meniul principal. Pentru a închide HASI, alegeți Ieșire.” HASI rămâne deschis. |
| M0-08 | Enter pe Setări, apoi F1 (sau Control H) | „Ajutor pentru Setări. Folosiți săgețile sus și jos…”, la final „…Aici veți putea schimba vocea, viteza vorbirii, culorile și alte preferințe.” |
| M0-09 | Săgeata jos, apoi F2 (sau Control R) | „Viteză, 2 din 12.”, apoi exact același mesaj încă o dată |
| M0-10 | F3 (sau Control U); apoi Enter pe Viteză și din nou F3 | „Sunteți în: Meniu principal, Setări.”; apoi mesajul despre Viteză; apoi „Sunteți în: Meniu principal, Setări, Viteză.” |
| M0-11 | Apeși F1 (mesaj lung) și, în timp ce vorbește, săgeata jos | Vorbirea se oprește **imediat** și auzi doar opțiunea nouă |
| M0-12 | Apeși F1 și, în timp ce vorbește, tasta Control | Liniște imediată, fără niciun alt mesaj |

Verificări suplimentare (opționale):

| ID | Ce apeși | Ce trebuie să auzi |
|---|---|---|
| X-01 | O literă oarecare, de exemplu Q | [sunet] „Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H.” |
| X-02 | Tastatura numerică (cu Num Lock pornit): 2, 8, 5, 0 | jos, sus, alege, înapoi |
| X-03 | Meniu principal → Despre HASI, apoi săgeata jos rând cu rând | versiunea, licența GPL, RHVoice, vocea Paul cu autorii, System.Speech |
| X-04 | Meniu principal → Ieșire → Închide HASI → Escape | „Anulat. Ieșire. Selectat: Închide HASI, 1 din 1.” |
| X-05 | Alt+F4, apoi Enter | „Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.”, apoi „La revedere.” și HASI se închide |
| X-06 | Alt+Tab către altă fereastră, apoi înapoi | „HASI nu mai este în prim-plan…”, apoi la revenire, de exemplu „Meniu principal. Selectat: Setări, 1 din 4.” |

Rezultat (raportul din 23 octombrie 2026): M0-01…M0-12 au trecut; oprirea vorbirii e instantanee; tastatura numerică merge cu NVDA în modul de somn; Paul se instalează fără drepturi de administrator și e detectat. Din jurnal: „Sfârșitul listei” se repeta când săgeata era ținută apăsată (corectat).

## Etapa 0.4 – normalizatorul românesc și „Test pronunție” (M0-33 și P-01…P-14)

Drumul până la ecran: Meniu principal → Setări (Enter) → End → săgeata sus (Diagnostic) → Enter → Enter pe „Test pronunție”.
Textele se scriu de la tastatură; Enter le rostește. Fiecare rând de mai jos se scrie singur în câmp: după fiecare test ștergi textul cu Backspace ținut apăsat.

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| P-01 | Intri în Test pronunție | „Test pronunție. Scrieți un text și apăsați Enter ca să-l auziți așa cum îl rostește HASI. Escape vă întoarce.” |
| P-02 | Scrii `abc`, apoi Backspace | „a”, „b”, „c”, apoi „Șters: c” |
| P-03 | Scrii `ș ț ă î â` (tastatura română) | fiecare literă, iar spațiile rostite „spațiu” |
| P-04 | Enter cu câmpul gol | „Câmpul este gol. Scrieți mai întâi un text.” |
| P-05 | Săgeata jos după ce ai scris ceva | „Textul scris: …” |
| **M0-33** | `Plata de 12,50 lei pe 1.03.2026 la 14:30`, Enter | „Plata de doisprezece lei și cincizeci de bani pe întâi martie două mii douăzeci și șase la ora paisprezece și treizeci de minute”. Pe ecran: „Scris: …” și „Rostit: …”. |
| P-06 | `1 leu`, apoi `21 lei`, apoi `0,50 lei` | „un leu”; „douăzeci și unu de lei”; „cincizeci de bani” |
| P-07 | `21.10.2026`, apoi `1 mai` | „douăzeci și unu octombrie două mii douăzeci și șase”; „întâi mai” |
| P-08 | `ora 9:05`, apoi `21:20` | „ora nouă și cinci minute”; „ora douăzeci și unu și douăzeci de minute” |
| P-09 | `2 ore`, `12 ore`, `2 ani`, `20 de oameni` | „două ore”, „douăsprezece ore”, „doi ani”, „douăzeci de oameni” |
| P-10 | `tel. 0722 123 456` | „telefon zero șapte doi doi, unu doi trei, patru cinci șase” |
| P-11 | `Str. Lalelelor nr. 12, bl. A3, sc. 2, ap. 14` | „strada Lalelelor numărul doisprezece, blocul A3, scara doi, apartamentul paisprezece” |
| P-12 | `15%`, `a 2-a`, `al 3-lea` | „cincisprezece la sută”, „a doua”, „al treilea” |
| P-13 | Te întorci în meniuri și asculți anunțurile cu numere | acum HASI spune numerele în cuvinte: „patru opțiuni”, „unu din patru”, „douăsprezece opțiuni”. Spune-mi dacă sună mai bine sau mai rău decât înainte. |
| P-14 | În meniul principal, ții apăsată săgeata jos | opțiunile, apoi o singură dată [sunet] „Sfârșitul listei. Ieșire, 4 din 4.” |

Opțional: dacă ai aspectul de tastatură „Română (moștenit)”, care scrie ş/ţ cu sedilă, scrie `ţară` și apasă Enter: pe ecran, la „Rostit”, trebuie să apară „țară”, cu virgulă.

Rezultat (raportul din 23 octombrie 2026): toate au trecut; diacriticele ajung corect în câmp. Alegeri: orele întregi fără „fix”; „21:20” rămâne „ora douăzeci și unu și douăzeci de minute”. Preferința la P-13 nu a fost completată în raport.

## Etapa 0.4 bis – corecturi, acronime, tastatura numerică (A-01…A-03, N-01…N-05)

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| A-01 | Test pronunție: `14:00`, `ora 2:00`, `1:00` | „ora paisprezece”, „ora două”, „ora unu” (fără „fix”) |
| A-02 | Test pronunție: `Bilet CFR la STB și RATB` | „Bilet ce fe re la se te be și ra te be” |
| A-03 | Test pronunție: `Declarația la ANAF, CNP-ul meu` | „Declarația la Anaf, ce ne pe-ul meu” |
| N-01 | În Test pronunție, cu Num Lock **pornit**, apeși 1, 2, 3 de pe tastatura numerică | „1”, „2”, „3” (se scriu cifre) |
| N-02 | Apeși Num Lock (îl oprești) | „Tastatura numerică: navigare.” |
| N-03 | Cu Num Lock **oprit**, apeși 8 de pe tastatura numerică | „Textul scris: …” (săgeata sus) |
| N-04 | Apeși din nou Num Lock | „Tastatura numerică: cifre.” |
| N-05 | Te întorci în meniu (Escape) și apeși 2 și 8 de pe tastatura numerică, cu Num Lock pornit | navigare, ca înainte; apăsarea pe Num Lock în meniu nu spune nimic |

Acronimele se editează în `Data\acronime.txt`, lângă `hasi.exe` (în sursă: `src\Hasi.Text.Ro\Data\acronime.txt`).

## Etapa 0.5 – motoarele de voce și lanțul de rezervă (V-01…V-05)

| ID | Cum pornești / ce faci | Ce trebuie să auzi |
|---|---|---|
| V-01 | `dotnet run --project src\Hasi.App -- --diagnostic` | ca până acum, cu Paul; în jurnal: `ENGINE sapi5 voice="Paul"` |
| V-02 | `dotnet run --project src\Hasi.App -- --diagnostic --engine onecore` | HASI vorbește cu Microsoft Andrei prin motorul OneCore (fără mesaj despre Paul); navighează 1–2 minute și urmărește dacă oprirea vorbirii la apăsarea unei taste e la fel de rapidă ca la Paul |
| V-03 | `dotnet run --project src\Hasi.App -- --diagnostic --test-voice-failure`, apoi apeși săgeata jos de 3–4 ori | după al treilea mesaj, vocea principală „se strică”: „Vocea Paul nu mai funcționează. Folosesc vocea …”, urmat de ultimul mesaj, repetat cu vocea nouă; apoi totul continuă normal |
| V-04 | Ca la V-03, apoi F2 | ultimul mesaj, cu vocea nouă |
| V-05 | `--engine onecore --rate -3` | Andrei vorbește mai rar |

## Etapa 0.5 bis – al doilea experiment RHVoice.dll (R-01…R-12)

Cele două variante, din folderul proiectului:

- **A (SAPI, implicit):** `dotnet run --project src\Hasi.App -- --diagnostic --rhvoice-path "C:\Users\cucua\AppData\Local\HASI\rhvoice-test"`
- **B (RHVoice direct):** `dotnet run --project src\Hasi.App -- --diagnostic --engine rhvoice-dll --rhvoice-path "C:\Users\cucua\AppData\Local\HASI\rhvoice-test"`

`--rhvoice-path` e necesar în ambele variante pentru ecranul „Comparare voci”.

| ID | Ce faci | Ce urmărești |
|---|---|---|
| R-01 | Navighezi 2–3 minute prin meniuri cu B, apoi la fel cu A | aceeași voce Paul; spune-mi orice diferență de timbru, volum, viteză sau claritate |
| R-02 | Setări → Diagnostic → Comparare voci; Enter pe fiecare din cele 5 fraze | „Varianta unu, Paul prin SAPI. …”, apoi „Varianta doi, Paul prin RHVoice direct. …”; săgeata stânga = doar varianta 1, săgeata dreapta = doar varianta 2; orice tastă oprește |
| R-03 | Cu B: F1 (mesaj lung), apoi săgeata jos în timpul vorbirii; de 10 ori | oprire imediată, fără rest din mesajul vechi |
| R-04 | Cu B: apeși Control de mai multe ori în timpul vorbirii; asculți și începutul fiecărui mesaj | nicio pocnitură sau „clic” la oprire ori la pornire |
| R-05 | Căști Bluetooth ca dispozitiv implicit; pornești B, aștepți 30 de secunde fără să apeși nimic, apoi săgeata jos | prima silabă („Î” din „Învață tastele”) se aude întreagă. Repetă cu A și compară. |
| R-06 | Cu B, în timpul unui mesaj lung, scoți căștile (sau schimbi dispozitivul implicit din Windows) | vorbirea continuă pe dispozitivul nou, fără blocaj; următoarea tastă funcționează |
| R-07 | Cu B deschis, pui calculatorul în repaus, îl trezești, apoi săgeata jos | HASI vorbește normal |
| R-08 | B cu `--rate -5`, apoi `--rate 5`, `--pitch 3`, `--volume 50` | mai rar, mai repede, ton mai înalt, mai încet. A: viteza și volumul se schimbă, tonul nu (SAPI nu are ton). |
| R-09 | B cu `--rhvoice-path C:\nu-exista` | „Motorul de voce cerut nu a pornit. Folosesc vocea Paul. Detaliile sunt în jurnalul de diagnostic.” |
| R-10 | După o sesiune cu B, deschizi jurnalul de diagnostic | rânduri `LATENCY … first-sound`, `LATENCY … stop`, `WORD …` (cuvintele citite) și `PERF …` (procesor, memorie) |
| R-11 | Cu Num Lock **oprit**, în meniu: 5 de pe tastatura numerică | același efect ca Enter |
| R-12 | Test pronunție: `2 autobuze`; apoi `2 smartfoane` | „două autobuze” (din dicționar); „doi smartfoane” (gen ghicit, greșit) și în jurnal `LEXICON? smartfoane`. Adaugi `smartfoane => f` în `src\Hasi.Text.Ro\Data\genuri.txt`, repornești: „două smartfoane”. |
