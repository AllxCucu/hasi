# Planul etapei 0.11 – instalare și actualizare

> **Stare:** pasul 1 **APROBAT** (23 octombrie 2026), pentru că nu atinge programul. Pașii 2–4 sunt încă
> **PROPUNERE**: îi aprobi după rezultatele regresiei (`docs/manual-tests/regresie-faza-0.md`).

## Decizii primite (23 octombrie 2026)

| # | Subiect | Decizia ta |
|---|---|---|
| 1 | „Simulare oprire” | Fără migrare. La tine rămâne pornită (în `settings.json` ai `"SimulatePower": true`, am verificat). Pentru utilizatorii noi, valoarea implicită devine **oprită**. |
| 2 | Id-ul Velopack | `HASI.App`. La dezinstalare se șterg `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI`. |
| 3 | Compilarea RHVoice.dll | Cu SCons. Dacă nu se poate construi doar biblioteca x64, mă opresc și îți dau variantele. |
| 4 | SignPath | Tu ești autor, recenzent și aprobator, și apari așa în README. Formularul îl completăm împreună, după publicarea versiunii 0.1.0. |
| 5 | Windows 10 | Nu ai. Vezi întrebarea 1 de la final. |
| 6 | P-13 | **Decis definitiv:** „unu din patru”, varianta HASI. |
| 7 | Sursa actualizărilor | GitHub Releases. |
| 8 | Windows 10 (răspuns final) | Varianta c: rămâne **„suportat, netestat”** până ai acces la un calculator cu Windows 10. |
| 9 | Pornirea automată după instalare | **Pornită implicit.** La prima pornire după instalare, HASI spune o singură dată: „HASI va porni automat când porniți calculatorul. Puteți schimba asta din Setări, Pornire automată.” |
| 10 | Opririle din 0.11 | Trei: după pasul 1, după pașii 2–3, înainte de publicarea 0.1.0. |
| 11 | Întrebarea de actualizare | **O singură dată pentru fiecare versiune nouă.** După Escape, HASI nu mai întreabă la pornire pentru acea versiune; actualizarea rămâne în Setări → Actualizări. Dacă apare între timp o versiune și mai nouă, întreabă din nou, o dată. |
| 12 | Dezinstalarea | Ștergere completă, confirmată: pornirea automată, `%LOCALAPPDATA%\HASI`, `%APPDATA%\HASI`. **Ghidul de instalare și README spun că dezinstalarea șterge toate setările și jurnalele.** |

## Ce am verificat pentru acest plan (nu presupun)

- **RHVoice**, tag-ul `1.18.4`:
  - CI-ul lor de Windows rulează `pip install lxml scons`, apoi `scons`, pe `windows-latest`.
  - Pe Windows, scriptul construiește întâi x86, apoi x64 (`enable_x64`). Are și o opțiune `languages`.
  - Vocea Paul (`data/voices/paul` → `paul-ron`) și datele românești (`data/languages/Romanian`, `src/scripts/Romanian`) sunt submodule chiar în depozitul RHVoice.
  - Depozitul are în total peste 100 de submodule (toate vocile și limbile).
  - DLL-ul pe care îl folosești acum e versiunea **1.18.2** (din jurnalul tău).
- **Velopack** ([pe Windows](https://docs.velopack.io/packaging/operating-systems/windows), [integrare](https://docs.velopack.io/integrating/overview), [dezinstalare](https://docs.velopack.io/integrating/uninstalling), [semnare](https://docs.velopack.io/packaging/signing)):
  - Instalează per-utilizator, fără drepturi de administrator, în `%LOCALAPPDATA%\HASI.App`.
  - Lângă folderul `current` pune un `hasi.exe` de pornire, cu „cale stabilă care rezistă actualizărilor”.
  - `current` se înlocuiește complet la fiecare actualizare. Datele noastre sunt deja în afara lui.
  - La dezinstalare șterge tot `%LOCALAPPDATA%\HASI.App`, scurtăturile și intrarea din lista de programe.
  - Despre cheia de pornire automată (`Run`) documentația nu spune nimic, deci o ștergem noi.
  - Hook-ul de dezinstalare nu are voie să aibă interfață și trebuie să termine repede (30 de secunde).
  - `VelopackApp.Build().Run()` trebuie să fie primul lucru din `Main`.
  - Actualizarea: `UpdateManager` cu `GithubSource`, apoi `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, `ApplyUpdatesAndRestart`.
  - Rulat din `bin\Debug`, dă `NotInstalledException`.
  - Dacă la actualizare un fișier din `current` e blocat, Velopack poate afișa o întrebare. Deci HASI trebuie să fie complet închis la aplicarea actualizării.
- **SignPath** ([termeni](https://signpath.org/terms), încă marcați „draft”): cerințele din `architecture.md` §5.1 sunt valabile. În plus, cer numele și versiunea produsului în metadatele fișierelor, pe care le avem deja.
- **Semnarea în Velopack:** `vpk` semnează fișierele programului și `Setup.exe` (prin `signtool` sau o comandă proprie). Documentația nu spune nimic despre SignPath, așa că legătura cu SignPath o stabilim după acceptare.

## Pașii propuși

### Pasul 1 – RHVoice.dll construit în CI (primul, cum ai cerut) – **FĂCUT, așteaptă ascultarea ta (W-01…W-04)**

> Rezultat (23 octombrie 2026), detalii în ADR 0004:
> - doar biblioteca x64 se construiește cu SCons (`sapi_dev_core=true`), în circa 70 de secunde;
> - sunetul e identic bit cu bit cu 1.18.2;
> - Paul și datele românești vin din același commit și sunt identice cu ale tale;
> - DLL-ul se declară „1.19.2”;
> - unele fișiere sunt sub LGPL-3.0-or-later, deci la pasul 2 se corectează textul de licență din „Despre HASI”, din `THIRD-PARTY-NOTICES.md` și din comentariul din `RhVoiceLibrary.cs`.
>
> Ce s-a schimbat față de planul de mai jos: SCons, nu CMake; datele vin din submodulele RHVoice, nu dintr-o versiune separată.

1. Un job separat în GitHub Actions, pe `windows-latest`, aduce RHVoice la **commitul exact al tag-ului 1.18.4**.
   - Propun ca RHVoice să **nu** fie submodul în depozitul nostru, cum scria în ADR 0004. CI-ul îl aduce după hash, cu `actions/checkout`, iar submodulele se inițializează **selectiv**: doar ce cere biblioteca (bibliotecile externe), plus Paul și datele românești. Altfel s-ar descărca peste 100 de voci.
   - Sursa corespunzătoare, cum cere GPL, rămâne „depozitul RHVoice la commitul X” plus fișierul nostru de CI.
2. Construcția: `pip install lxml scons`, apoi `scons` cu ținta bibliotecii x64, `languages=romanian`, `release=true`.
   - **Dacă SCons nu poate construi doar biblioteca x64, mă opresc** și îți dau variantele:
     - a) construim tot, cu durata pe care o măsor, și păstrăm doar DLL-ul;
     - b) folosim calea CMake, care există în depozit;
     - c) rămânem la DLL-ul oficial, cu trimitere la sursă.
3. Verificări automate în CI:
   - DLL-ul e x64;
   - SHA-256 și commitul sunt scrise într-un `BUILD-INFO.txt`;
   - un test care încarcă DLL-ul construit și sintetizează „Bun venit în HASI” cu Paul, iar sunetul obținut nu e liniște;
   - testele existente de motor (`RhVoiceDllEngineTests`) rulează pe DLL-ul nou.
4. Rezultatul e un artifact CI, `rhvoice-x64`: DLL-ul, datele lui Paul, datele românești și licențele (GPL/LGPL, CC BY-SA 4.0).
5. **Test după ureche:** construim 1.18.4, iar tu ai acum 1.18.2, deci pronunția se poate schimba puțin.
   - Ascultăm cu **Comparare voci**: 1.18.2, DLL-ul tău de acum, față de 1.18.4, cel din CI;
   - apoi pașii 1–23 din regresie.

   **Oprire.**

### Pasul 2 – pachetul Velopack

1. Pachetul NuGet `Velopack` și unealta `vpk`, ambele cu versiune fixată (licență MIT).
   - Apar în `THIRD-PARTY-NOTICES.md` și în „Despre HASI”, în același commit.
   - Fac ADR 0007: installer și actualizări.
2. Licența RHVoice corectată (ADR 0004, ultima secțiune) în „Despre HASI”, `THIRD-PARTY-NOTICES.md` și `RhVoiceLibrary.cs`, în același commit cu includerea DLL-ului în pachet.
3. Un `Main` propriu, cu `VelopackApp.Build()` la început, înaintea regulii de o singură instanță:
   - **la dezinstalare** (hook rapid, fără interfață): șterge valoarea `HASI` din cheia `Run`, apoi folderele `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI` (decizia ta 2);
   - **după o actualizare** (`OnRestarted`): HASI spune versiunea nouă, la pasul 3.
4. Publicarea, cu `vpk pack --packId HASI.App --packTitle HASI --mainExe hasi.exe`:
   - **self-contained** `win-x64`: .NET intră în pachet, deci nu se instalează niciun runtime și nu apare nicio fereastră de administrator (UAC);
   - RHVoice de la pasul 1 intră în `rhvoice\`, lângă program. HASI caută deja acolo, primul.
   - Măsor dimensiunea installer-ului și ți-o spun.
5. **Pornire automată:** când HASI rulează instalat, cheia `Run` indică `hasi.exe` de pornire (`%LOCALAPPDATA%\HASI.App\hasi.exe`), nu folderul `current`. Se activează la prima pornire după instalare (`OnFirstRun`), iar HASI spune o singură dată mesajul de la decizia 9.
6. **„Simulare oprire” devine oprită implicit** în `UserSettings`. ADR 0006 se actualizează. Testele care presupun „pornită implicit” se inversează, iar transcrierile 05 și 08 se schimbă vizibil în diff. La tine rămâne pornită (decizia 1).
7. **Ce aude un utilizator la instalare:**
   - `Setup.exe` are o fereastră de progres, fără voce;
   - la final pornește HASI, care spune bun venit;
   - cât timp versiunea e nesemnată, Windows SmartScreen avertizează înainte. Îl scriu în ghidul de instalare, cu tastele pentru NVDA.

### Pasul 3 – actualizarea, cu anunț rostit (M0-31)

1. În nucleu, o interfață `IUpdateService` (verifică, descarcă cu progres, aplică și repornește). Implementarea Velopack stă în aplicație.
2. Sursa vine din configurare (`architecture.md` §1.8):
   - implicit `github`, adică `https://github.com/AllxCucu/hasi`, ca la decizia 7;
   - pentru testele tale, și `folder` (o cale locală), ca să încerci actualizarea fără să publicăm nimic.
3. **Setări → Actualizări** (acum e doar un loc rezervat):
   - „Caută actualizări acum”;
   - „Verificare automată la pornire: Da / Nu”, implicit Da;
   - „Versiunea instalată: 0.1.0”.
4. **La pornire,** dacă există o versiune nouă **despre care HASI nu a întrebat încă** (decizia 11; versiunea refuzată se ține minte în `settings.json`): după bun venit, HASI pune întrebarea de confirmare (anunțată, ca orice confirmare):
   - Întrebarea: „Este disponibilă versiunea {v} a HASI. Doriți să actualizați acum? HASI se va închide și va porni din nou.”
   - **Enter:** „Se descarcă actualizarea.”, apoi progresul („50 la sută”), „Actualizarea este gata. HASI pornește din nou.”, iar după repornire „HASI a fost actualizat la versiunea {v}.”
   - **Escape:** „Puteți actualiza oricând din Setări, Actualizări.” HASI nu mai întreabă pentru această versiune, ci doar pentru una mai nouă.
5. **Erori,** în română simplă, fără coduri:
   - verificarea automată fără internet: tăcere, doar o linie în jurnal;
   - „Caută acum” fără internet: „Nu există conexiune la internet.”;
   - descărcare eșuată: „Actualizarea nu a putut fi descărcată. Rămâne versiunea {v}.”;
   - rulat din build, fără installer: „Actualizările funcționează doar după instalare.”
6. **Rețea și confidențialitate:**
   - singura cerere e către GitHub, o dată la pornire; limita GitHub de 60 de cereri pe oră nu contează aici;
   - sursa se trece în `docs/data-sources.md`, cu planul de rezervă: dacă proiectul se mută, schimbăm doar configurarea.

**Oprire după pașii 2–3:** instalare, pornire automată, actualizare din folder local, dezinstalare.

### Pasul 4 – CI și prima versiune

1. **La fiecare push:** build, teste, publicare self-contained, `vpk pack`. Rezultatul (`Setup.exe` și pachetele) rămâne **artifact CI**, nepublicat. Aceasta e „publicarea de test”.
2. **Publicarea 0.1.0:** un tag `v0.1.0` declanșează `vpk upload github`. Fac asta **numai după aprobarea ta explicită**, fiindcă e o publicare. Prima versiune e **nesemnată**.
3. **README:**
   - „Code signing policy”, cu textul cerut de SignPath (mențiunea „în curs de aplicare” până la acceptare);
   - rolurile: AllxCucu, autor, recenzent și aprobator;
   - politica de confidențialitate: HASI contactează doar sursa de actualizări, fără date personale, iar verificarea se poate opri;
   - ghidul de instalare pentru nevăzători.
4. După publicare completăm împreună formularul SignPath. Semnarea în CI vine după acceptare. Ordinea exactă (programul semnat înainte de `vpk pack`, apoi `Setup.exe`) o stabilim atunci.

## Scenarii pentru 0.11 (le detaliez la implementare)

- Instalezi din `Setup.exe` (artifact CI): SmartScreen, apoi HASI pornește singur și vorbește; în „Despre”, versiunea 0.1.0.
- Pornirea automată după instalare: deconectare din cont și autentificare.
- O actualizare din folder local, de la 0.1.0 la 0.1.1 (M0-31): întrebarea, progresul, repornirea, „HASI a fost actualizat…”.
- Fără internet: „Caută actualizări acum” → „Nu există conexiune la internet.”
- Dezinstalarea: dispar HASI, pornirea automată, `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI`; cele două versiuni RHVoice se aud la fel (pasul 1).

## Riscuri și lucruri de verificat la implementare

1. Dacă SCons poate construi doar biblioteca x64 (pasul 1.2). Dacă nu, mă opresc. **Rezolvat:** se poate.
2. Ce submodule RHVoice cere biblioteca. Dacă inițializarea selectivă nu merge, durata CI crește. **Rezolvat:** Boost, Romanian, Paul.
3. Ce scurtături creează Velopack implicit (Desktop, meniul Start). Documentația citită nu spune.
4. Antivirusul poate da alarme false pe un installer nesemnat. Se rezolvă prin SignPath, mai târziu.
5. Dacă un pachet self-contained cu .NET 10 merge cu versiunea Velopack fixată. Verific înainte de pasul 2.

## Întrebări pentru tine (răspunse pe 23 octombrie 2026: deciziile 8–10 de mai sus)

1. **Windows 10.** „Gata” pentru Faza 0 cere Windows 11 **și** Windows 10 (`architecture.md` §4), dar nu ai Windows 10. Variante:
   - a) definiția devine „doar Windows 11”, o schimbare de plan pe care o decizi tu;
   - b) caut o variantă legală și gratuită de Windows 10 pentru o mașină virtuală și îți spun ce am găsit;
   - c) amânăm testul pe Windows 10 până ai acces la un astfel de calculator.

   Până atunci, HASI declară Windows 10 ca sistem suportat, dar netestat.
2. **Pornirea automată după instalare:** pornită implicit (bine pentru cine nu știe unde e HASI) sau oprită până o alege utilizatorul?
3. **Opririle din 0.11:** propun trei: după pasul 1 (vocea din CI), după pașii 2–3 (instalare și actualizare), înainte de publicarea 0.1.0. E în regulă, sau vrei altă împărțire?
