# HASI – 0.11, pașii 2 și 3: installer și actualizări

Pașii 2 și 3 sunt gata. M-am oprit la oprirea prevăzută. Pasul 4, publicarea 0.1.0, nu e început. Toate cele **339 de teste** trec, iar CI-ul e verde.

## Ce s-a făcut (3 commit-uri, fiecare verificat singur)

- **`a0c46e8` – actualizări cu anunț rostit:**
  - la pornire, o versiune nouă e oferită **o singură dată**, după bun venit, fără să-l taie;
  - Escape spune „Puteți actualiza oricând din Setări, Actualizări.”, iar HASI întreabă din nou doar pentru o versiune mai nouă;
  - **Setări → Actualizări** are trei opțiuni: Caută actualizări acum, Verificare automată la pornire (Da/Nu) și Versiunea instalată;
  - progresul descărcării se spune din 25 în 25 la sută, iar erorile sunt spuse simplu („Nu există conexiune la internet.” și celelalte).
- **`c9c32ce` – „Simulare oprire” oprită implicit** pentru utilizatorii noi. La tine rămâne pornită: în `settings.json` ai salvat `true`.
- **`74d47fb` – installer-ul**, în același commit cu textul de licență corectat:
  - `installer\pack.ps1` face un installer cu .NET și vocea incluse, fără drepturi de administrator; acceptă doar RHVoice-ul construit în CI;
  - prima pornire după instalare activează pornirea automată și spune o singură dată mesajul tău;
  - după o actualizare, HASI spune versiunea nouă;
  - dezinstalarea șterge pornirea automată, `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI`. Ghidul (`docs/ghid-instalare.md`) și README spun că **dezinstalarea șterge toate setările și jurnalele**;
  - Windows 10 apare „suportat, netestat”;
  - decizia e scrisă în ADR 0007.

## Versiunea RHVoice și licența (cerințele tale)

- „Despre HASI”, `THIRD-PARTY-NOTICES.md` și `BUILD-INFO.txt` spun acum **„RHVoice 1.18.4”** și commitul `fa9dd196fd2dac3b0bf089a2d80fc8477c2380e3`.
  - În „Despre HASI”, commitul se rostește literă cu literă, primele 8 caractere: „f, a, nouă, d, d, unu, nouă, șase”. Scris legat, fiecare caracter s-ar fi pronunțat greu de înțeles.
  - Codul complet e în `THIRD-PARTY-NOTICES.md` și în `BUILD-INFO.txt`. Spune-mi dacă îl vrei rostit întreg.
- **De unde vine „1.19.2”:** DLL-ul își raportează singur versiunea, prin funcția lui `RHVoice_get_version`. Valoarea e pusă la compilare de scriptul RHVoice (`SConstruct`, funcția `get_version()`), care întoarce „1.19.2” la toate tag-urile 1.18.x. Apare automat în două locuri:
  - în jurnalul de diagnostic, unde rândul arată acum așa: `RHVOICE version 1.19.2 (build: RHVoice 1.18.4, commit fa9dd196…)`;
  - în numele fișierelor WAV produse de utilitarul de verificare din CI.
- **Licența** e corectată în „Despre HASI”, `THIRD-PARTY-NOTICES.md` și în comentariul din cod: GPL 3 sau ulterioară pentru întreg, cu părți GPL 2+, LGPL 2.1+ și LGPL 3+.
- **Două build-uri ale aceleiași surse au alt SHA-256.** MSVC scrie data compilării în DLL, deci fiecare build din CI are altă amprentă. Am verificat că sunetul celor 5 fraze rămâne identic bit cu bit cu 1.18.2.

## Ce am verificat și ce nu

- **Proba de pornire** pe programul publicat (cel din installer, cu vocea inclusă), fără taste:
  - vorbește cu RHVoice din pachet;
  - verificarea de actualizări de la pornire tace, cum trebuie când HASI nu e instalat;
  - nicio eroare, iar pornirea ta automată a rămas neatinsă.
- **Nu am instalat nimic pe contul tău:** instalarea ți-ar fi schimbat pornirea automată și scurtăturile. Deci instalarea, actualizarea între două versiuni instalate și dezinstalarea **nu le-am încercat eu**: sunt scenariile tale.

## Fișierele pentru test (construite local, nepublicate, ignorate de git)

- programul de instalare 0.1.0, de 83 MB: `artifacts\HASI-0.1.0-Setup.exe`;
- sursa locală de actualizări, cu 0.1.0 și 0.1.1: `artifacts\feed`. Actualizarea 0.1.0 → 0.1.1 e un delta de 0,1 MB.

## Scenarii (X-00…X-10, în `docs/manual-tests/faza-0.md`)

| ID | Ce apeși | Ce trebuie să auzi |
|---|---|---|
| X-00 | Închizi HASI și faci o copie a vocii de test: `robocopy "%LOCALAPPDATA%\HASI\rhvoice" "%USERPROFILE%\HASI-rhvoice-copie" /E` | – |
| X-01 | Deschizi `artifacts\HASI-0.1.0-Setup.exe`; dacă apare SmartScreen: „Mai multe informații”, apoi „Executare oricum” | bară de progres fără voce, apoi HASI pornește singur: bun venit și o singură dată „HASI va porni automat când porniți calculatorul. Puteți schimba asta din Setări, Pornire automată.” |
| X-02 | Despre HASI, rând cu rând | versiunea 0.1.0; rândul 4: „RHVoice 1.18.4, commitul f, a, nouă, d, d, unu, nouă, șase… GNU GPL, versiunea 3 sau ulterioară…”; rândurile 10 și 11: .NET și Velopack |
| X-03 | Setări → Pornire automată; apoi ieși din cont și intri din nou | „…Selectat: Da: HASI pornește singur…, actual, 1 din 2.”; după autentificare HASI pornește singur. `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v HASI` arată `%LOCALAPPDATA%\HASI.App\hasi.exe` |
| X-04 | Cu HASI deschis, Enter pe scurtătura „HASI” de pe Desktop | nu apare a doua fereastră; HASI vine în față și spune ecranul curent |
| X-05 | Setări → Actualizări | „Actualizări. 3 opțiuni. …”; al treilea rând: „Versiunea instalată: 0.1.0” |
| X-06 | Închizi HASI. Tasta Windows + R: `%LOCALAPPDATA%\HASI.App\hasi.exe --update-source "folder:C:\Users\cucua\Documents\dev\workspace\Hasi\artifacts\feed"` | după bun venit: „Este disponibilă versiunea 0.1.1 a HASI. Doriți să actualizați acum? …”; **Escape**: „Puteți actualiza oricând din Setări, Actualizări. …” |
| X-07 | Închizi HASI și repeți comanda de la X-06 | **nu** mai întreabă |
| X-08 | Setări → Actualizări → Caută actualizări acum; Enter | „Caut actualizări.”, apoi întrebarea; după Enter: „Se descarcă actualizarea.”, „Actualizarea este gata. HASI pornește din nou.”; HASI repornește singur și spune „HASI a fost actualizat la versiunea zero punct unu punct unu.”; în „Despre”: 0.1.1 |
| X-09 | Fără internet: pornești HASI normal; Setări → Actualizări → Caută acum | „Nu există conexiune la internet.” Cu internet: „Aveți cea mai nouă versiune a HASI, 0.1.1.” (pe GitHub nu e încă nimic publicat; dacă auzi „Verificarea actualizărilor nu a reușit”, notează) |
| X-10 | Setări Windows → Aplicații → HASI → Dezinstalare | HASI se închide; dispar programul, scurtăturile, pornirea automată, `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI`. Apoi refaci vocea de test: `robocopy "%USERPROFILE%\HASI-rhvoice-copie" "%LOCALAPPDATA%\HASI\rhvoice" /E` |

**Atenție după X-10:**
- Setările tale s-au șters, iar HASI pornește ca pentru un utilizator nou.
- „Simulare oprire” e **oprită**: repornirea și oprirea din HASI sunt reale. O pornești din nou din Setări → Diagnostic → Simulare oprire.
- La X-10 se șterg și folderele de test `rhvoice-ci-1.18.4` și `comparatie-rhvoice`.

Mai de știut: Velopack produce și un `HASI.App-win-Portable.zip`, pe care nu îl folosim.

## Ce urmează

Aștept rezultatele X-00…X-10. Pasul 4, publicarea 0.1.0, nu începe fără aprobarea ta.
