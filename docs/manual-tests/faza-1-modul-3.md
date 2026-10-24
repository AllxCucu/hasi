# Faza 1, modulul 3: Citire documente

Pornești din terminal: `dotnet run --project src\Hasi.App -- --diagnostic`. Numerele se aud în cuvinte.
[sunet] înseamnă sunetul scurt de la capătul listei.

**Pregătire (folosești fișierele tale):**
- un document Word (`.docx`), cu titluri dacă se poate;
- un PDF cu text, de mai multe pagini (de preferat unul românesc, cu diacritice);
- un PDF scanat (doar imagini), și, dacă ai, unul cu prima pagină text și alte pagini scanate;
- o carte `.epub` (de preferat una mare, peste 5 MB, pentru scenariul de deschidere în fundal);
- un fișier `.txt`;
- dacă ai: un PDF sau un EPUB protejat cu parolă sau DRM.
- Pune-le în Documente sau pe Desktop. Nu le modifică HASI; doar le citește.
- Pentru scenariile cu NVDA (I), pornești NVDA cu aranjamentul de tastatură „desktop”.

## A. Meniul principal și meniul modulului

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-01 | (pornire), apoi săgeata jos de 6 ori | „Bun venit în HASI. Meniu principal. 7 opțiuni. Selectat: Ceas și calendar, 1 din 7.” Apoi: „Editor de text, 2 din 7.”, „Citire documente, 3 din 7.”, „Setări, 4 din 7.”, „Învață tastele, 5 din 7.”, „Despre HASI, 6 din 7.”, „Ieșire, 7 din 7.” |
| C3-02 | Home; săgeata jos de 2 ori; Enter; F1 | „Citire documente. 2 opțiuni. Selectat: Deschide un document, 1 din 2.”; ajutorul se termină cu „Documente recente arată ultimele documente citite, cel mai nou primul; fiecare se deschide de unde ați rămas.” |
| C3-03 | End; Enter (dacă n-ai citit încă niciun document) | „Documente recente, 2 din 2.”, apoi „Nu aveți documente recente.” |

## B. Deschiderea fiecărui format

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-04 | Home; Enter pe Deschide un document; Enter pe Documente | lista locurilor, apoi documentele. Fiecare fel are numele lui: „…, document Word”, „…, document PDF”, „…, carte EPUB”, „…, text”. |
| C3-05 | Deschizi documentul `.docx` | titlul documentului, apoi primul rând. |
| C3-06 | Deschizi PDF-ul cu text | titlul, apoi primul rând de pe prima pagină. Dacă unele pagini sunt scanate, o singură dată: „Pagina … este o imagine scanată.” sau „Paginile …, … și … sunt imagini scanate.” |
| C3-07 | Deschizi PDF-ul scanat (doar imagini) | „Documentul conține doar imagini scanate. HASI nu poate citi încă imagini.” Documentul nu se deschide; rămâi în listă. |
| C3-08 | Deschizi cartea `.epub` | titlul cărții și primul rând. |
| C3-09 | Deschizi fișierul `.txt` | titlul și primul rând. |
| C3-10 | Deschizi un fișier PDF sau EPUB mare (peste 5 MB); numeri în gând | dacă durează peste o secundă: „Se deschide documentul…”; apoi documentul se deschide singur. În timpul așteptării poți apăsa Escape: te întorci în listă, fără să se deschidă documentul. |
| C3-11 | (dacă ai) Deschizi PDF-ul sau EPUB-ul protejat | „Documentul este protejat și nu poate fi deschis.” |

## C. Cititul cu tastele

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-12 | Într-un document: săgeata dreapta și stânga de câteva ori; Control + săgeata dreapta; Control + săgeata stânga | litera de la cursor; apoi cuvântul de la cursor, de fiecare dată |
| C3-13 | Săgeata jos; săgeata sus; Home; End | rândul de la cursor, citit întreg; începutul rândului; sfârșitul rândului |
| C3-14 | Control + End; Control + Home | sfârșitul documentului; începutul documentului |
| C3-15 | Cu Num Lock oprit, pe tastatura numerică: 6, 4, 2, 8 | la fel ca săgețile: literă, literă, rând, rând |
| C3-16 | F1 în document | ajutorul cititorului, cu tastele de mai sus; Escape te întoarce în document |

## D. Citește tot

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-17 | Home la început de document; Enter | HASI citește de la cursor, propoziție cu propoziție, fără pauze lungi între propoziții |
| C3-18 | În timpul citirii, apeși o tastă (de exemplu săgeata dreapta) | vorbirea se oprește imediat și HASI **nu spune nimic**. Cursorul rămâne la **începutul propoziției** întrerupte (apeși Home sau Control + săgeata stânga și auzi începutul ei). |
| C3-19 | Apeși Enter din nou | HASI reia de la începutul propoziției care a fost întreruptă, nu de la cuvânt |
| C3-20 | Lași citirea până la capăt | la sfârșit se oprește singură, fără mesaj de eroare |
| C3-21 | Tasta 5 de pe tastatura numerică (Num Lock oprit) | la fel ca Enter: începe „Citește tot” |
| C3-22 | F10 (sau Control + M); Enter pe Citește tot | „Meniul documentului. 4 opțiuni. Selectat: Citește tot, 1 din 4.” (în `.docx`, `.txt` și `.epub` meniul nu are „Mergi la pagina…”: „Meniul documentului. 3 opțiuni. Selectat: Citește tot, 1 din 3.”); citirea pornește |

## E. Titluri, pagini, capitole

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-23 | În `.docx` sau `.epub`: H de câteva ori; Shift + H | „Titlu 2: …” (nivelul titlului) pentru titlul următor; Shift + H, titlul anterior. La capăt: „Nu mai există titluri după aici.” sau „Nu mai există titluri înainte de aici.” |
| C3-24 | În PDF: Page Down de 2 ori; Page Up | „Pagina 2 din …. …”, „Pagina 3 din …. …”, apoi „Pagina 2 din …. …”. Pe o pagină scanată: „Pagina … este o imagine scanată. HASI nu poate citi încă imagini.” |
| C3-25 | În PDF: Control + Home, Page Up; apoi Control + End, Page Down | „Aceasta este prima pagină.”; „Aceasta este ultima pagină.” |
| C3-26 | În EPUB: Page Down; Page Up | „Capitolul 2: …. …”; apoi capitolul 1. La capete: „Acesta este primul capitol.” / „Acesta este ultimul capitol.” |
| C3-27 | În PDF sau EPUB, Citește tot din mijlocul unei pagini sau al unui capitol, până treci în următoarea | când citirea trece mai departe: „Pagina 5.” sau „Capitolul 3.”, apoi textul |
| C3-28 | În PDF: F10; Mergi la pagina…; Enter; scrii un număr prea mare; Enter; Backspace; scrii un număr bun; Enter | „Mergi la pagina. Scrieți numărul paginii, de la 1 la …, și apăsați Enter.”; la număr greșit „Scrieți un număr între 1 și ….”; la număr bun „Pagina … din …. …” |

## F. Poziția și documentele recente

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-29 | În document: F10; Spune poziția; Enter | „Rândul … din ….” și, în PDF, „Pagina … din ….”, în EPUB „Capitolul … din ….” |
| C3-30 | Ajungi la o pagină din mijloc (PDF) sau la un capitol (EPUB); aștepți 10 secunde; Escape; Documente recente; Enter pe document | în listă: „…, pagina …”; la deschidere: „Continui de la pagina ….” (la EPUB „Continui de la capitolul ….”; la `.docx` și `.txt` „Continui de unde ați rămas.”) |
| C3-31 | Închizi documentul cu F10, Închide documentul; redeschizi din Documente recente | aceeași poziție. (Alt + F4 chiar după ce ai mutat cursorul poate pierde ultimele secunde: poziția se salvează la 5 secunde și la ieșire.) |
| C3-32 | Muți sau redenumești documentul, apoi îl alegi din Documente recente | „Documentul nu mai există în locul în care era.” |

## G. Cele patru verificări ale vocii la citirea continuă

La fiecare variantă, în Setări → Voce, pui vocea, apoi citești un PDF sau un EPUB cu Enter (Citește tot), cel puțin 2 minute.
Ascultă două lucruri: ultimul cuvânt al fiecărei propoziții nu se taie, iar între propoziții nu apar pauze lungi.

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-33 | Voce: „Paul, RHVoice direct”; Citește tot | ultimul cuvânt al fiecărei propoziții se aude întreg; pauzele sunt scurte |
| C3-34 | Voce: „Paul, prin SAPI”; Citește tot | la fel |
| C3-35 | Voce: Microsoft Andrei; Citește tot | la fel |
| C3-36 | (dacă ai căști Bluetooth) Aceeași citire, cu sunetul pe căști | la fel; dacă începutul primei propoziții se taie, notează-o |
| C3-37 | Lași HASI tăcut peste un minut (fără să apeși nimic); apoi Enter (Citește tot) | prima propoziție se aude întreagă, fără să-i lipsească începutul. (Fluxul audio se închide după un minut de tăcere.) |

## H. Confidențialitate

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-38 | Deschizi un document, citești câteva propoziții cu Citește tot, notezi un cuvânt neobișnuit din ele; închizi HASI; deschizi ultimul `sesiune-*.log` din `%LOCALAPPDATA%\HASI\diagnostic` și cauți cuvântul | nu găsești cuvântul nicăieri în jurnal (nici în `SAY`, nici în `SPEAK`). Nici titlul documentului nu apare în clar. Tastele (Enter, Page Down) rămân vizibile. |

## I. NVDA

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-39 | Cu NVDA pornit (aranjament desktop), într-un document deschis: Enter; apoi o tastă; H; Page Down | **o singură voce** (cea a HASI); NVDA nu citește în paralel. Enter citește tot; o tastă oprește; H și Page Down fac ce scrie mai sus. Dacă o tastă nu ajunge la HASI, notează-o. |
| C3-40 | Cu NVDA pornit și Num Lock oprit: tasta 5 de pe tastatura numerică | pornește „Citește tot”. Dacă NVDA o ia pentru el și HASI nu aude nimic, notează asta. |

## J. Retestare modul 2: Editorul

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C3-41 | Editor de text → Document nou; scrii trei rânduri; Control + Home; F10 | „Meniul documentului. 5 opțiuni. Selectat: Salvează, 1 din 5.”; apoi „Salvează o copie, 2 din 5.”, „Citește tot de la cursor, 3 din 5.”, „Spune poziția, 4 din 5.”, „Închide documentul, 5 din 5.” |
| C3-42 | Enter pe Citește tot de la cursor; apoi, în timpul citirii, apeși o tastă | HASI citește rândurile de la cursor, propoziție cu propoziție; orice tastă oprește citirea și nu spune nimic |
| C3-43 | Cursorul la mijlocul documentului: F10, Citește tot de la cursor | citește de la cursor până la sfârșit, nu de la început |
| C3-44 | Document nou, scrii „Primul”; Control + S, numele `Unu`; Document nou, scrii „Al doilea”, **fără să salvezi**; aștepți 35 de secunde; în al doilea terminal `taskkill /f /im hasi.exe` | HASI tace imediat |
| C3-45 | Repornești HASI | Cu un singur document nesalvat: „Am găsit un document nesalvat: …, din …, ora ….” Cu două documente nesalvate (repetă C3-44 de două ori, cu texte diferite, fără să salvezi): „Am găsit 2 documente nesalvate. Selectat: …, din …, ora …, 1 din 2.” |
| C3-46 | Pe lista cu două ciorne: Enter pe prima; Deschide documentul | documentul apare cu textul scris |
| C3-47 | Pe lista de ciorne (dacă ai ciorne păstrate, altfel repetă C3-44 și C3-45): Enter pe una; Șterge ciorna; Enter | „Documentul nesalvat se va pierde.”, apoi „Ciorna a fost ștearsă.”; cealaltă rămâne în listă |
| C3-48 | Pe lista de ciorne: Escape | „mai târziu”: toate ciornele se păstrează și apar din nou la următoarea pornire |
| C3-49 | Ieșire → Închide HASI → Enter; după „La revedere.” apeși repede câteva taste | după „La revedere.” tastele sunt ignorate: nu auzi nimic nou, iar HASI se închide |

## Lista de regresie

`docs/manual-tests/regresie.md`: pașii 1–5, 14, 28 și 29 s-au schimbat pentru meniul principal cu 7 opțiuni și
pentru Despre HASI cu 15 rânduri. Pașii noi 51–55 (secțiunea L) sunt pentru acest modul.
