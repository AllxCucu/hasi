# HASI – Răspuns după etapele 0.1–0.3

Etapele 0.1, 0.2 și 0.3 sunt gata: 9 commit-uri pe ramura `main`, iar toate cele **106 teste automate** trec (93 pentru nucleu, 7 de arhitectură, 6 transcrieri de referință). Mă opresc aici, cum ai cerut, până primesc raportul tău.

Ce s-a mai făcut, pe lângă cod:
- documentul aprobat `docs/architecture.md`, cu deciziile tale în §5 și condițiile SignPath în §5.1;
- ADR-urile 0001, 0002 și 0003 în `docs/adr/`;
- CLAUDE.md fără marcajul DRAFT, cu licența GPL-3.0-or-later și alternativele de taste;
- SDK-ul .NET 10.0.402, instalat cu winget și fixat în `global.json`.

---

## 1. Cum pornești aplicația

Din folderul proiectului (`C:\Users\cucua\Documents\dev\workspace\Hasi`), într-un terminal:

```
dotnet run --project src\Hasi.App -- --diagnostic
```

`--diagnostic` salvează tot ce s-a rostit și toate tastele apăsate în
`%LOCALAPPDATA%\HASI\diagnostic\sesiune-AAAA-LL-ZZ_HH-MM-SS.log`. Pentru testele tale, recomand să îl folosești mereu. Când ceva nu merge, trimite-mi acel fișier.

Alte opțiuni, utile până apar ecranele de Setări (etapa 0.6):

| Opțiune | Efect |
|---|---|
| `--rate -3` | vorbire mai lentă (de la -10 la 10; 0 = normal) |
| `--voice "Microsoft Andrei"` | forțează o anumită voce |

Exemplu: `dotnet run --project src\Hasi.App -- --diagnostic --rate -3`

După primul `dotnet run`, poți porni HASI și direct, cu dublu clic pe
`src\Hasi.App\bin\Debug\net10.0-windows10.0.19041.0\hasi.exe`.

**Cum închizi HASI:** Alt+F4, apoi Enter. Sau, din meniu: Ieșire → Închide HASI → Enter.

**Dacă HASI nu primește tastele:** uneori Windows nu dă focusul unei ferestre noi. În acest caz, după 2 secunde HASI spune: „HASI nu mai este în prim-plan… țineți apăsată tasta Alt și apăsați Tab.” Folosește Alt+Tab până ajungi la fereastra HASI. La revenire, HASI îți spune unde ești.

---

## 2. Ce trebuie instalat înainte

1. **Vocea Paul (RHVoice), pentru SAPI.** Descarcă și rulează installer-ul oficial
   `RHVoice-voice-Romanian-Paul-v4.2.1001.23-setup.exe`, de pe https://rhvoice.org/ro-voices/
   (linkul „Download the voice for other Windows programs”). Te rog să-mi spui:
   - dacă installer-ul a cerut drepturi de administrator;
   - dacă, după instalare, HASI **nu** mai spune „Vocea Paul nu este instalată”.

   Dacă mesajul apare în continuare, deschide jurnalul de diagnostic. Rândul `VOICES` arată toate vocile pe care le vede HASI. Trimite-mi-l: e posibil ca Paul să fie înregistrat doar pentru programele pe 32 de biți (riscul notat în ADR 0002).
2. **Pachetul de voce românesc din Windows.** Pe calculatorul tău **este deja instalat** (am văzut vocea Microsoft Andrei, ro-RO). Pe alt calculator: Setări → Oră și limbă → Vorbire → Adăugare voci → Română (România). Andrei e vocea de rezervă când Paul lipsește.
3. **Cititoarele de ecran:** coexistența cu NVDA și Narator vine în etapa 0.9. Până atunci, dacă testezi cu NVDA pornit, vei auzi două voci. Soluția temporară: în fereastra HASI apasă **NVDA+Shift+S** (în aspectul laptop: NVDA+Shift+Z), care pune NVDA în modul de somn doar pentru această aplicație (aceeași combinație îl trezește). Te rog să verifici că și tastatura numerică ajunge la HASI în acest mod: e exact întrebarea pe care trebuie s-o confirme experimentul din etapa 0.9. Naratorul îl oprești cu Control+Windows+Enter.

---

## 3. Scenariile de test M0-01…M0-12, adaptate la ce s-a implementat

Se găsesc și în `docs/manual-tests/faza-0.md`. „[sunet]” = un ton scurt înaintea mesajului.

| ID | Ce apeși | Ce trebuie să auzi |
|---|---|---|
| M0-01 | Pornești HASI | „Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4.” Fără Paul urmează: „Vocea Paul nu este instalată. Folosesc vocea Microsoft Andrei.” |
| M0-02 | Săgeata jos de 3 ori | „Învață tastele, 2 din 4.”, „Despre HASI, 3 din 4.”, „Ieșire, 4 din 4.” |
| M0-03 | Încă o dată săgeata jos | [sunet] „Sfârșitul listei. Ieșire, 4 din 4.” |
| M0-04 | Home, apoi End (pe laptop: Control + săgeată sus / Control + săgeată jos) | „Setări, 1 din 4.”, apoi „Ieșire, 4 din 4.” |
| M0-05 | Home, apoi Enter pe Setări | „Setări. 12 opțiuni. Selectat: Voce, 1 din 12.” |
| M0-06 | Escape | „Meniu principal. Selectat: Setări, 1 din 4.” |
| M0-07 | Încă o dată Escape | „Sunteți în meniul principal. Pentru a închide HASI, alegeți Ieșire.” (HASI rămâne deschis) |
| M0-08 | Enter pe Setări, apoi F1 (sau Control H) | „Ajutor pentru Setări. Folosiți săgețile sus și jos…”, la final: „…Aici veți putea schimba vocea, viteza vorbirii, culorile și alte preferințe.” |
| M0-09 | Săgeata jos, apoi F2 (sau Control R) | „Viteză, 2 din 12.”, apoi același mesaj încă o dată |
| M0-10 | F3 (sau Control U); apoi Enter pe Viteză și din nou F3 | „Sunteți în: Meniu principal, Setări.”, apoi „Viteză. Această funcție va fi disponibilă într-o versiune următoare…”, apoi „Sunteți în: Meniu principal, Setări, Viteză.” |
| M0-11 | F1 (mesaj lung), apoi săgeata jos în timp ce vorbește | vorbirea se oprește **imediat**; auzi doar opțiunea nouă |
| M0-12 | F1, apoi tasta Control în timp ce vorbește | liniște imediată, fără alt mesaj |

Ce te rog să urmărești în mod special:
- **la M0-11 și M0-12:** cât de repede se oprește vocea. Dacă se mai aude o bucată din mesajul vechi, spune-mi cât de lungă (cuvinte sau secunde);
- **pronunția:** cum citește vocea „HASI”, „4 din 4”, „12 opțiuni”. Normalizatorul românesc vine abia în etapa 0.4, deci acum numerele le citește vocea singură. Notează tot ce sună greșit; din fiecare fac un caz de test;
- **formulările:** orice mesaj confuz sau prea lung e un bug. Poți corecta formularea direct în `tests\Hasi.Transcripts.Tests\Transcripts\*.transcript` (rândurile care încep cu `<`). Testul va pica, iar eu aliniez textele aplicației la formularea ta.

Verificări în plus, opționale (X-01…X-06), sunt în `docs/manual-tests/faza-0.md`: litere fără funcție, tastatura numerică, Despre HASI, Ieșire cu anulare, Alt+F4, Alt+Tab.

---

## 4. Ce nu funcționează încă (nu raporta acestea)

| Ce lipsește | Când vine |
|---|---|
| Toate cele 12 opțiuni din Setări spun „va fi disponibilă într-o versiune următoare”. Viteza și vocea se schimbă doar cu `--rate` și `--voice`. Nimic nu se salvează. | 0.6 |
| Normalizarea textului românesc (numere, sume, date, ore, abrevieri, ş/ţ → ș/ț) | 0.4 |
| Ecranul „Test pronunție” | 0.4 |
| „Învață tastele”: acum spune doar că va fi disponibil | 0.7 |
| Ieșire → Repornește / Oprește calculatorul (acum există doar „Închide HASI”) | 0.7 |
| Pornirea/oprirea modului de diagnostic din meniu sau cu Control+Shift+D, plus „Copiază jurnalul pe Desktop” (acum doar `--diagnostic`) | 0.8 |
| Repornirea automată după o eroare gravă (acum HASI spune „A apărut o problemă neașteptată” și continuă; detaliile ajung în `%LOCALAPPDATA%\HASI\logs\errors.log`) | 0.8 |
| Coexistența cu NVDA/Narator: acum se aud două voci (vezi soluția temporară din secțiunea 2) | 0.9 |
| Tasta globală Control+Alt+H pentru revenire; pornirea automată cu Windows; o singură instanță (acum, dacă pornești HASI de două ori, se deschid două ferestre) | 0.10 |
| Installer, actualizare automată, semnare | 0.11 |
| Culori și mărimea textului configurabile (acum: galben pe negru, text mare, fix) | 0.6 |
| Ecoul tastelor | 0.6 |
| Sunetele sunt tonuri simple, generate | ulterior, dacă vrei altele |

Două lucruri pe care le-am făcut altfel decât în plan, ca să ți le semnalez:
- **Motorul OneCore** era în etapa 0.5. L-am adus în formă minimă, pentru că inițial credeam că Andrei e vizibil doar prin OneCore. Am constatat apoi că `System.Speech` sub .NET 10 îl vede și prin SAPI5, deci HASI folosește SAPI5 pentru Andrei. OneCore intervine doar dacă SAPI5 nu pornește și **nu a fost încă testat după ureche**.
- **Anunțul pentru pierderea focusului** era în etapa 0.10. L-am adus acum, într-o formă simplă (fără tasta globală de revenire), pentru că fără el tastele tale ar putea ajunge, fără niciun semn, în altă fereastră.

---

## 5. De știut

- **Un incident în testele mele.** La prima pornire de probă am trimis automat tastele săgeată jos, F3, Alt+F4 și Enter fără să verific ce fereastră avea focusul. Fereastra activă era terminalul (Windows Terminal), deci tastele au putut ajunge acolo. Jurnalul HASI din acea rundă conține și taste pe care nu le-am trimis eu, probabil ale tale. De atunci trimit taste numai după ce verific că HASI e în prim-plan, iar regula e scrisă în CLAUDE.md. Testul final a mers cap-coadă: F3, Control H, Alt+F4, Enter → „La revedere.” → HASI s-a închis normal.
- **Depozitul GitHub** încă nu există, deci CI-ul n-a rulat niciodată pe GitHub. Ca să-l creez public, îmi trebuie numele contului sau al organizației și confirmarea ta. E primul pas și pentru SignPath (§5.1 din `docs/architecture.md`: MFA activ, o versiune publicată, apoi formularul de pe signpath.org/apply).
- **Recenzie independentă cu Codex** (doar citire, fără modificări din partea lui). A găsit 7 probleme reale; le-am verificat și le-am corectat pe toate, cu teste:
  1. **Enter ținut apăsat** deschidea întrebarea de închidere și o confirma singur, prin autorepetarea tastei. Acum, ținute apăsat, se repetă doar săgețile și tastele care schimbă valori. Enter și Escape acționează o singură dată pe apăsare. Te rog să verifici asta și după ureche: ține Enter apăsat pe „Închide HASI”.
  2. Dacă vocea cea mai bună nu pornea, HASI nu le mai încerca pe celelalte. Acum le încearcă pe rând. Fără nicio voce, mesajul apare pe ecran, însoțit de tonul de eroare.
  3. Erorile apărute în timpul vorbirii treceau neobservate (liniște fără explicație). Acum se scriu în jurnalul de erori și se aude tonul de eroare.
  4. O eroare de scriere în jurnalul de diagnostic putea bloca ieșirea din HASI. Acum jurnalul nu mai poate bloca nimic, iar ieșirea se încheie oricum.
  5. și 6. Două condiții de concurență în motorul OneCore, care puteau tăia sau pierde mesaje.
  7. Un parametru inserat într-un text putea fi interpretat greșit drept alt parametru.
