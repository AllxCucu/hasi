# Planul Fazei 1 – MVP util zilnic

> **Stare:** PROPUNERE (24 octombrie 2026), verificată independent cu Codex (§9). Nu scriu cod până nu aprobi
> planul. Fiecare modul are propria oprire pentru testul tău după ureche, cu scenariile lui „ce apăs / ce trebuie
> să aud” și câțiva pași noi în lista de regresie.

Cuprins:
0. Ce am verificat (nu presupun)
1. Ordinea modulelor
2. Ce se adaugă în `Hasi.Core` și unde
3. Modulele, unul câte unul
4. Mementourile când HASI e închis sau calculatorul doarme
5. Datele utilizatorului și dezinstalarea
6. Prima publicare (0.2.0)
7. Riscuri
8. Decizii pe care ți le cer
9. Ce s-a schimbat după verificarea cu Codex

---

## 0. Ce am verificat (nu presupun)

| Subiect | Constatare | Sursă |
|---|---|---|
| Sărbătorile legale | Codul muncii, **art. 139**, forma consolidată la **27 aprilie 2026** (ultima modificare a codului: OUG 32/2026, care nu atinge art. 139). Alin. (1): 1 și 2 ianuarie; 6 ianuarie, Boboteaza, și 7 ianuarie, Sfântul Ioan (Legea 52/2023, în vigoare din 9 martie 2023, deci prima dată aplicate în ianuarie 2024); 24 ianuarie; Vinerea Mare (Legea 64/2018); prima și a doua zi de Paști; 1 mai; 1 iunie (Legea 220/2016); prima și a doua zi de Rusalii; Adormirea Maicii Domnului; 30 noiembrie; 1 decembrie; prima și a doua zi de Crăciun; plus două zile pentru fiecare din cele 3 sărbători ale cultelor necreștine, doar pentru credincioșii lor. | legislatie.just.ro, Legea 53/2003 republicată (DetaliiDocument/128647) |
| Alin. (2^1) | Pentru salariații altor culte creștine, Vinerea Mare, Paștele și Rusaliile se acordă **după data cultului lor** (Legea 37/2020). Legea nu spune „ortodox”, deci HASI spune explicit „Paștele ortodox”, „Rusaliile ortodoxe”. | același text |
| Datele fără zi în lege | Legea numește „Adormirea Maicii Domnului” și „Crăciunul” fără dată. Biserica Ortodoxă Română le ține la **15 august** și **25–26 decembrie**. Le trec așa, cu această notă. | același text |
| Paștele ortodox | Algoritmul Meeus pentru calendarul iulian, plus 13 zile, **valabil doar 1900–2099**; calendarul HASI se limitează la acești ani. Calculat pentru 2024–2030 și potrivit cu datele publicate: 5 mai 2024, 20 aprilie 2025, 12 aprilie 2026, 2 mai 2027, 16 aprilie 2028, 8 aprilie 2029, 28 aprilie 2030. Rusaliile = Paștele + 49 și + 50 de zile; Vinerea Mare = Paștele − 2. **La implementare verific anii de test și pe un calendar oficial BOR**, nu doar pe presă. Atenție la suprapuneri: pe 1 iunie 2026 cad și Ziua Copilului, și a doua zi de Rusalii. | calcul local; presa (Alba24, EVZ) |
| `vpk` 1.2.161 | Are `--noPortable`. Arhiva portabilă nu se mai produce deloc. | `dotnet vpk pack -H` |
| .docx | **DocumentFormat.OpenXml 3.5.1** și dependența lui directă **DocumentFormat.OpenXml.Framework 3.5.1**, ambele MIT, de la .NET Foundation; plus `System.IO.Packaging` (MIT, .NET). Citire și scriere. | nuget.org, github.com/dotnet/Open-XML-SDK |
| .pdf | **PdfPig 0.1.16** (august 2026), Apache-2.0, compatibilă cu GPL-3.0. Conține cod derivat din PDFBox (Apache) și listele de glife și metricile celor 14 fonturi Adobe, cu licență permisivă: se distribuie cu notițele păstrate și cu fișierul `MustRead.html` (citit de mine în `src/UglyToad.PdfPig.Fonts/Resources/AdobeFontMetrics/`). | nuget.org, `NOTICES.txt` și `MustRead.html` din github.com/UglyToad/PdfPig |
| .epub | **VersOne.Epub 3.3.6**, Unlicense (domeniu public, compatibil GPL), fără dependențe: capitolele în ordinea de citire și cuprinsul. Textul din XHTML îl scot cu **HtmlAgilityPack 1.13.1** (MIT), fără să descarc nimic din afara cărții. | nuget.org, github.com/vers-one/EpubReader |
| Variante respinse | Docnet.Core (PDFium; ultima versiune în 2023, un DLL nativ în plus); `Windows.Data.Pdf` (doar desenează paginile); AngleSharp (MIT, mai mare decât ne trebuie). | nuget.org |
| Nucleul de azi | `ISpeechEngine.Speak` nu spune când s-a terminat sunetul unui mesaj. `Announcer` are doar `Interrupt` și `Queue`. Jurnalul de diagnostic scrie **fiecare caracter tastat** (`Shell.Dispatch`), înainte să știe ce ecran e. `ChoiceScreen` există deja. Ieșirea (Alt+F4), oprirea calculatorului și actualizarea închid HASI fără să întrebe ecranele. Ctrl+↑/↓, Home/End și Enter au deja sens global. Tastele / * − de pe tastatura numerică sunt Ajutor, Repetă, Unde sunt. Testul de arhitectură are lista modulelor scrisă de mână. | cod: `Shell.cs`, `Speech.cs`, `ValueScreen.cs`, `KeyMap.cs`, `DependencyRulesTests.cs` |

---

## 1. Ordinea modulelor

Păstrez ordinea ta. O singură dependență de reținut: mementourile au nevoie de „vorbirea anunță când a
terminat”, care apare la citirea documentelor (modulul 3), deci agenda trebuie să vină după el. Ordinea ta
respectă asta deja.

| # | Modul | De ce aici |
|---|---|---|
| 0 | Corecturile din 0.11 | Mici; le auzi la prima oprire, împreună cu modulul 1. |
| 1 | Ceas, dată și calendar | Mic. Verifică pe ceva simplu: un modul nou, un fișier de date, timpul simulat în teste. Aduce alegerea unei date, refolosită de agendă. |
| 2 | Editor de text | Aduce navigarea în text, alegerea fișierelor, salvarea sigură și locul datelor personale (§5). |
| 3 | Citire documente | Folosește navigarea din editor; adaugă formatele și „citește tot”. |
| 4 | Calculator | Mic, independent; cere un „profil de taste” (§3.4). |
| 5 | Agendă | Cere programatorul de evenimente și prioritatea de anunț (§4). |
| 6 | Manager de fișiere | Folosește alegerea fișierelor din modulul 2 și deschiderea documentelor din 3. |

---

## 2. Ce se adaugă în `Hasi.Core` și unde

**Regula:** în nucleu intră doar piese **generice** (fără reguli de agendă, de calcul sau de format), folosite de
cel puțin două module. Ce ține de Windows intră în `Hasi.Platform.Windows` sau în aplicație, în spatele unei
interfețe din nucleu. Ecranele tot nu ating vorbirea sau fereastra.

| Piesă nouă | Unde | Apare în modulul | Cine o mai folosește |
|---|---|---|---|
| `TimeProvider` (din .NET) și fusul orar în `IModuleContext` | Core | 1 | agenda, editorul |
| `DatePickerScreen`: alegi o zi cu săgețile (zi, lună, an), cu ce spune modulul despre fiecare zi | Core | 1 | agenda (data programării) |
| **Operații în fundal:** fiecare operație lungă (deschidere, salvare, copiere) are un identificator și poate fi oprită; rezultatul intră în bucla de evenimente și **se aruncă dacă utilizatorul a părăsit între timp ecranul care a cerut-o** | Core | 2 | documente, fișiere |
| **Lucru nesalvat:** un ecran poate spune „am modificări nesalvate”. Înainte de ieșire (și Alt+F4), de repornirea sau oprirea calculatorului și de actualizare, HASI întreabă toate ecranele deschise | Core | 2 | agenda (formular neterminat) |
| **Confidențialitate în jurnal:** ecranele de text și cele cu date personale se declară „sensibile”; atunci jurnalul de diagnostic maschează caracterele tastate, textul rostit și titlurile ecranelor (numele documentului, al contactului) | Core | 2 | documente, agendă |
| `TextBuffer` (text, rânduri logice, cursor, anulare și refacere) și `TextNavigator` (caracter, cuvânt, rând, început/sfârșit, ce se rostește la fiecare mutare) | Core | 2 | documente, agendă (adresa, nota) |
| `TextEditScreen` (mai multe rânduri, editabil) | Core | 2 | agendă |
| Comenzi de text noi în `KeyMap` (cuvânt, rând, început de document, citește tot, spune poziția, meniul ecranului), configurabile, cu alternative fără taste F | Core | 2 | documente |
| `IFileSystem` (interfață) + `FileBrowserScreen` (locuri: Documente, Desktop, Descărcări, stick-uri; dosare; fișiere filtrate pe tip) + `SaveAsScreen` | Core; discul, folderele Windows (Known Folders) și stick-urile în `Hasi.Platform.Windows` | 2 | documente, fișiere |
| `IDocumentLoader` / `IDocumentWriter` (interfețe) și un **model de document comun** (paragrafe, titluri, pagini, capitole) | Core; implementarea într-o bibliotecă nouă **`Hasi.Documents`** (pur .NET, ca `Hasi.Text.Ro`) cu OpenXml, PdfPig, VersOne.Epub, HtmlAgilityPack | 2 (.txt, .docx), 3 (.pdf, .epub) | editor, documente, fișiere, fără să depindă unul de altul |
| **Progresul vorbirii:** fiecare mesaj primește un identificator și un „proprietar”; canalul de vorbire raportează **terminat** (sunetul s-a auzit tot, nu doar sinteza), **întrerupt** sau **eșuat**; raportul intră în bucla de evenimente și ajunge doar la proprietar, dacă mai există. Și `FallbackSpeechEngine`, la schimbarea vocii, raportează corect | Core + cele 4 motoare din `Hasi.Speech` | 3 | agendă, editor (citește tot) |
| `TextReaderScreen` (doar citire, cu titluri, pagini, capitole) și „citește tot” | Core | 3 | editor (citește tot) |
| **Profil de taste:** un ecran poate cere ca anumite taste să aibă alt sens la el (tastele operațiilor în calculator), restul tastelor globale rămânând neschimbate | Core | 4 | – (generic, dar mic) |
| `Scheduler`: evenimente la o oră din calendar, calculate din ceas, nu temporizatoare care se pierd în repaus; aplicația îl „bate” la câteva secunde și îl anunță la revenirea din repaus și la schimbarea orei | Core; semnalele de repaus în aplicație | 5 | ceas (ora fixă, dacă o vrei) |
| Prioritatea **`Notification`**: așteaptă finalul mesajului curent, apoi vorbește, cu un sunet propriu; Ctrl și orice tastă o opresc ca pe orice mesaj (regula de oprire rămâne), dar notificarea rămâne „în așteptare” | Core | 5 | actualizări |
| `NotificationScreen`: vine peste orice ecran, se anunță ca orice ecran; când se închide, ecranul de dedesubt e exact cum era (cursorul din editor nu se mută) | Core | 5 | – |
| `IUserDataStore`: fișier JSON scris atomic (fișier temporar, apoi înlocuire), cu copii de siguranță zilnice | Core (logica) + aplicație (căile) | 2 (ciorne, documente recente) | documente (poziția de citire), agendă |
| `IFileHandlers`: un modul spune „deschid fișierele .docx pentru citire / pentru editare” și dă ecranul; aplicația adună lista | Core | 6 | fișiere → editor / documente |

**Rămân în module:** recurența programărilor, confirmarea mementourilor, regulile de calcul, regulile de salvare,
lista de sărbători. `ChoiceScreen` și `MenuScreen` existente acoperă alegerile cu mai multe variante; nu fac
un ecran nou pentru asta.

**Testul de arhitectură** primește toate modulele noi și `Hasi.Documents` (cu verificarea că rămâne pur .NET).
Adaug și un test care verifică că modulele nu apelează direct Windows (P/Invoke).

---

## 3. Modulele

Pentru fiecare modul: ecranele, ce rămâne în modul și ce se testează automat. Tastele exacte și formulările ți
le arăt la începutul fiecărui modul (în transcrierile de referință, unde le poți corecta), înainte de cod.

### 3.1 Ceas, dată și calendar (`Hasi.Modules.Clock`)

**Ecrane:**
- Meniul „Ceas și calendar”: Ora, Data, Calendar, Sărbători legale.
- **Ora:** „Este ora paisprezece și treizeci și două de minute.” (prin normalizatorul existent).
- **Data:** „Astăzi este sâmbătă, 24 octombrie 2026.” Dacă e sărbătoare legală: „… Sărbătoare legală: …”.
- **Calendar** (`DatePickerScreen` din nucleu): pornește pe azi. ↑/↓ = ziua anterioară/următoare,
  PageUp/PageDown = luna, Home/End și Ctrl+↑/↓ = prima/ultima zi a lunii. Fiecare zi: „Vineri, 1 mai 2026.
  Sărbătoare legală: Ziua Muncii.” Pe 1 iunie 2026 spune ambele sărbători.
- **Sărbători legale:** lista pe anul curent, începând cu următoarea; ←/→ schimbă anul (1900–2099).

**În modul:** fișierul de date `sarbatori-legale.txt`, editabil ca `acronime.txt`, cu **reguli**, nu cu date:
`fix 01-01`, `paste -2`, `paste +49`. Fiecare regulă are o **cheie de text** (numele rostit stă în `ro.json`, ca
toate textele), articolul din lege și data de la care se aplică (1 iunie din 2017, Vinerea Mare din 2018, 6–7
ianuarie din 2024). Dacă legea se schimbă, se schimbă fișierul și ajunge la utilizatori prin actualizare. Sursa
se trece în `docs/data-sources.md`. Zilele cultelor necreștine nu le calculez (depind de declarațiile cultelor),
dar le menționez în ajutor.

**Teste:** tabel 2024–2030 cu toate sărbătorile; ani bisecți; capetele 1900 și 2099; suprapuneri; ziua schimbării
orei (25 octombrie 2026, chiar mâine); transcrieri pentru fiecare ecran, cu ceasul fixat în test.

### 3.2 Editor de text (`Hasi.Modules.Editor`)

**Ecrane:**
- Meniul „Editor”: Document nou, Deschide un document (.txt, .docx), Documente recente (ultimele 10).
- **Editorul** (`TextEditScreen`): scrii cu ecoul ales în Setări.
  - ←/→ caracterul; Ctrl+←/→ cuvântul; ↑/↓ rândul; Home/End începutul/sfârșitul rândului; Ctrl+Home/End și
    Ctrl+↑/↓ începutul/sfârșitul documentului (același sens de „primul/ultimul” ca în meniuri).
  - Backspace și Delete spun ce au șters; Ctrl+Z anulează, Ctrl+Y reface; Enter face rând nou.
  - **„Rândul” e rândul logic**, cel dintre două apăsări de Enter, nu rândul de pe ecran, care depinde de mărimea
    textului. Auzi mereu același lucru, oricum ar fi afișat.
  - **F10 / Ctrl+M** deschide meniul editorului: Salvează, Salvează o copie, Citește tot de la cursor (după
    modulul 3), Spune poziția („rândul 3 din 10”), Închide documentul. Ctrl+S salvează direct.
  - Ctrl+H, Ctrl+R și Ctrl+U rămân Ajutor, Repetă și Unde sunt, ca peste tot.
- **Salvarea, cât mai simplă:**
  - un document nou primește la prima salvare doar o întrebare: numele (HASI propune primele cuvinte). Locul e
    Documente, formatul e .docx (îl deschide oricine din familie, și pe telefon); .txt și alt loc sunt în
    „Salvează o copie”;
  - salvarea e atomică (fișier temporar, apoi înlocuire): o pană de curent nu lasă un fișier pe jumătate;
  - dacă salvarea nu reușește (stick scos, disc plin): „Documentul nu a putut fi salvat. Textul este păstrat în
    HASI.”, și nu pleci din document fără să alegi ce faci.
- **Escape** iese din document. Cu modificări nesalvate: „Documentul are modificări nesalvate.” și un meniu cu
  trei variante: Salvează și ieși, Ieși fără să salvezi (cu confirmare), Rămâi în document. Aceeași întrebare
  apare la Alt+F4, la închiderea HASI, la oprirea calculatorului și înainte de o actualizare.
- **Ciornă automată** la 30 de secunde, în datele personale (§5), deci nu se pierde nici la dezinstalare. După o
  eroare gravă sau o pană de curent, HASI spune la pornire „Am găsit un document nesalvat…” și îl oferă.

**Siguranța fișierelor .docx, fără ghicit:** un .docx **făcut de HASI** se salvează peste el însuși. Orice alt
.docx (scris în Word, primit de la cineva) se deschide pentru editare ca **copie**: la salvare HASI propune
„Scrisoare – HASI.docx” și spune de ce („Documentul original are formatare pe care HASI nu o poate păstra.”).
Originalul nu se atinge niciodată.

**Fișierele .txt:** citite ca UTF-8 dacă au marcaj de început sau sunt UTF-8 valid; altfel ca Windows-1250
(codarea românească veche). Se scriu mereu UTF-8.

**În afara Fazei 1 (le spun ca să știi):** selecția, copierea și lipirea, căutarea. Le propun într-o fază
următoare, dacă le ceri.

**Teste:** navigarea pe caracter, cuvânt și rând cu diacritice; anulare și refacere; salvarea și redeschiderea
(.txt, .docx); copia pentru .docx străine; salvarea eșuată; ciorna; întrebarea la Alt+F4; masca din jurnal;
transcrieri.

### 3.3 Citire documente (`Hasi.Modules.Documents`)

**Ecrane:**
- Meniul „Documente”: Deschide un document (.txt, .docx, .pdf, .epub), Documente recente („continui de unde ai
  rămas”).
- **Cititorul** (`TextReaderScreen`): aceleași taste de mutare ca editorul, fără scriere, plus:
  - **„Citește tot”** de la poziția curentă. Se oprește la orice tastă și **ține minte propoziția** la care s-a
    oprit; la reluare, o citește de la început. (Cuvântul exact nu se poate ști sigur, pentru că normalizatorul
    schimbă numerele și prescurtările; propoziția, da.)
  - salt la titlul următor/anterior (.docx, .epub), la pagină (.pdf: „pagina 3 din 20”), la capitol (.epub);
  - poziția se salvează la ieșire; la redeschidere: „Continui de la pagina 12.”
- **Deschiderea** se face în fundal și se poate opri cu Escape; dacă durează peste o secundă: „Se deschide
  documentul…”. Documente foarte mari: o limită de mărime, cu mesaj clar.

**Ce promite HASI la PDF, cinstit:** citește textul pe care PDF-ul îl conține. Dacă o pagină nu are text
(scanată): „Pagina 4 este o imagine scanată. HASI nu poate citi încă imagini.” Dacă tot documentul e scanat, o
spune o dată, la deschidere. Dacă textul pare stricat (litere fără sens, de la fonturi fără tabel corect), o
spune, în loc să citească aiureli.

**Erori în română simplă:** document protejat cu parolă sau DRM: „Documentul este protejat și nu poate fi
deschis.”; fișier stricat: „Documentul nu a putut fi deschis. Poate fi deteriorat.”

**Teste:** documente mici de test, făcute de noi, pentru fiecare format: titluri, liste, tabele (citite rând cu
rând), pagini, capitole, diacritice cu sedilă, PDF pe două coloane, PDF parțial scanat, fișier stricat,
oprirea și reluarea citirii (cu motor simulat).

### 3.4 Calculator (`Hasi.Modules.Calculator`)

- Un singur ecran: scrii o expresie; fiecare tastă se rostește („doisprezece”, „plus”, „ori”, „împărțit la”);
  Enter spune „Rezultatul: …”; Backspace șterge; Delete golește; ↑/↓ recitesc expresia și rezultatele anterioare.
- **Merge complet fără tastatura numerică:** + − * / de pe tastatura obișnuită, virgula zecimală („12,5”),
  paranteze, procente („200 + 10 %” = 220, definit clar în ajutor).
- **Tastatura numerică, de aprobat:** cu Num Lock pornit, în calculator, / * − + devin operații, prin „profilul
  de taste” din nucleu; Ajutorul rămâne pe F1 și Ctrl+H, Repetă pe F2 și Ctrl+R. HASI o spune la intrare. Cu Num
  Lock oprit, tastatura numerică navighează, ca peste tot. **De verificat cu NVDA pornit**, în aspectul desktop:
  NVDA folosește unele taste numerice pentru mouse și cursorul de examinare, deci e posibil să nu ajungă la HASI.
- Calculul cu `decimal`, nu `double` (0,1 + 0,2 = 0,3 exact). „Nu se poate împărți la zero.”
- **Teste:** tabel de expresii, inclusiv erori și rotunjiri; transcrieri; profilul de taste.

### 3.5 Agendă (`Hasi.Modules.Agenda`)

**Ecrane:**
- Meniul „Agendă”: Programările de azi, Următoarele programări, Mementouri ratate, Contacte, Adaugă o programare.
- **Contacte:** listă alfabetică românească (ș după s, ț după t); o literă apăsată sare la primul nume cu acea
  literă. Pe un contact, Enter: Citește, Modifică, Șterge (cu confirmare rostită). Formularul are câmpurile
  nume, telefon, adresă, notă, fiecare pe rândul lui; telefonul se citește pe grupe de cifre.
- **Programare:** titlu, data (`DatePickerScreen`, cu scurtăturile „Azi” și „Mâine”), ora și minutele (săgeți
  sau cifre), repetare (o dată, zilnic, săptămânal, lunar, anual: medicamentele sunt zilnice), memento (la ora
  programării sau cu 15 minute / o oră / o zi înainte).

**Mementoul, cât HASI e pornit:**
- un sunet propriu, apoi, după mesajul curent: „Memento: Doctorul de familie, la ora 10.”;
- ecranul de memento vine peste orice ecran. Dacă sunt mai multe în același timp, e **un singur ecran** cu toate;
- **Enter nu confirmă înainte ca mementoul să fi fost spus o dată complet.** Așa, cine apasă Enter tocmai când
  scrie un rând nou în editor nu confirmă din greșeală un memento pe care nu l-a auzit;
- opțiuni: „Am auzit” (nu „am luat pastila”: HASI nu știe asta), „Amână 10 minute”. Escape închide ecranul, dar
  mementoul rămâne în așteptare și revine;
- fără răspuns: se repetă la un minut, de cel mult 5 ori, apoi trece în „Mementouri ratate”;
- dacă HASI nu e în prim-plan, vorbește oricum și adaugă „Apăsați Control, Alt și H pentru a reveni.” Nu ia
  focusul singur;
- **dacă Windows e blocat** (ecranul de blocare), HASI spune doar „Aveți un memento.”, fără titlu, pentru că
  titlul poate fi medical și privat.

**Regulile de timp, scrise și testate** (cu ceas simulat): ora care nu există primăvara (03:00–04:00 la noi)
sună la 04:00; ora care se repetă toamna sună o singură dată; „lunar pe 31” sună în ultima zi a lunilor mai
scurte; „anual pe 29 februarie” sună pe 28 februarie în anii nebisecți; ceasul dat înapoi nu repetă un memento
confirmat. Fiecare apariție are un identificator salvat, deci confirmarea și amânarea rezistă la o eroare gravă.
Mementourile ratate în mai multe zile se adună într-o singură listă, nu în zeci de anunțuri.

### 3.6 Manager de fișiere (`Hasi.Modules.Files`)

- Locuri: Documente, Desktop, Descărcări, Imagini, Muzică și fiecare **stick USB** introdus („Stick USB,
  Kingston, 15 gigaocteți liberi”). Nu arăt fișierele de sistem, ascunse sau folderele Windows.
- Pe un fișier, Enter deschide meniul lui: Citește, Editează (prin `IFileHandlers`, după tip), Copiază în…,
  Mută în…, Redenumește, Șterge, Informații (mărime, data). „Copiază în…” arată direct locurile, inclusiv
  stick-ul, fără „copiază, apoi lipește”, greu de urmărit pentru un începător.
- **Ștergerea** merge în Coșul de reciclare, cu confirmare rostită. Pe un stick nu există Coș: atunci HASI spune
  „Pe stick, fișierul se șterge definitiv. Sigur?”, o confirmare separată.
- **Copierea și mutarea:** mutarea pe alt disc e copiere urmată de ștergerea originalului **numai după ce copia
  e completă**. Progres („50 la sută”), oprire la cerere, mesaje clare la disc plin sau stick scos în timpul
  copierii; ce rămâne pe jumătate se șterge. Dacă numele există: întreabă. Nu folosesc dialogurile Windows de
  copiere, care nu sunt accesibile pentru HASI; fac operațiile noi și spunem noi ce se întâmplă.
- **„Scoate stick-ul în siguranță”**, cu mesaj clar dacă un program încă îl folosește.
- **Teste:** cu `IFileSystem` simulat (fără disc); operațiile reale, în teste de integrare pe un folder temporar.

---

## 4. Mementourile când HASI e închis sau calculatorul doarme

**Problema:** un memento la ora 10 nu se aude dacă la ora 10 HASI e închis, utilizatorul nu e autentificat sau
calculatorul e în repaus. Un memento de medicament pierdut în tăcere e cel mai rău caz.

**Ce pot promite cinstit:** mementourile sunt **„cel mai bine posibil”, dar niciunul nu se pierde în tăcere**.
O spun și în ajutorul agendei și în ghid.

**Propunerea, în trei straturi:**

1. **Recuperare (sigur, în modulul 5).** La pornirea HASI și la revenirea din repaus, HASI verifică ce mementouri
   au trecut între timp și le spune: „Cât timp calculatorul a fost oprit, a trecut un memento: Pastila de
   tensiune, azi la ora 8.” Rămân în „Mementouri ratate” până le confirmi.
2. **HASI pornit cât mai des (în modulul 5).** Pornirea automată e deja activă implicit. Dacă închizi HASI și ai
   un memento în următoarele 24 de ore, confirmarea închiderii o spune: „Aveți un memento azi la ora 18.” și, fără
   stratul 3, „Dacă închideți HASI, nu îl veți auzi.”
3. **Pornire programată de Windows (de aprobat).** O sarcină în Programatorul de activități Windows, doar pentru
   utilizatorul curent, **fără parolă și fără drepturi de administrator** (Microsoft documentează varianta asta:
   rulare cu sesiunea utilizatorului autentificat, cu privilegii minime). Detaliile pe care le stabilesc dinainte:
   - pornește `hasi.exe` (lansatorul stabil al Velopack) cu un minut înainte de următorul memento, într-un
     **mod „memento”**: fără bun venit, fără întrebarea de actualizare; dacă HASI rulează deja, îi spune doar
     „verifică mementourile”, fără să-l aducă în față;
   - pornește și pe baterie (implicit, Windows nu pornește sarcinile pe baterie, deci o setez explicit), cu durată
     scurtă de rulare pentru sarcină;
   - se reprogramează după fiecare memento sunat, amânat sau confirmat și după fiecare schimbare din agendă;
   - funcționează doar dacă utilizatorul e autentificat; se șterge la dezinstalare;
   - îl verific întâi printr-un experiment în `spikes/` (crearea fără administrator, pornirea pe baterie,
     comportamentul la un memento ratat), înainte de modulul 5.

**Ce NU propun:**
- să trezim calculatorul din repaus: depinde de setarea „Permite temporizatoare de trezire”, des oprită pe
  laptopuri sau pe baterie; laptopurile cu „Modern Standby” suspendă oricum programele obișnuite; un laptop trezit
  în geantă se și încălzește;
- o setare „Nu lăsa calculatorul să adoarmă”: ar împiedica doar adormirea automată, nu pe cea cerută de
  utilizator sau de capac (după cum e configurat Windows), deci ar promite mai mult decât poate. O las deoparte.

---

## 5. Datele utilizatorului și dezinstalarea

**Problema pe care o văd:** acum dezinstalarea șterge tot `%APPDATA%\HASI` și `%LOCALAPPDATA%\HASI`, fără nicio
întrebare. Dacă agenda, ciornele sau pozițiile de citire ar sta acolo, **o dezinstalare urmată de reinstalare**
(exact ce face un ajutor binevoitor când „ceva nu merge”) ar șterge contactele și programările unui om care nu le
are scrise în altă parte. Problema apare deja la **modulul 2** (ciornele), nu abia la agendă, deci decizia trebuie
luată acum.

**Deja observat (24 octombrie 2026):** dezinstalarea de la X-10 a șters și `%LOCALAPPDATA%\HASI\rhvoice`, folderul
cu RHVoice.dll folosit la rulările din build. De atunci, HASI rulat din build vorbește cu „Paul, prin SAPI”
(rularea de verificare de azi). Versiunea instalată nu e afectată, pentru că are RHVoice lângă program.

**Propunerea (schimbată după verificarea cu Codex):**

| Ce | Unde | La dezinstalare |
|---|---|---|
| Setări | `%APPDATA%\HASI\settings.json` (ca acum) | se șterg (decizia ta 12 rămâne) |
| Jurnale, cache | `%LOCALAPPDATA%\HASI` (ca acum) | se șterg |
| **Datele personale:** agenda, ciornele, documentele recente, pozițiile de citire, copiile de siguranță zilnice (ultimele 7) | **`%LOCALAPPDATA%\HASI.UserData`**, un folder separat, în afara celor care se șterg | **rămân** |
| Documentele salvate din editor | unde le salvezi; implicit folderul **Documente** al Windows | **nu se ating** (nici acum nu se ating) |

De ce `%LOCALAPPDATA%` și nu Documente sau `%APPDATA%`:
- **rămâne pe calculator.** Documente poate fi sincronizat în OneDrive (Windows 11 propune des „copiere de rezervă
  a folderelor”), iar `%APPDATA%` e „roaming” și, în rețele de firmă, poate fi mutat pe un server. Regula noastră
  e „datele rămân local”;
- **e un folder separat**, nu un subfolder al celor șterse: o greșeală la codul de dezinstalare nu-l poate atinge.

Ce vine cu el, ca să nu fie „ascuns și pierdut”:
- în agendă: **„Fă o copie de siguranță”** (pe stick sau în Documente, un singur fișier) și **„Adu datele dintr-o
  copie”**: așa se mută pe un calculator nou;
- în Setări: **„Șterge datele personale”**, cu confirmare dublă, pentru cine dă calculatorul altcuiva.
- ghidul de instalare și README spun exact ce se șterge și ce rămâne; ADR 0007 se actualizează.

**Varianta b**, dacă preferi un loc vizibil: `Documente\HASI`, cu riscul OneDrive de mai sus.

---

## 6. Prima publicare (0.2.0)

**Propun 0.2.0 după modulul 3 (Citire documente).**

De ce:
- **e primul pachet util în fiecare zi** pentru un om care abia a început: ora și data, o notă scrisă, o scrisoare
  sau o carte citită. Prima impresie contează: cine instalează HASI și găsește doar un ceas nu mai revine;
- agenda, cu mementourile de medicamente, e partea cea mai delicată. Vine după ce publicarea și actualizarea au
  mers în lumea reală;
- **SignPath** cere o versiune publicată; cererea poate porni imediat după 0.2.0 (acceptarea și durata ei nu le
  putem garanta).

**Condiții pentru publicare** (o listă de verificat la oprirea modulului 3, nu doar „modulul e gata”):
- salvarea, ciorna și recuperarea după o eroare gravă trec la tine;
- dezinstalare + reinstalare păstrează datele personale (§5);
- jurnalul de diagnostic nu conține textul scris;
- tastele noi merg cu NVDA pornit;
- o actualizare instalată (din folder local, ca la X-05) păstrează documentele, ciornele și pozițiile de citire.

**Apoi:** propun o versiune mică de întreținere (0.2.1, cu calculatorul sau doar cu corecturi), ca prima
actualizare reală prin GitHub să nu fie cea care aduce agenda. Agenda vine în 0.3.0.

**Alternativa:** 0.2.0 după modulul 1, doar ca să pornim mai devreme cererea SignPath. Nu o recomand.

Pașii de publicare sunt cei pregătiți în `plan-0.11.md`, pasul 4, neschimbați. Publicarea o fac numai după
aprobarea ta explicită.

---

## 7. Riscuri

| # | Risc | Măsură |
|---|---|---|
| F1 | „Citește tot” cere ca fiecare motor să spună când **s-a auzit** tot mesajul. RHVoice direct știe (are ieșirea noastră); SAPI5 are `SpeakCompleted`; OneCore are deja evenimentul de sfârșit de redare. Rezerva (`FallbackSpeechEngine`) trebuie să nu piardă raportul la schimbarea vocii. | Interfața întâi cu motorul simulat; măsor pe fiecare motor în modulul 3. |
| F2 | PDF-urile românești au des fonturi fără tabel Unicode corect: textul iese cu litere greșite sau lipsă. | Teste pe PDF-uri reale de-ale tale (le citești tu, nu le pun în depozit); HASI spune când textul pare stricat. |
| F3 | Ordinea textului în PDF-uri cu coloane sau tabele. | Algoritmii de ordine ai PdfPig; teste pe documente cu două coloane. |
| F4 | PdfPig e încă „0.1.x”: API-ul se poate schimba. | Versiune fixată; tot codul PdfPig stă într-o singură clasă din `Hasi.Documents`. |
| F5 | Tastele noi se pot ciocni cu NVDA (mai ales pe tastatura numerică, în aspectul desktop) sau cu tastele globale HASI. | Tabelul tastelor fiecărui modul ți-l arăt înainte de cod; Ctrl+H/R/U rămân globale; fiecare funcție merge și fără tastatura numerică; testat cu NVDA, cu Num Lock pornit și oprit. |
| F6 | Programatorul de activități poate fi blocat de politici sau de antivirus. | Experiment în `spikes/`; dacă nu merge, rămân straturile 1 și 2 din §4. |
| F7 | Meniul principal crește de la 4 la 10 opțiuni: se schimbă bun venit-ul și mulți pași din regresie. | Ordinea o decizi tu (§8); transcrierile se schimbă vizibil în diff. |
| F8 | Date personale în jurnalul de diagnostic (text scris, contacte, nume de documente). | Ecranele sensibile maschează caracterele, mesajele și titlurile (§2). |
| F9 | Licențele: fiecare pachet are fișiere de licență și NOTICE care trebuie livrate, nu doar pomenite. | Graful complet de dependențe (din versiunile fixate) în `THIRD-PARTY-NOTICES.md`; licențele și NOTICE-urile într-un folder `licente\` în pachetul instalat; „Despre HASI” le numește. |

---

## 8. Decizii pe care ți le cer

**Acum, înainte de modulul 1:**
1. **Planul în ansamblu** și ordinea modulelor.
2. **Locul datelor personale** (§5): `%LOCALAPPDATA%\HASI.UserData`, păstrat la dezinstalare (recomandat), sau
   `Documente\HASI`. E o schimbare a deciziei 12 („dezinstalarea șterge tot”), de aceea ți-o cer explicit.
3. **Meniul principal:** propun modulele întâi, în ordinea folosirii (Ceas și calendar, Agendă, Editor,
   Documente, Calculator, Fișiere), apoi Setări, Învață tastele, Despre HASI, Ieșire. Fiecare modul apare în meniu
   abia când e gata.
4. **Prima publicare:** după modulul 3, cu condițiile din §6 (recomandat), sau după modulul 1.

**Înainte de modulul 5 (le poți decide și acum):**
5. **Pornirea programată de Windows** pentru mementouri (§4, stratul 3): da sau nu.

**La începutul modulului respectiv:**
6. Calculatorul: / * − + de pe tastatura numerică devin operații (§3.4)?
7. Editorul: formatul implicit .docx (§3.2) și tastele exacte.
8. Ceasul: vrei o tastă globală „Ce oră este?” (de exemplu Ctrl+T, oriunde în HASI) și/sau ora rostită la fiecare
   oră fixă? Ar fi o comandă globală nouă în tabelul de taste, deci o schimbare de plan.

## Lucruri care rămân la fel

- Fiecare modul: commit-uri mici, teste `ShellHarness`, transcrieri, scenarii în `docs/manual-tests/` și câțiva
  pași noi în regresie; apoi **oprire**, versiune care rulează, rularea de verificare cu `--diagnostic`.
- Lista de regresie: propun ca `regresie-faza-0.md` să devină `regresie.md`, cu o secțiune nouă pentru fiecare
  modul (3–5 pași esențiali), ca să rămână o singură sesiune.
- Fiecare bibliotecă nouă intră în `THIRD-PARTY-NOTICES.md` și în „Despre HASI” în același commit.
- Gata, pentru Faza 1, înseamnă: scenariile tuturor modulelor și regresia trec la tine pe Windows 11; Windows 10
  rămâne „suportat, netestat”.

---

## 9. Ce s-a schimbat după verificarea cu Codex

Am dat planul spre verificare independentă lui Codex, cu acces doar de citire la depozit. A făcut 22 de
observații; le-am verificat în cod și în documentație înainte să le iau. Cele preluate:

- **Lucrul nesalvat** e protejat la orice închidere (Alt+F4, oprirea calculatorului, actualizarea), nu doar la
  Escape: acum HASI se închide fără să întrebe ecranele (verificat în `Shell.cs`).
- **Jurnalul de diagnostic** scria fiecare caracter tastat înainte de ecran; acum ecranele sensibile maschează și
  caracterele, și titlurile (verificat).
- **Datele personale:** decizia se ia acum, nu la agendă (ciornele apar în modulul 2), iar locul recomandat s-a
  mutat din Documente în `%LOCALAPPDATA%\HASI.UserData` (OneDrive, `%APPDATA%` roaming), cu copie de siguranță,
  restaurare și ștergere la cerere.
- **Mementourile:** Enter nu confirmă un memento neauzit; un singur ecran pentru mai multe; ecranul blocat nu
  spune titlul; „Am auzit”, nu „am luat”; regulile de oră de vară, de 31 și de 29 februarie; mod „memento” la
  pornirea programată, care nu se bate cu regula de o singură instanță; pornire și pe baterie.
- **Setarea „Nu lăsa calculatorul să adoarmă”** a fost scoasă: promitea mai mult decât poate Windows.
- **Progresul vorbirii** aparține mesajului care l-a cerut (identificator și proprietar), nu ecranului curent;
  „citește tot” reia de la propoziție, nu de la cuvânt.
- **Operațiile în fundal** au identificator și oprire; un rezultat întârziat nu redeschide un ecran părăsit.
- **Calculatorul** primește un „profil de taste” în nucleu și merge complet fără tastatura numerică.
- **Ștergerea de pe stick** e definitivă (nu există Coș) și are confirmare separată; mutarea șterge originalul
  doar după copia completă.
- **Nucleul** nu primește un ecran de alegere nou (`ChoiceScreen` există), dar primește alegerea datei, comună
  ceasului și agendei; recurența rămâne în agendă.
- **Sărbătorile:** numele rostite stau în `ro.json`, nu în fișierul de date; „Paștele ortodox” spus explicit;
  diferența dintre data legii (martie 2023) și prima aplicare (ianuarie 2024); algoritmul limitat la 1900–2099.
- **0.2.0** are condiții de publicare, iar prima actualizare reală nu e cea cu agenda.
- **Licențele:** `DocumentFormat.OpenXml.Framework` adăugat; fișierele de licență și NOTICE livrate în pachet.

Una nu se confirmă: Codex n-a putut citi fișierul Adobe `MustRead.html` din PdfPig; eu l-am citit direct din
depozit (calea e în §0).
