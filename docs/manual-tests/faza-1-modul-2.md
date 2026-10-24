# Faza 1, modulul 2: Editor de text

Pornești din terminal: `dotnet run --project src\Hasi.App -- --diagnostic`. Numerele se aud în cuvinte.
[sunet] înseamnă sunetul scurt de la capătul listei.

**Pregătire:**
- Ai nevoie de un document Word făcut de Word (nu de HASI), pe care să îl poți pierde fără grijă: pune o copie în
  Documente sau pe Desktop. Dacă ai un stick USB, introdu-l.
- Pentru scenariile cu NVDA (H), pornești NVDA cu aranjamentul de tastatură „desktop”.
- Documentele pe care le salvezi aici rămân în Documente; le poți șterge tu după test.

## A. Meniul principal și meniul editorului

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-01 | (pornire) | „Bun venit în HASI. Meniu principal. 6 opțiuni. Selectat: Ceas și calendar, 1 din 6.” Apoi săgeata jos de 5 ori: „Editor de text, 2 din 6.”, „Setări, 3 din 6.”, „Învață tastele, 4 din 6.”, „Despre HASI, 5 din 6.”, „Ieșire, 6 din 6.” |
| C2-02 | Home; săgeata jos; Enter; F1 | „Editor de text. 3 opțiuni. Selectat: Document nou, 1 din 3.”; ajutorul se termină cu „Documente recente arată ultimele documente deschise sau salvate, cel mai nou primul.” |
| C2-03 | End; Enter (dacă n-ai deschis încă niciun document) | „Documente recente, 3 din 3.”, apoi „Nu aveți documente recente.” |

## B. Scrii și citești într-un document nou

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-04 | Home, Enter pe Document nou | „Document nou. Documentul este gol.” |
| C2-05 | Scrii `Dragă Ana,`; Enter; scrii `Ce mai faci? Mă gândesc la tine.` | cu Ecoul tastelor pe „Fiecare caracter”, auzi fiecare literă; la Enter, „Rând nou” |
| C2-06 | Săgeata stânga de 3 ori, apoi săgeata dreapta de 2 ori | litera de la cursor, de fiecare dată |
| C2-07 | Control + săgeata stânga de 2 ori; Control + săgeata dreapta | cuvântul de la cursor, de fiecare dată |
| C2-08 | Săgeata sus; săgeata jos | rândul de la cursor, citit întreg: „Dragă Ana,” apoi „Ce mai faci? Mă gândesc la tine.” La un rând gol auzi „Rând gol”. |
| C2-09 | Home; End | începutul rândului; sfârșitul rândului („Sfârșit de rând”) |
| C2-10 | Control + Home; Control + End | „Începutul documentului.”; „Sfârșitul documentului.” |
| C2-11 | Backspace de 2 ori; Delete la începutul unui rând | „Șters: …” cu litera ștearsă, de fiecare dată; la Backspace la început de rând, „Șters: rând nou” |
| C2-12 | Control + Z de 2 ori; Control + Y | „Anulat. …” cu rândul de acum, de fiecare dată; „Refăcut. …”. La capăt: „Nu este nimic de anulat.” sau „Nu este nimic de refăcut.” |
| C2-13 | F1 în document | ajutorul cu tastele, terminat cu „Escape iese din document.” |

## C. Ecoul tastelor

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-14 | Setări → Ecoul tastelor → „Fiecare cuvânt”; înapoi în editor, Document nou; scrii `bună ziua ` | nu auzi literele; auzi cuvântul „bună”, apoi „ziua”, când apeși spațiul |
| C2-15 | Setări → Ecoul tastelor → „Fără ecou”; în document scrii câteva litere, apoi Backspace | nu auzi nimic la scris; la Backspace auzi tot „Șters: …” (ștergerea se anunță mereu). Pui la loc „Fiecare caracter”. |

## D. Salvarea

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-16 | Într-un document nou, scrii `Dragă Ana,` Enter `Ce mai faci?`; Control + S; Enter fără să scrii nimic | „Numele documentului. Scrieți numele documentului și apăsați Enter. Dacă apăsați Enter fără să scrieți, se folosește numele propus: Dragă Ana.”; apoi „Documentul a fost salvat: Dragă Ana.” În Documente apare `Dragă Ana.docx`. |
| C2-17 | Document nou, scrii `Rețeta de prăjitură`; Control + S; scrii numele `Rețete`; Enter | „Documentul a fost salvat: Rețete.” |
| C2-18 | Document nou, scrii ceva; Control + S; scrii `Rețete`; Enter; apoi Escape | „Există deja un document cu numele Rețete. Îl înlocuiți? Apăsați Enter pentru da sau Escape pentru alt nume.”; după Escape revii la nume și poți scrie alt nume. Nimic nu s-a înlocuit. |
| C2-19 | Același lucru, dar la întrebare apeși Enter | documentul vechi se înlocuiește; „Documentul a fost salvat: Rețete.” |
| C2-20 | F10 sau Control + M | „Meniul documentului. 4 opțiuni. Selectat: Salvează, 1 din 4.”; apoi: „Salvează o copie, 2 din 4.”, „Spune poziția, 3 din 4.”, „Închide documentul, 4 din 4.” Enter pe Spune poziția: „Rândul … din …, coloana ….” Escape te întoarce în document. |

## E. Deschiderea documentelor

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-21 | Închizi documentul (Escape, fără modificări), apoi în Editor de text: Documente recente, Enter | „Documente recente. … document(e). Selectat: …, 1 din …” cu ultimul document salvat primul; Enter îl deschide și spune titlul și primul rând. |
| C2-22 | Editor de text: Deschide un document, Enter | lista locurilor: „Documente”, „Desktop”, „Descărcări” și, dacă ai stick, „Stick USB, …, … gigaocteți liberi”. Enter pe Documente: întâi dosarele, „Dosarul …”, apoi documentele: „Rețete, document Word”. Escape te duce un pas înapoi. |
| C2-23 | Pui în Documente un fișier `.txt`; îl deschizi din Deschide un document | în listă: „…, text”; Enter îl deschide; în document: litere, cuvinte, rânduri ca la C2-06…C2-09. |
| C2-24 | Deschizi documentul Word făcut de Word; citești; adaugi o frază; Control + S | „Documentul original are formatare pe care HASI nu o poate păstra. Îl salvez ca document nou, iar originalul rămâne neschimbat.”; apoi numele propus, „… – HASI”; Enter: „Documentul a fost salvat: … – HASI.” Deschis în Word, **originalul** este exact cum era; copia are textul nou. |
| C2-25 | Într-un loc gol (de exemplu un dosar fără documente): Deschide un document, intri | „… Nu există documente aici. Apăsați Escape pentru a vă întoarce.” |

## F. Copii

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-26 | Într-un document: F10, Salvează o copie, Enter; alegi „Text”; alegi Desktop; Enter pe numele propus | întâi formatul („Document Word” / „Text”), apoi locul, apoi numele; „Copia a fost salvată: …” Pe Desktop apare un `.txt`; documentul deschis rămâne cel de până acum. |
| C2-27 | (dacă ai stick) Același lucru, cu locul „Stick USB, …”, format „Document Word” | „Copia a fost salvată: …”; fișierul apare pe stick. Scoți stick-ul, apoi Control + S la o copie nouă pe stick: „Documentul nu a putut fi salvat. Locul nu mai există; poate stick-ul a fost scos. Textul este păstrat în HASI.” |

## G. Modificări nesalvate

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-28 | Deschizi un document salvat, scrii ceva, Escape | „Documentul are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și ieși, 1 din 3.” Enter: „Documentul a fost salvat.” și ești înapoi în „Editor de text” (de exemplu „Editor de text. Selectat: Documente recente, 3 din 3.”). |
| C2-29 | Același lucru; săgeata jos, Enter pe „Ieși fără să salvezi”; apoi Escape; apoi Enter | „Modificările se vor pierde.”; Escape te întoarce la cele 3 opțiuni; Enter iese fără să salveze. Redeschis, documentul nu are modificarea. |
| C2-30 | Același lucru; Escape pe întrebare (sau „Rămâi în document”) | te întorci în document, cu textul scris încă acolo |
| C2-31 | Cu modificări nesalvate într-un document: Alt + F4 | „Documentul … are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3.” (titlul documentului, dacă are unul). Enter salvează, apoi întreabă „Sigur doriți să închideți HASI?”; Escape: „Anulat.” |
| C2-32 | Același Alt + F4; „Continuă fără să salvezi”, Enter | „Modificările nesalvate se vor pierde.”; Enter, apoi întrebarea de închidere |
| C2-33 | Același Alt + F4; „Renunță” (sau Escape) | „Anulat.” Rămâi în document, cu textul neschimbat. |

## H. Ciorne, confidențialitate, taste globale, NVDA

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C2-34 | Pornești HASI normal (`dotnet run --project src\Hasi.App -- --diagnostic`); Document nou; scrii o frază; aștepți **35 de secunde** fără să salvezi; apoi, într-un al doilea terminal, `taskkill /f /im hasi.exe` (închide HASI brusc, ca o pană de curent) | HASI tace imediat, fără nicio întrebare. La următoarea pornire, după bun venit: „Am găsit un document nesalvat: …, din …, ora ….”, cu Deschide documentul / Șterge ciorna / Mai târziu. |
| C2-35 | Enter pe Deschide documentul | documentul apare cu textul scris; Control + S îl salvează. Repetă C2-34, apoi alegi „Șterge ciorna”: „Documentul nesalvat se va pierde.”, Enter: „Ciorna a fost ștearsă.” „Mai târziu”: ciorna se oferă din nou la pornirea următoare. |
| C2-36 | Document nou; scrii cuvântul `zmeură73` (un cuvânt care nu apare nicăieri altundeva); Control + S, numele `testsecret`; apoi închizi HASI și deschizi ultimul `sesiune-*.log` din `%LOCALAPPDATA%\HASI\diagnostic` | căutând `zmeură` și `testsecret` în jurnal nu găsești nimic; literele tastate apar ca `CHAR '*'`, rândurile `SAY` și `SPEAK` sunt mascate, iar titlurile apar ca `***`. Tastele de comandă (Enter, Control + S) rămân vizibile. |
| C2-37 | Într-un document: Control + T; apoi Control + H | „Este ora …”, fără să se schimbe documentul sau cursorul; apoi ajutorul documentului (la fel ca F1) |
| C2-38 | Cu NVDA pornit (aranjament desktop), într-un document cu 2–3 rânduri: Control + săgeata stânga și dreapta; Home; End; Control + Home; Control + End; F10; Control + M | tastele funcționează la fel ca fără NVDA și auzi **o singură voce** (cea a HASI); NVDA nu citește în paralel și nu fură F10 sau Control + M. Dacă auzi două voci sau o tastă nu face ce scrie aici, notează tasta. |

## Lista de regresie

`docs/manual-tests/regresie.md`: pașii 1–5, 14, 28 și 29 s-au schimbat pentru meniul principal cu 6 opțiuni și
pentru Despre HASI cu 12 rânduri. Pașii noi 46–50 (secțiunea K) sunt pentru acest modul.
