# HASI – Faza 1, modulul 2: Editor de text

**Stare:** codul e gata. Build-ul trece fără avertismente și toate cele 757 de teste trec. Rularea de verificare a trecut: 6 opțiuni în meniul principal, vocea RHVoice, nicio eroare. **Recenzia Codex pe nucleu nu a rulat încă**: pornește la 13:13, când i se ridică limita. Dacă găsește probleme, le corectez și îți spun înainte să testezi.

## Subagenții (`.claude/agents/`)
- `implementer`: scrie o bucată de cod după specificația mea. Rulează pe modelul principal, cel pe care îl folosesc și eu.
- `test-writer`: scrie transcrieri și teste. Rulează pe Sonnet.
- `docs-writer`: scrie scenarii, regresie, ghid și licențe. Rulează pe Sonnet.

Claude Code îi recunoaște după nume abia din sesiunea următoare. Până atunci i-am pornit ca agenți generali, cu regulile din fișierul fiecăruia.

## Ce poți testa
- **Pornire:** `dotnet run --project src\Hasi.App -- --diagnostic`.
- **Scenariile:** `docs/manual-tests/faza-1-modul-2.md`, de la C2-01 la C2-38. Regresia e în `docs/manual-tests/regresie.md`, pașii 1–50; pașii 46–50 sunt noi.
- **Meniul principal** are acum: Ceas și calendar, **Editor de text**, Setări, Învață tastele, Despre HASI, Ieșire.
- **Editorul** are trei opțiuni: Document nou, Deschide un document (.txt, .docx) și Documente recente. Tastele sunt cele din plan, §3.2.
- **Salvarea și ieșirea:**
  - Control S cere doar numele și propune primele cuvinte din text. Documentul se salvează ca .docx în Documente.
  - Un .docx scris în Word nu se modifică niciodată; HASI îl salvează ca document nou.
  - Cu modificări nesalvate, HASI te întreabă ce faci înainte de Escape, Alt+F4, repornire, oprire și actualizare.
  - Ciorna se salvează la 30 de secunde și ți se oferă la următoarea pornire.
- **Jurnalul de diagnostic** nu mai conține textul scris și nici numele documentelor.
- **Despre HASI** are un rând nou: Open XML SDK, sub licența MIT.

## Decizii luate de mine, de confirmat
1. **Operațiile în fundal** (deschidere și salvare cu oprire la cerere) le amân la modulul 3. Deschiderea și salvarea unui .txt sau .docx durează puțin; PDF-urile mari vor avea nevoie de ele.
2. **Profilul de taste pentru editare** l-am adus acum, nu la modulul 4, cum era în plan: editorul are nevoie de el pentru Home/End și pentru săgețile stânga și dreapta.
3. **O singură ciornă:** dacă lași o ciornă „pentru mai târziu” și lucrezi la alt document nesalvat, după 30 de secunde ciorna nouă o înlocuiește pe cea veche.
4. **Ordinea la Alt+F4:** după „Salvează și continuă”, HASI salvează, apoi întreabă „Sigur doriți să închideți HASI?”. Ajungi să confirmi de două ori, dar nimic nu se pierde din greșeală.
5. **Scrierea fișierelor:** .txt se scrie UTF-8 cu marcaj de început, ca să-l citească corect și programele vechi. Un rând gol de la sfârșitul unui .txt nu se păstrează.
6. **Alegerea fișierelor:** spațiul liber de pe stick se spune ca în Windows (de exemplu „15 gigaocteți liberi”). Folderele de sistem și fișierele ascunse nu apar.

## Ce nu e făcut
- **Recenzia Codex** pe nucleu (vezi sus).
- **Selecția, copierea, lipirea și căutarea** rămân în afara Fazei 1, cum scrie în plan.
- **„Citește tot”** apare în meniul documentului abia cu modulul 3.
