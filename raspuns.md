# HASI – Răspuns după etapa 0.4

Etapa 0.4 e gata: normalizatorul românesc și ecranul „Test pronunție”. Toate cele **132 de teste automate** trec:

| Proiect de test | Teste | Ce verifică |
|---|---|---|
| Hasi.Core.Tests | 106 | nucleul |
| Hasi.Text.Ro.Tests | 7 | fișierele de cazuri, cu peste 150 de cazuri de pronunție |
| Hasi.Transcripts.Tests | 10 | transcrierile de referință |
| Hasi.Architecture.Tests | 9 | regulile de arhitectură |

Mă opresc aici, cum ai cerut, până primesc raportul tău.

---

## 1. Ce am făcut din raportul tău

- **Regulile noi** sunt în CLAUDE.md, la „Non-negotiables”: fără taste sau input simulat pe desktop fără aprobarea ta pentru fiecare rulare, și întreb înainte de orice schimbare de plan. În etapa asta n-am trimis nicio tastă. Am pornit HASI o singură dată, 3 secunde, fără input, ca să verific în jurnal că vorbirea trece prin normalizator.
- **Jurnalul de diagnostic.** În raport a rămas textul „[cale fișier]” în loc de cale, dar am găsit sesiunile tale din 23 octombrie în `%LOCALAPPDATA%\HASI\diagnostic`. Nu apar erori, iar Paul e detectat corect. Am găsit o singură problemă: ținând săgeata jos pe ultima opțiune, „Sfârșitul listei” și sunetul se repetau de 12 ori în 0,4 secunde. **Corectat**: mesajul se spune acum o singură dată (scenariul P-14).
- **„Problemele raportate” și „cazurile de pronunție de mai sus”** lipsesc din mesaj: toate scenariile au trecut, iar mesajul nu conține nicio listă de pronunții greșite. Dacă ai notat pronunții greșite din etapa 0.3, trimite-mi-le, sau adaugă-le direct (vezi secțiunea 4).
- **GitHub: nu am putut crea depozitul.** `gh` nu e autentificat pe calculator. Rulează în acest terminal:
  ```
  ! gh auth login
  ```
  (alegi GitHub.com, HTTPS, autentificare din browser). Apoi creez depozitul public `AllxCucu/hasi`, trimit toate cele 13 commit-uri și verific că CI-ul trece pe GitHub.

---

## 2. Cum pornești

La fel ca înainte:

```
dotnet run --project src\Hasi.App -- --diagnostic
```

**Drumul până la Test pronunție:** Meniu principal → Setări (Enter) → End → săgeata sus („Diagnostic, 11 din 12”) → Enter → Enter pe „Test pronunție”.

Ce face ecranul:
- **scrii** cu tastatura, inclusiv ă, î, â, ș, ț. Fiecare caracter e rostit, iar semnele de punctuație au nume („virgulă”, „punct”, „spațiu”);
- **Backspace** șterge ultimul caracter și spune ce a șters; ținut apăsat, șterge în continuu;
- **săgeata sus sau jos** citește tot textul scris;
- **Enter** rostește textul exact cum îl spune HASI. Pe ecran apar două rânduri, „Scris: …” și „Rostit: …”. Textul rămâne în câmp, ca să-l poți corecta și asculta din nou;
- **Escape** te întoarce în meniu. F1, F2 și F3 funcționează ca peste tot.

În jurnalul de diagnostic, fiecare Enter apare cu `text=` (ce ai scris), `normalized=` (ce a produs normalizatorul) și `SPEAK` (exact ce a primit Paul).

---

## 3. Scenariile pentru 0.4, inclusiv M0-33

Sunt și în `docs/manual-tests/faza-0.md`. Scrie fiecare text singur în câmp; între teste golești câmpul ținând Backspace apăsat.

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| P-01 | Intri în Test pronunție | „Test pronunție. Scrieți un text și apăsați Enter ca să-l auziți așa cum îl rostește HASI. Escape vă întoarce.” |
| P-02 | Scrii `abc`, apoi Backspace | „a”, „b”, „c”, apoi „Șters: c” |
| P-03 | Scrii `ș ț ă î â` | fiecare literă, iar spațiile ca „spațiu” |
| P-04 | Enter cu câmpul gol | „Câmpul este gol. Scrieți mai întâi un text.” |
| P-05 | Săgeata jos după ce ai scris ceva | „Textul scris: …” |
| **M0-33** | `Plata de 12,50 lei pe 1.03.2026 la 14:30`, apoi Enter | „Plata de doisprezece lei și cincizeci de bani pe întâi martie două mii douăzeci și șase la ora paisprezece și treizeci de minute”. Pe ecran: ambele forme. |
| P-06 | `1 leu`, `21 lei`, `0,50 lei` | „un leu”; „douăzeci și unu de lei”; „cincizeci de bani” |
| P-07 | `21.10.2026`, `1 mai` | „douăzeci și unu octombrie două mii douăzeci și șase”; „întâi mai” |
| P-08 | `ora 9:05`, `21:20` | „ora nouă și cinci minute”; „ora douăzeci și unu și douăzeci de minute” |
| P-09 | `2 ore`, `12 ore`, `2 ani`, `20 de oameni` | „două ore”, „douăsprezece ore”, „doi ani”, „douăzeci de oameni” |
| P-10 | `tel. 0722 123 456` | „telefon zero șapte doi doi, unu doi trei, patru cinci șase” |
| P-11 | `Str. Lalelelor nr. 12, bl. A3, sc. 2, ap. 14` | „strada Lalelelor numărul doisprezece, blocul A3, scara doi, apartamentul paisprezece” |
| P-12 | `15%`, `a 2-a`, `al 3-lea` | „cincisprezece la sută”, „a doua”, „al treilea” |
| P-13 | Asculți din nou meniurile | numerele sunt acum spuse de HASI în cuvinte: „patru opțiuni”, „unu din patru”, „douăsprezece opțiuni”. Înainte le citea Paul singur. Spune-mi care variantă sună mai natural. |
| P-14 | În meniul principal, ții apăsată săgeata jos | opțiunile, apoi **o singură dată** [sunet] „Sfârșitul listei. Ieșire, 4 din 4.” |

**Formulări pe care am ales să le validezi tu după ureche:**
- „21:20” devine „ora douăzeci și unu **și** douăzeci de minute”. Cei doi „și” pot suna greoi. Alternativa ar fi „ora douăzeci și unu, douăzeci de minute”.
- „14:00” devine „ora paisprezece **fix**”.
- „1 din 4” devine „**unu** din patru”, iar „1 opțiune” devine „**o** opțiune”.
- „101 pagini” devine „o sută una pagini”, fără „de”, după regula CLDR a pluralelor; o parte dintre vorbitori ar spune „o sută una de pagini”.

---

## 4. Cum îmi raportezi o pronunție greșită

Varianta cea mai simplă: îmi trimiți textul scris și ce ai auzit.

Varianta directă: deschizi un fișier din `tests\Hasi.Text.Ro.Tests\Cases\` (de exemplu `02-numere.cases`) și adaugi un rând de forma:

```
ce e scris => ce trebuie rostit
```

Rândurile care încep cu `#` sunt comentarii. Testul va pica, iar eu corectez normalizatorul până trece. Dacă `normalized=` din jurnal e corect, dar Paul tot pronunță greșit, problema e a vocii, nu a normalizatorului; spune-mi și așa.

---

## 5. Ce nu funcționează încă sau ce am lăsat deoparte

| Ce | Detalii |
|---|---|
| Ecoul fiecărui caracter tastat | Fix, doar în câmpul de text. Nu e o mutare de etapă: un câmp de text fără ecou nu poate fi folosit de un nevăzător. Setarea globală „Ecoul tastelor” (caracter / cuvânt / niciunul) rămâne în etapa 0.6. Dacă vrei altfel, spune-mi. |
| Cifrele de pe tastatura numerică | Nu scriu cifre în câmpul de text, pentru că rămân taste de navigare. Pentru cifre folosește rândul de cifre de deasupra literelor. |
| Lipirea textului (Control V) | Nu există. Textul se scrie doar de la tastatură. |
| Acronimele (CFR, ANAF, SRL) | Le las vocii, fără să le spun literă cu literă. Spune-mi cum le citește Paul. |
| Cifrele romane („secolul XX”) | Nu le transform încă. |
| „1 mai” | E citit mereu „întâi mai” (dată), chiar și în propoziții rare ca „1 mai mult”. |
| Genul substantivelor | Vine dintr-un lexicon mic, plus terminațiile tipice de plural. Un substantiv necunoscut după un număr poate primi forma masculină („doi”) și poate pierde „de”. Fiecare caz pe care îl găsești ajunge în tabele. |
| Scrierea efectivă în fereastră | Nu am testat-o eu, pentru că regula nouă interzice inputul simulat. Logica e acoperită de teste, dar primul test real al tastării (mai ales ș, ț, ă, î, â din aspectul românesc) e al tău, la P-02 și P-03. Dacă literele nu ajung în câmp, trimite-mi jurnalul: rândurile `KEY` și `CHAR` arată exact ce s-a primit. |
| Diagnostic → „Mod de diagnostic” și „Copiază jurnalul pe Desktop” | Spun încă „va fi disponibilă” (etapa 0.8). |
| Restul | Ca în lista de la etapa 0.3: setările (0.6), „Învață tastele” (0.7), coexistența cu NVDA (0.9), tasta globală și instanța unică (0.10), installer-ul (0.11). |
