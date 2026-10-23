# HASI – Arhitectură și plan pentru Faza 0

> **Stare:** APROBAT pe 21 octombrie 2026, cu ajustările cerute (vezi §5). Plan modificat pe 24 octombrie 2026: etapa 0.9 (cititoarele de ecran) a fost scoasă, iar detectarea Naratorului a trecut în 0.10 (§1.6, ADR 0003).
> Deciziile importante sunt explicate și în `docs/adr/`.

Cuprins:
0. Ce am verificat înainte de propunere
1. Alegerea limbajului și arhitectura
2. Structura repository-ului (CLAUDE.md este în rădăcină)
3. Riscuri tehnice
4. Planul Fazei 0 și scenariile de testare manuală
5. Decizii aprobate

---

## 0. Ce am verificat (nu presupun)

| Subiect | Constatare | Sursă |
|---|---|---|
| .NET 8 | **Iese din suport pe 10 noiembrie 2026**, peste 3 săptămâni. La fel .NET 9. Următorul LTS este **.NET 10**, cu suport până în noiembrie 2028. | Microsoft .NET Blog |
| RHVoice – API C | `RHVoice.h` are antet **LGPL-2.1-or-later**. Expune `RHVoice_new_tts_engine`, `RHVoice_get_voices`, `RHVoice_new_message_w`, `RHVoice_speak` etc. (sinteză cu callback-uri audio). | github.com/RHVoice/RHVoice |
| RHVoice – licența depozitului | **Atenție:** `LICENSE.md` din rădăcina depozitului conține textul **GPL-2.0**, iar distribuțiile Linux îl etichetează diferit (LGPL-2.1+, GPL-2.0, GPL-3.0+). Licența e deci mixtă pe fișiere. Înainte să legăm direct `RHVoice.dll`, trebuie verificat fișier cu fișier ce intră în DLL. | github.com/RHVoice/RHVoice, Arch, ALT Linux |
| Vocea Paul | Depozit `RHVoice/paul-ron`, versiunea 4.2, licența **CC BY-SA 4.0**. Drepturi de autor: Stefan Moisei (pachetul de limbă), Paul Bogorin (vorbitorul), Zvonimir Stanecic (antrenarea vocii); egalizare: Tomasz Bilecki. Există **installer SAPI5 pentru Windows** și add-on NVDA. Datele de limbă română: LGPL-2.1 (după Arch). | rhvoice.org/ro-voices, github.com/RHVoice/paul-ron |
| NVDA Controller Client | DLL sub **LGPL 2.1**: `nvdaController_testIfRunning`, `speakText`, `cancelSpeech`. NVDA vorbește și pe ecranul de blocare, deci nu trimitem prin el text sensibil când Windows e blocat. **Nu se mai folosește:** etapa 0.9 a fost scoasă (ADR 0003). | github.com/nvaccess/nvda (extras/controllerClient) |
| Mediul tău | Inițial: .NET SDK 9.0.318, git 2.55, gh, fără Python. **.NET SDK 10.0.402 instalat** cu winget pe 21 octombrie 2026. | verificat local |

---

## 1. Limbajul și arhitectura

### 1.1 Recomandare: **C# pe .NET 10 LTS**, interfață în **WPF**

.NET 8 nu mai are rost ca țintă, pentru că iese din suport înainte să terminăm Faza 0. Propun .NET 10, care e succesorul direct și are aceleași API-uri.

| Criteriu | C# / .NET 10 | Python 3.12+ |
|---|---|---|
| **SAPI5** | COM nativ (SpVoice), cu flag-ul `SVSFPurgeBeforeSpeak` pentru întrerupere imediată | `comtypes`/`pywin32`, funcționează |
| **OneCore** (`Windows.Media.SpeechSynthesis`) | Direct, cu target `net10.0-windows10.0.19041.0`, fără pachete în plus | Prin pachetele `winrt-*`, care se versionează separat și s-au rupt de câteva ori |
| **RHVoice direct** | P/Invoke pe `RHVoice.dll` (legare dinamică, conformă LGPL) | `ctypes`: varianta dovedită, pe care o folosește add-on-ul NVDA |
| **Instalarea la utilizator** | Un singur installer, instalare per-utilizator fără drepturi de administrator (Velopack, MIT) | PyInstaller/Nuitka: un pachet mare, cu **alarme false frecvente la antivirus** |
| **Pornire** | Sub o secundă, cu ReadyToRun | 2–5 secunde în modul onefile |
| **Actualizare automată** | Velopack: actualizări delta, sursă GitHub Releases, fără UAC | Trebuie făcută manual |
| **Semnarea codului** | Standard (signtool), fără probleme | Posibilă, dar antivirusul tot reacționează la bootloader |
| **Teste** | xUnit, rapide, rulate în CI pe Windows | pytest, excelent |
| **Viteza de dezvoltare** | Bună | Ceva mai mare |

**De ce C#:** utilizatorul țintă e un vârstnic nevăzător. Pentru el, o alarmă antivirus, un dialog UAC sau o pornire lentă înseamnă blocaj total, iar exact la acestea Python pierde. Singurul avantaj real al lui Python ar fi reutilizarea codului din ecosistemul NVDA, dar nu vom copia cod.

**De ce WPF:** text vectorial care se scalează curat, teme de culoare simple și o platformă matură pe .NET 10. WinUI 3 complică instalarea (runtime-ul Windows App SDK), iar WinForms scalează slab textul mare.

### 1.2 Diagrama componentelor

```
┌──────────────────────────── Hasi.App (WPF, specific Windows) ─────────────────────────────┐
│  ShellWindow (ecran complet, text mare)   KeyboardSource   Bootstrapper (DI + module)    │
└───────────┬──────────────────────────────────────┬──────────────────────────┬───────────┘
            │ afișare (VisualChannel)              │ InputEvent               │ înregistrează
┌───────────▼──────────────────────────────────────▼──────────────────────────▼───────────┐
│                    Hasi.Core  (.NET pur, fără Windows, 100% testabil)                    │
│                                                                                          │
│   KeyMap ──► Command ──► Shell (stiva de ecrane, bucla de evenimente) ──► IScreen        │
│                              │                        (MenuScreen, ValueScreen,          │
│                              │                         ConfirmScreen, TextScreen …)      │
│                              ▼ Utterance                                                 │
│           Announcer  (coadă, priorități, întrerupere la tastă, „repetă”, „unde sunt”)    │
│                              │                                                           │
│                              ▼                                                           │
│           OutputHub ──► IOutputChannel:  Speech │ Visual │ Diagnostic │ (Braille – mai târziu)
│                                                                                          │
│   Servicii (doar interfețe): ISettingsStore, ILocalizer, IClock, INetworkStatus,         │
│   ICredentialStore, IDiagnostics, IScreenReaderMonitor, ISystemActions                   │
└──────────────────────────────────────────────────────────────────────────────────────────┘
        ▲ implementează IModule                         ▲ implementează ISpeechEngine / servicii
┌───────┴───────────────────┐       ┌──────────────────┴─────────────────────────────────────┐
│ Hasi.Modules.*            │       │ Hasi.Speech: RHVoice, Sapi5, OneCore,                   │
│ Settings, About, KeyTutor,│       │              ieșirea audio, Null                        │
│ Exit … (Faza 1+: Clock,   │       │ Hasi.Text.Ro: normalizator românesc, plurale            │
│ Editor, Agenda …)         │       │ Hasi.Platform.Windows: Credential Manager, autostart,   │
└───────────────────────────┘       │   detectare cititoare de ecran, hotkey, rețea           │
                                    └─────────────────────────────────────────────────────────┘
```

**Reguli de dependență** (verificate automat cu un test de arhitectură):
- `Hasi.Core` nu depinde de nimic specific Windows sau de vreun modul concret.
- Modulele depind doar de `Hasi.Core`, nu unele de altele.
- Doar `Hasi.App` cunoaște toate piesele și le leagă prin injectarea dependențelor.

### 1.3 Fluxul unei apăsări de tastă

```
tastă fizică ─► KeyboardSource (WPF PreviewKeyDown, inclusiv Alt/F10)
            ─► Announcer.OnKeyPressed()  → oprește imediat vorbirea curentă
            ─► KeyMap: KeyChord → Command (Next, Back, Help …) + caracterul tastat
            ─► Shell.Dispatch(InputEvent) → ecranul curent → ScreenResult
            ─► Shell aplică rezultatul (Push / Pop / Confirm …) → produce Utterance
            ─► Announcer → ITextNormalizer (română) → OutputHub → canale
```

`Shell` rulează pe un singur fir, ca o buclă de evenimente. Timerele (de exemplu mementourile din Faza 1) își trimit evenimentele în aceeași buclă. Comportamentul e determinist, deci testele sunt simple.

### 1.4 Interfețele principale

Semnăturile de mai jos sunt cele implementate în etapele 0.2–0.3. Față de schița inițială s-au schimbat două lucruri:
- ecranele primesc un `IScreenContext` (textele din `ro.json` și opțiunile de navigare), ca să nu-și țină singure dependențele;
- `ISpeechEngine.Speak` doar adaugă textul în coada motorului, fără să aștepte. Așa bucla de evenimente nu se blochează niciodată.

```csharp
// Un modul expune doar un ecran-rădăcină. Nucleul nu știe ce conține.
public interface IModule
{
    string Id { get; }            // "settings", "about"
    string TitleKey { get; }      // cheie în ro.json
    IScreen CreateRootScreen(IModuleContext context);
}

public interface IScreen
{
    string Title { get; }                                        // folosit de „unde sunt”
    Utterance Enter(EnterReason reason, IScreenContext context); // unde ești, câte opțiuni, care e selectată
    ScreenResult Handle(InputEvent input, IScreenContext context);
    Utterance Help(IScreenContext context);                      // F1 / Ctrl+H
}

// Ce poate cere un ecran de la Shell (logica nu atinge niciodată fereastra):
//   Say(Utterance) | Push(IScreen) | Pop(prefix, then) | Replace(IScreen)
//   | Confirm(question, onYes) | Unhandled | Handled

public sealed record InputEvent(Command Command, KeyChord Key, char? Character = null);

public enum Command { None, Next, Previous, First, Last, Activate, Back, Increase, Decrease,
                      Help, RepeatLast, WhereAmI, StopSpeech, Exit /* … */ }

public sealed record Utterance(string Text, UtterancePriority Priority = UtterancePriority.Interrupt,
                               Earcon Earcon = Earcon.None, string? DisplayText = null,
                               bool Sensitive = false);

// Ce primesc canalele: textul original plus textul normalizat, adică exact ce primește motorul.
public sealed record Announcement(Utterance Utterance, string SpokenText);

// Punctul de extensie general pentru ieșiri. Vorbirea e prima implementare; Braille vine mai târziu.
public interface IOutputChannel
{
    void Present(Announcement announcement);
    void Cancel();
}

public interface ISpeechEngine : IDisposable
{
    string Id { get; }
    string? CurrentVoiceName { get; }
    void Speak(string text);                       // pune în coadă, nu blochează
    void Stop();                                   // oprește imediat și golește coada
    Task WaitUntilIdleAsync(TimeSpan timeout);     // de exemplu înainte de „La revedere”
}

public interface ITextNormalizer { string Normalize(string text); }
```

**Testarea meniurilor fără sunet și fără fereastră:**

```csharp
var h = new ShellHarness(modules: [settings, about]);
h.Start();
h.Press(Key.Down, Key.Enter);
h.LastSpoken.Should().Be("Setări. 12 opțiuni. Selectat: Voce, 1 din 12.");
```

Pe lângă asta, folosim **transcrieri de referință**: un fișier text cu „tastă → ce trebuie să se audă” pentru fiecare ecran. Orice schimbare de formulare apare clar în diff, iar tu poți revizui formulările direct în acele fișiere.

### 1.5 Stratul de vorbire

- **Lanț de rezervă configurabil:** motorul preferat → celelalte → semnal sonor + text pe ecran. HASI nu rămâne niciodată mut fără explicație.
- **RHVoice/Paul, în doi pași:**
  1. **Faza 0:** prin **SAPI5**, cu vocea instalată de installer-ul oficial Paul, nemodificat. Programul e separat, deci nu apare nicio problemă de licență la legare, iar integrarea e cea mai simplă.
  2. **După audit de licență și un experiment de probă:** apeluri directe în `RHVoice.dll` (P/Invoke), cu ieșire audio proprie (WASAPI). Câștigăm oprire instantanee, poziția cuvântului curent, instalare fără administrator și voce inclusă în pachetul HASI. Decizia o scriu ca ADR, după ce vedem rezultatele.
- **OneCore:** vocea Microsoft Andrei (ro-RO), dacă e instalat pachetul de vorbire românesc din Windows.
  Constatare din etapa 0.3: `System.Speech` sub .NET 10 vede vocile OneCore și prin SAPI5 (Andrei apare în listă), deci rezerva românească funcționează deja prin SAPI5. Motorul `OneCoreEngine` direct a fost adus înainte din etapa 0.5, în formă minimă, și intervine doar dacă SAPI5 nu pornește.
- **Lanțul de rezervă (etapa 0.5):** `FallbackSpeechEngine` (în nucleu) primește vocile în ordinea preferinței. Fiecare voce are propria instanță de motor. Dacă vocea activă raportează o eroare în timpul rulării, lanțul trece la următoarea, iar HASI spune „Vocea X nu mai funcționează. Folosesc vocea Y.” și repetă ultimul mesaj. RHVoice.dll direct: audit și experiment în ADR 0004, decizie în așteptare.
- **Coada de mesaje:** priorități `Interrupt` (navigare), `Queue` (citire continuă), `Notification` (memento: așteaptă finalul propoziției curente, apoi vorbește).
- **Normalizare** (`Hasi.Text.Ro`), ca lanț de etape pure, fiecare cu tabel de teste:
  1. Unicode NFC; ş/ţ/Ş/Ţ (sedilă) → ș/ț/Ș/Ț (virgulă). Opțional, reparăm mojibake-ul frecvent (de exemplu `Ã®` → `î`).
  2. Abrevieri: str., nr., bl., sc., ap., et., jud., dl., dna., prof., dr., etc., ș.a., ș.a.m.d.
  3. Sume: „12,50 lei” → „doisprezece lei și cincizeci de bani”; „1 leu”; „20 de lei”.
  4. Date: „21.10.2026” → „21 octombrie 2026”; „1 martie” → „întâi martie”.
  5. Ore: „14:30” → „ora paisprezece și treizeci de minute”.
  6. Numere, cu genul corect: „2 ore” → „două ore”, „2 dosare” → „două dosare”, „2 bani” → „doi bani”.
  7. Simboluri: %, €, @, /.

  Textul afișat pe ecran rămâne neschimbat (numai diacriticele se corectează). Normalizarea se aplică doar fluxului vocal.

  Implementat în etapa 0.4, în `src/Hasi.Text.Ro`. Pe lângă etapele de mai sus mai sunt: adrese de e-mail și web, telefoane (citite cifră cu cifră, pe grupe), unități (km, kg, °C…), procente, ordinale („a 2-a”, „al 3-lea”), numere de versiune („0.1.0”) și coduri lungi (CNP, cifră cu cifră). Genul substantivelor numărate vine dintr-un mic lexicon (`Lexicon.cs`) și din terminațiile tipice de plural.
  Cazurile de test sunt în `tests/Hasi.Text.Ro.Tests/Cases/*.cases`, câte un rând de forma `scris => rostit`. Testerul poate adăuga rânduri direct acolo.

### 1.6 Coexistența cu cititoarele de ecran

**Schimbare de plan (24 octombrie 2026, decizia testerului):** etapa 0.9 a fost scoasă din Faza 0. Motivul este un test făcut de tester cu NVDA pornit: s-a auzit o singură voce, a HASI. Condițiile testului:
- NVDA repornit, fără modul de somn activ;
- tastatura numerică în aspectul desktop al NVDA;
- ecoul tastării în „Test pronunție”.

Totul a funcționat corect. Detaliile sunt în ADR 0003, acum marcat „înlocuit”.

**Nu se mai fac:**
- add-on-ul „HASI pentru NVDA”;
- trimiterea textului prin NVDA (NVDA Controller Client);
- notificările UI Automation pentru Narator;
- scripturile JAWS.

**Rămâne doar detectarea Naratorului, mutată în etapa 0.10.** La pornire, dacă Naratorul e activ (procesul `Narrator.exe`), HASI spune: „Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta Windows și Enter.”

Riscurile R1 și R2 (§3) rămân în listă, ca să știm ce urmărim dacă problema reapare.

### 1.7 Tastatura

**În Faza 0 nu folosim hook global de taste.** Tastele se preiau doar cât timp fereastra HASI are focusul (aplicația e pe tot ecranul). În plus:
- o **tastă globală de revenire** (`RegisterHotKey`), propusă Ctrl+Alt+H, readuce HASI în prim-plan;
- dacă HASI pierde focusul (o notificare Windows, o altă aplicație), spune: „HASI nu mai este în prim-plan. Apăsați Control, Alt și H pentru a reveni.”;
- **Alt+F4** este interceptat și cere confirmare.

**Tastele aprobate** (configurabile din `KeyMap`):

Pe laptopuri, tastele F1–F3, Home și End cer adesea tasta Fn, iar tastatura numerică lipsește. De aceea, fiecare comandă globală are și o **alternativă fără taste F**, cu litere ușor de reținut în română.

| Tastă | Alternativă pentru laptop | Tastatura numerică | Acțiune |
|---|---|---|---|
| ↑ / ↓ | – | 8 / 2 | opțiunea anterioară / următoare |
| ← / → | – | 4 / 6 | micșorează / mărește valoarea (în setări) |
| Enter | – | Enter / 5 | alege |
| Escape | – | 0 | înapoi (mereu) |
| Home / End | Ctrl+↑ / Ctrl+↓ | 7 / 1 | prima / ultima opțiune |
| F1 | **Ctrl+H** (help) | / | ajutor pentru ecranul curent |
| F2 | **Ctrl+R** (repetă) | * | repetă ultimul mesaj |
| F3 | **Ctrl+U** (unde sunt) | - | unde sunt (calea în meniu) |
| Ctrl (singur) | – | – | oprește vorbirea |
| Alt+F4 | meniul Ieșire | – | închide HASI (cu confirmare) |
| Ctrl+Alt+H | – | – | readuce HASI în prim-plan (global, etapa 0.10) |
| Ctrl+Shift+F12 | **Ctrl+Shift+D** | – | pornește / oprește modul de diagnostic (etapa 0.8) |
| orice altă tastă | | | întrerupe vorbirea |

Ecranul „Învață tastele” prezintă întâi alternativele fără taste F, pentru că merg pe orice tastatură.

**Capătul listei:** implicit, lista nu sare de la final la început. HASI scoate un sunet scurt și spune „Sfârșitul listei” (sau „Începutul listei”). Utilizatorii avansați pot activa saltul din Setări → „Capătul listei” (etapa 0.6). Opțiunea există deja în `ShellOptions.WrapLists`.

### 1.8 Alte decizii de arhitectură

- **Texte:** în `Resources/ro.json` (cheie → text), cu suport de plurale pe regulile CLDR pentru română: *one* „1 opțiune”, *few* „2 opțiuni”, *other* „20 de opțiuni”. Am ales JSON în loc de `.resx` pentru că `.resx` nu știe plurale și e greu de editat de cineva care nu e programator.
- **Setări:** `%APPDATA%\HASI\settings.json`, cu versiune de schemă și migrări.
- **Parole:** în Windows Credential Manager (`CredWrite`/`CredRead`), cu ținte de forma `HASI:email:<cont>`.
- **Module:** înregistrate explicit în `Bootstrapper`; nu încărcăm DLL-uri dinamic în Faza 0. Meniul principal se construiește din lista modulelor, iar modulele se pot ascunde din setări, ca un începător să vadă puține opțiuni.
- **Fără telemetrie.** Singurul acces la rețea în Faza 0 este verificarea actualizărilor, care se poate dezactiva.
- **Actualizări fără sursă unică:** sursa vine din configurare (`update.source`: `github` + depozit, `http` + adresă sau `folder` + cale). În cod, ea e o interfață `IUpdateSource`, implementată peste sursele Velopack (GitHub, server web simplu, folder local). Dacă mutăm proiectul de pe GitHub, schimbăm doar configurarea. Folderul local e util pentru testele tale și pentru instalări fără internet.

---

## 2. Structura repository-ului

```
Hasi/
├─ CLAUDE.md                       convenții pentru dezvoltare (în engleză)
├─ README.md
├─ LICENSE                         GPL-3.0-or-later (propunere, vezi §5)
├─ THIRD-PARTY-NOTICES.md          RHVoice, Paul, NAudio, MULTEXT-East, Velopack …
├─ global.json                     fixează versiunea SDK .NET 10
├─ Directory.Build.props           nullable, avertismente tratate ca erori, analizoare
├─ Directory.Packages.props        versiunile pachetelor NuGet, centralizat
├─ Hasi.slnx
├─ src/
│  ├─ Hasi.Core/                   Shell, ecrane, meniuri, comenzi, Announcer, OutputHub, interfețe
│  ├─ Hasi.Text.Ro/                normalizator românesc, numere în litere, plurale
│  ├─ Hasi.Speech/                 RHVoice (direct), Sapi5, OneCore, ieșirea audio, Null
│  ├─ Hasi.Platform.Windows/       Credential Manager, autostart, detectarea Naratorului, hotkey, rețea
│  ├─ Hasi.App/                    WPF: ShellWindow, KeyboardSource, VisualChannel, Bootstrapper
│  │  └─ Resources/ro.json         toate textele interfeței
│  └─ Modules/
│     ├─ Hasi.Modules.Settings/
│     ├─ Hasi.Modules.About/       „Despre HASI”, cu toate atribuirile
│     ├─ Hasi.Modules.KeyTutor/    „Învață tastele”
│     └─ Hasi.Modules.Exit/        închide HASI / repornește / oprește calculatorul
├─ tests/
│  ├─ Hasi.Core.Tests/             navigare simulată, transcrieri de referință
│  ├─ Hasi.Text.Ro.Tests/
│  ├─ Hasi.Modules.*.Tests/
│  ├─ Hasi.Architecture.Tests/     verifică regulile de dependență
│  ├─ Hasi.Transcripts.Tests/      transcrierile de referință (formulările validate de tester)
│  └─ Hasi.Integration.Tests/      motoare de voce reale (rulate local)
├─ installer/                      Velopack, scripturi de publicare și semnare
├─ docs/
│  ├─ architecture.md              versiunea aprobată a acestui document
│  ├─ adr/                         decizii de arhitectură (ADR)
│  ├─ data-sources.md              licență, limite, plan de rezervă pentru fiecare sursă
│  ├─ licenses.md
│  └─ manual-tests/faza-0.md       scenariile tale de test
└─ .github/workflows/ci.yml        build + teste pe windows-latest
```

---

## 3. Riscuri tehnice

| # | Risc | Impact | Măsuri |
|---|---|---|---|
| R1 | **Vorbire dublă cu NVDA/Narator.** Cititorul anunță focusul și face ecou tastelor peste HASI. | Mare → urmărit | **Observat pe 24 octombrie 2026:** cu NVDA pornit (fără modul de somn), testerul a auzit o singură voce, a HASI, inclusiv la tastarea în „Test pronunție”. Etapa 0.9 a fost scoasă (ADR 0003). Rămâne: mesajul pentru Narator, la pornire (etapa 0.10). Dacă vorbirea dublă reapare (altă versiune NVDA, alte setări, alt cititor), redeschidem ADR 0003. |
| R2 | **NVDA preia tastele.** În aspectul desktop, tastatura numerică comandă cursorul de examinare, iar Insert/CapsLock sunt modificatori NVDA. | Mare → urmărit | **Observat pe 24 octombrie 2026:** tastatura numerică în aspectul desktop al NVDA a funcționat în HASI. Urmărim același lucru la fiecare test cu NVDA; dacă reapare, redeschidem ADR 0003. |
| R3 | **Licența mixtă RHVoice:** antet LGPL-2.1+, dar `LICENSE.md` din rădăcină e GPL-2.0. | Mare, juridic | Faza 0 folosește SAPI (program separat, nemodificat). Legăm direct DLL-ul doar după audit pe fișiere. Dacă apare cod GPL-2.0-only în DLL, rămânem pe SAPI. |
| R4 | **RHVoice prin SAPI:** installer-ul poate cere drepturi de administrator; oprirea prin SAPI e mai lentă; nu știm ce formă a diacriticelor așteaptă lexiconul lui Paul; numerele pot fi citite inconsecvent de motor. | Mediu | Test de oprire (țintă sub 50 ms). Test de pronunție pe o listă de cuvinte cu ș/ț (ambele forme). Normalizarea noastră trimite deja numerele scrise în litere. |
| R5 | **Pierderea focusului:** notificări, UAC, Windows Update sau alte aplicații fură fereastra și utilizatorul „se pierde”. Pe desktopul securizat UAC, HASI nu poate nici vorbi, nici asculta. | Mare | Tastă globală de revenire; detectarea pierderii focusului și anunț; instalare per-utilizator, ca să evităm UAC; documentăm limita UAC. |
| R6 | **Hook-uri globale de taste** (dacă le adăugăm mai târziu): Windows le elimină în tăcere dacă răspund greu (LowLevelHooksTimeout), nu primesc tastele aplicațiilor ridicate (UIPI), iar unele antivirusuri le consideră keyloggere. | Mediu | Evitate în Faza 0. Dacă devin necesare, rulează pe un fir dedicat, fără alocări în callback. |
| R7 | **SmartScreen/antivirus pe un executabil nesemnat.** Un utilizator nevăzător nu poate trece singur de acel dialog. | Mare | SignPath Foundation (§5.1), cu Azure Artifact Signing (cost lunar) ca rezervă. Până atunci, instalare asistată. |
| R8 | **Windows 10:** suportul pentru consumatori, inclusiv ESU, s-a încheiat pe 13 octombrie 2026. Vocile OneCore românești pot lipsi. | Mic–mediu | Testăm și pe Windows 10, dar nu folosim API-uri exclusive Windows 11 fără alternativă. |
| R9 | **Dispozitivul audio** se schimbă (căști Bluetooth, revenire din repaus). | Mediu | Motorul detectează schimbarea și se reinițializează; test manual dedicat. |
| R10 | **Româna e complexă la normalizare:** genul numeralelor, „de” după 20, „întâi”, abrevieri ambigue (de exemplu „st.”). | Mediu | Tabele mari de teste; tu raportezi pronunțiile greșite, iar fiecare devine un caz de test. |
| R11 | **Taste ținute apăsate** (autorepetare) produc avalanșe de mesaje. | Mic | Doar ultimul mesaj se rostește; repetarea de navigare are o limită. |
| R12 | **Jurnalul de diagnostic poate conține date private** (texte de e-mail, parole). | Mare | Câmpurile marcate `Sensitive` apar mascate (•••); modul e oprit implicit; jurnalul rămâne local, iar tu decizi ce trimiți. |

---

## 4. Planul Fazei 0

Fiecare etapă înseamnă commit-uri mici (Conventional Commits, în engleză) cu teste automate. La finalul fiecărei funcționalități primești lista de scenarii manuale, iar următoarea etapă începe după raportul tău.

**Oprire obligatorie după etapa 0.3.** Primești o versiune care vorbește și pe care o poți rula, plus scenariile M0-01…M0-12. Trec la 0.4 abia după raportul tău.

**Formulările rostite** din §4.2 sunt păstrate în transcrierile de referință din `tests/Hasi.Transcripts.Tests/Transcripts/`. Le poți corecta direct acolo. Testul va pica, iar eu aliniez `ro.json` la formularea ta.

| Etapă | Livrabil | Teste automate |
|---|---|---|
| **0.1 Fundație** | `git init`, soluția și proiectele goale, CI pe GitHub Actions, LICENSE, CLAUDE.md, analizoare | build verde, testul de arhitectură |
| **0.2 Nucleul de meniuri** | `Shell`, `IScreen`, `MenuScreen`, `ConfirmScreen`, comenzi, `KeyMap`, `Announcer`, `OutputHub`, `Localizer` cu plurale, `ShellHarness` | navigare, anunțuri la intrare, Escape, capetele listei, confirmări |
| **0.3 Prima aplicație care vorbește** | `ShellWindow` pe tot ecranul, `KeyboardSource`, motorul SAPI5, meniul principal | test de oprire a vorbirii, transcrierea meniului principal |
| **0.4 Normalizator românesc v1** | diacritice, numere, sume, date, ore, abrevieri | sute de cazuri în tabele |
| **0.5 Motoarele de voce** | OneCore, RHVoice/Paul prin SAPI, alegerea vocii, lanțul de rezervă; experiment de probă cu `RHVoice.dll` direct + audit de licență → ADR | lanțul de rezervă (cu motoare simulate), coada și prioritățile |
| **0.6 Setări** | voce, viteză, volum, ton, ecoul tastelor, culori (seturi cu contrast ridicat), mărimea textului, persistență | salvare, reîncărcare, migrare |
| **0.7 Ajutor și orientare** | F1, F2, F3, „Învață tastele”, „Despre HASI”, meniul de ieșire cu confirmări, Alt+F4 | un test pentru fiecare tastă globală, pe toate ecranele |
| **0.8 Diagnostic și erori** | mod de diagnostic (fișier de sesiune), jurnal permanent de erori, la eroare gravă mesaj rostit și repornire automată | formatul jurnalului, mascare `Sensitive` |
| ~~0.9 Coexistența cu cititoarele de ecran~~ | **scoasă din Faza 0** pe 24 octombrie 2026 (decizia testerului, ADR 0003): cu NVDA, HASI funcționează cu o singură voce. Detectarea Naratorului trece în 0.10. | – |
| **0.10 Integrare în sistem** | pornire automată (opțională), o singură instanță, tasta globală de revenire, detectarea pierderii focusului; **detectarea Naratorului** la pornire, cu mesajul din §1.6 | detectarea Naratorului cu un detector simulat; restul manual |
| **0.11 Instalare și actualizare** | installer Velopack per-utilizator, actualizare cu anunț rostit; sursa e configurabilă (vezi §1.8); semnare prin SignPath Foundation, dacă proiectul e acceptat | publicare de test în CI |

**Gata, pentru Faza 0, înseamnă:** toate scenariile M0-xx de mai jos trec la tine, pe Windows 11 și pe Windows 10, cu și fără NVDA pornit.

### 4.1 Formatul jurnalului de diagnostic

Fișierul: `%LOCALAPPDATA%\HASI\diagnostic\sesiune-2026-10-21_14-30-05.log`

```
14:30:05.120 START  HASI 0.1.0, Windows 11 26300
14:30:05.130 ENGINE sapi5 voice="Paul"
14:30:05.400 SCREEN Meniu principal
14:30:05.401 SAY    [Interrupt] text="Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4."
14:30:05.401        normalized="Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4."
14:30:05.402 SPEAK  engine=sapi5 text="Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4."
14:30:07.010 KEY    Down
14:30:07.011 CMD    Next
14:30:07.012 STOP   engine=sapi5
14:30:07.012 SAY    [Interrupt] text="Învață tastele, 2 din 4."
14:30:07.012        normalized="Învață tastele, 2 din 4."
14:30:07.013 SPEAK  engine=sapi5 text="Învață tastele, 2 din 4."
```

Fiecare mesaj apare pe trei niveluri, ca să poți spune exact unde e greșeala:
- `text` = ce a cerut ecranul (textul din `ro.json`);
- `normalized` = ce a produs normalizatorul românesc;
- `SPEAK` = exact șirul trimis motorului de voce.

Dacă `normalized` e corect, dar pronunția e greșită, problema e a vocii. Dacă `normalized` e deja greșit, problema e a normalizatorului. Mesajele marcate `Sensitive` (de exemplu parolele) apar ca `•••` pe toate cele trei niveluri.

Modul de diagnostic se pornește din Setări → Diagnostic → „Mod de diagnostic” sau cu Ctrl+Shift+F12 / Ctrl+Shift+D (etapa 0.8). Până atunci se pornește din linia de comandă, cu `--diagnostic`. Opțiunea „Copiază jurnalul pe Desktop” îl pune acolo unde îl poți găsi ușor ca să mi-l trimiți.

**Ecranul „Test pronunție”** (Setări → Diagnostic → Test pronunție, etapa 0.4): scrii un text, apeși Enter și îl auzi normalizat. Ecranul îl afișează și scris, iar ambele variante intră în jurnalul de diagnostic, ca să-mi poți raporta pronunțiile greșite.

### 4.2 Scenariile de testare manuală (Faza 0)

Formulările sunt propuneri; tu le validezi după ureche.

| ID | Ce apeși | Ce trebuie să auzi |
|---|---|---|
| M0-01 | Pornești HASI | „Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4.” |
| M0-02 | ↓ de 3 ori | „Învață tastele, 2 din 4”, „Despre HASI, 3 din 4”, „Ieșire, 4 din 4” |
| M0-03 | Încă o dată ↓ pe ultima opțiune | un sunet scurt și „Sfârșitul listei. Ieșire, 4 din 4.” (lista nu sare la început) |
| M0-04 | Home, apoi End | „Setări, 1 din 4”, apoi „Ieșire, 4 din 4” |
| M0-05 | Enter pe Setări | „Setări. 12 opțiuni. Selectat: Voce, 1 din 12.” |
| M0-06 | Escape | „Meniu principal. Selectat: Setări, 1 din 4.” |
| M0-07 | Escape în meniul principal | „Sunteți în meniul principal. Pentru a închide HASI, alegeți Ieșire.” (nu se închide) |
| M0-08 | F1 în Setări | ajutor scurt despre ecranul curent și tastele lui, apoi „Apăsați orice tastă pentru a continua.” |
| M0-09 | F2 după orice mesaj | exact ultimul mesaj, repetat |
| M0-10 | F3 în Setări → Viteză | „Sunteți în: Meniu principal, Setări, Viteză.” |
| M0-11 | Apeși ↓ în timp ce HASI vorbește un mesaj lung | vorbirea se oprește instantaneu și auzi doar opțiunea nouă |
| M0-12 | Ctrl în timpul unui mesaj | tăcere imediată |
| M0-13 | Setări → Viteză, apoi → de 3 ori | „Viteză 60”, „70”, „80”, fiecare rostit deja cu noua viteză |
| M0-14 | Setări → Voce, alegi Microsoft Andrei | confirmare rostită cu vocea nouă: „Vocea a fost schimbată: Microsoft Andrei.” |
| M0-15 | Dezinstalezi vocea Paul și pornești HASI | HASI vorbește cu o voce de rezervă și spune: „Vocea Paul nu este instalată. Folosesc vocea Microsoft Andrei.” |
| M0-16 | Setări → Ecoul tastelor → Caracter, apoi intri în „Învață tastele” și apeși A | „A” |
| M0-17 | „Învață tastele”, apoi apeși F3 | „F3: spune unde vă aflați.” (fără să execute acțiunea); Escape de două ori ca să ieși |
| M0-18 | Setări → Culori: alegi „galben pe negru”, apoi mărimea textului: „foarte mare” | ecranul se schimbă imediat; auzi numele culorilor și al mărimii |
| M0-19 | Închizi și redeschizi HASI | setările de la M0-13, M0-14 și M0-18 sunt păstrate |
| M0-20 | Meniu principal → Despre HASI | versiunea, licența, RHVoice, vocea Paul și datele limbii române (cu autorii), MULTEXT-East, NAudio, System.Speech; navigabil rând cu rând cu ↑/↓ |
| M0-21 | Ieșire → Închide HASI | „Sigur doriți să închideți HASI? Enter pentru da, Escape pentru nu.” Escape te întoarce; Enter închide cu „La revedere.” |
| M0-22 | Alt+F4 | aceeași confirmare ca la M0-21 |
| M0-23 | Ieșire → Oprește calculatorul → Escape | confirmare, iar Escape anulează („Anulat.”) |
| M0-24 | Pornești modul de diagnostic, faci 5 pași, alegi „Copiază jurnalul pe Desktop” | „Jurnalul a fost copiat pe Desktop.” Fișierul conține fiecare tastă și fiecare mesaj. |
| M0-27 | (etapa 0.10) Pornești Naratorul, apoi HASI | „Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta Windows și Enter.” |
| M0-28 | Deschizi o altă fereastră peste HASI, apoi Ctrl+Alt+H | „HASI nu mai este în prim-plan…”, apoi, după tasta de revenire, HASI anunță ecranul curent |
| M0-29 | Pornești HASI a doua oară | nu se deschide a doua fereastră; cea existentă revine în față și anunță ecranul curent |
| M0-30 | Setări → Pornire automată → Da, apoi repornești Windows | HASI pornește singur și vorbește după autentificare |
| M0-31 | Instalezi o versiune mai veche, apoi publicăm una nouă | „Este disponibilă o versiune nouă a HASI. Apăsați Enter pentru a actualiza.” → repornire → versiunea nouă în „Despre” |
| M0-32 | Scoți căștile Bluetooth în timpul vorbirii | vorbirea continuă pe difuzoare, fără blocaj |
| M0-33 | Setări → Diagnostic → Test pronunție; scrii „Plata de 12,50 lei pe 1.03.2026 la 14:30” și apeși Enter | „Plata de doisprezece lei și cincizeci de bani pe întâi martie două mii douăzeci și șase la ora paisprezece și treizeci de minute”. Ecranul arată ambele variante, iar jurnalul de diagnostic le are pe amândouă. |

---

## 5. Decizii aprobate (21 octombrie 2026)

| # | Subiect | Decizie |
|---|---|---|
| 1 | Limbaj și UI | C# pe **.NET 10 LTS**, cu WPF. SDK-ul e fixat în `global.json` (ADR 0001). |
| 2 | Arhitectură | §1, cu ajustările din acest document. |
| 3 | Licență | **GPL-3.0-or-later**. |
| 4 | Taste | §1.7. Fiecare comandă globală are și o alternativă fără taste F (Ctrl+H, Ctrl+R, Ctrl+U, Ctrl+↑/↓, Ctrl+Shift+D), toate configurabile din `KeyMap`. Revenirea cu Ctrl+Alt+H e aprobată. |
| 5 | Capătul listei | Fără salt, cu sunet și „Sfârșitul listei”. Saltul se poate activa din Setări, pentru utilizatorii avansați. |
| 6 | Găzduire | Depozit GitHub public, cu versiunile în GitHub Releases. Sursa actualizărilor e configurabilă (§1.8). |
| 7 | Semnare | Încercăm SignPath Foundation (§5.1). Până atunci construim nesemnat. |
| – | Test pronunție | Ecran separat, în Setări → Diagnostic, nu în „Învață tastele”. |
| – | Jurnal | Fiecare mesaj apare cu `text`, `normalized` și `SPEAK` (§4.1). |
| – | Ritm | Oprire obligatorie după etapa 0.3, pentru testul tău după ureche. |

### 5.1 SignPath Foundation: condiții și ce ai de făcut

Condițiile sunt cele din termenii publicați pe signpath.org/terms (pagina e marcată „draft”, deci le reverificăm la aplicare).

**Condiții pentru proiect:**
- licență aprobată OSI, fără dublă licențiere comercială, pentru toate componentele (GPL-3.0-or-later e în regulă);
- nicio componentă proprietară; bibliotecile de sistem sunt permise;
- proiect întreținut activ și **deja publicat** în forma care va fi semnată, cu funcționalitatea descrisă pe pagina de descărcare;
- se semnează numai binare construite din codul nostru, într-un mod verificabil (CI public); componentele terțe (de exemplu RHVoice) trebuie să vină cu semnăturile lor;
- programul nu modifică sistemul fără avertisment și are dezinstalare;
- fiecare versiune semnată cere **aprobare manuală**.

**Condiții pentru echipă:**
- toți membrii folosesc **autentificare în doi pași (MFA)**, atât pe GitHub, cât și pe SignPath;
- roluri declarate: autori, recenzenți (orice pull request de la cineva din afara echipei e verificat de un membru) și aprobatori (cine aprobă semnarea).

**Condiții pentru site (README-ul proiectului ajunge):**
- o secțiune „Code signing policy”, vizibilă pe pagina principală și pe cea de descărcare, cu textul „Free code signing provided by SignPath.io, certificate by SignPath Foundation”;
- lista rolurilor și a membrilor;
- o politică de confidențialitate. Atenție: textul-tip („This program will not transfer any information to other networked systems unless specifically requested by the user”) nu se potrivește perfect, pentru că **verificarea automată a actualizărilor** e un transfer nesolicitat explicit. Varianta corectă: o politică scurtă care spune că se contactează doar sursa de actualizări (fără date personale), iar verificarea se poate dezactiva.

**Ce ai tu de făcut, în ordine:**
1. Activezi MFA pe contul GitHub, dacă nu e deja activ.
2. Îmi spui numele contului sau al organizației GitHub. Eu creez depozitul public, dar numai după confirmarea ta, pentru că e o publicare.
3. După etapa 0.11 publicăm prima versiune **nesemnată** (de exemplu 0.1.0) în GitHub Releases. Fără o versiune publicată, cererea nu e acceptată.
4. Completezi formularul de pe signpath.org/apply. Formularul se încarcă dinamic și nu i-am putut citi câmpurile, deci le verificăm împreună atunci.
5. După acceptare, eu integrez semnarea în GitHub Actions, iar tu aprobi manual fiecare cerere de semnare.

## Surse

- [.NET 8 și 9 – sfârșitul suportului (Microsoft)](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)
- [RHVoice (GitHub)](https://github.com/RHVoice/RHVoice) – `src/include/RHVoice.h`, `LICENSE.md`
- [Vocea Paul (GitHub, paul-ron)](https://github.com/RHVoice/paul-ron) și [pagina vocilor românești](https://rhvoice.org/ro-voices/)
- [Pachetele Arch rhvoice-voice-paul](https://archlinux.org/packages/extra-testing/x86_64/rhvoice-voice-paul/) / [rhvoice-language-romanian](https://archlinux.org/packages/extra-testing/x86_64/rhvoice-language-romanian/)
- [NVDA Controller Client](https://github.com/nvaccess/nvda/blob/master/extras/controllerClient/readme.md)
