# Regresie Faza 0 – o singură sesiune (40 de pași)

Lista se parcurge de sus în jos, fără să închizi HASI, până la pasul 37. Pașii care se pot verifica fără sunet
sunt și test automat: `tests/Hasi.Transcripts.Tests/Transcripts/08-regresie.transcript`. Fiecare pas spune ce apeși și ce
trebuie să auzi. Numerele din mesaje se aud în cuvinte („patru opțiuni”, „unu din patru”). [sunet] înseamnă
sunetul scurt de la capătul listei sau de eroare. Între paranteze sunt scenariile vechi acoperite.

**Pregătire (nu se numără):**
- Închizi HASI, Naratorul și programele de care nu ai nevoie.
- Deschizi **Notepad** și scrii „test”, fără să salvezi.
- Pornești dintr-un terminal: `dotnet run --project src\Hasi.App -- --diagnostic`.

Dacă un pas nu sună cum scrie aici, notează numărul pasului și ce ai auzit, apoi mergi mai departe.

## A. Pornire și navigare

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 1 | (nimic, HASI pornește) | „Bun venit în HASI. Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4.”, fără întârziere (M0-01, R-16) |
| 2 | Săgeata jos de 3 ori, apoi ții apăsată săgeata jos | „Învață tastele, 2 din 4.”, „Despre HASI, 3 din 4.”, „Ieșire, 4 din 4.”, apoi o singură dată [sunet] „Sfârșitul listei. Ieșire, 4 din 4.” (M0-02, M0-03, P-14) |
| 3 | Control + săgeata sus, apoi Control + săgeata jos | „Setări, 1 din 4.”, apoi „Ieșire, 4 din 4.” (M0-04) |
| 4 | Home, apoi Escape | „Setări, 1 din 4.”, apoi „Sunteți în meniul principal. Pentru a închide HASI, alegeți Ieșire.” (M0-07) |
| 5 | Cu Num Lock **oprit**, de pe tastatura numerică: 2, 8, 5, apoi 0 | „Învață tastele, 2 din 4.”, „Setări, 1 din 4.”, „Setări. 11 opțiuni. Selectat: Voce, 1 din 11.”, „Meniu principal. Selectat: Setări, 1 din 4.” (R-11, N-05) |
| 6 | Enter pe Setări, apoi Control + H | „Ajutor pentru Setări. Folosiți săgețile sus și jos …”, care se termină cu „Aici schimbați vocea, viteza vorbirii, culorile și alte preferințe. Fiecare schimbare se salvează imediat.” (M0-08) |
| 7 | Săgeata jos, apoi Control + R, apoi Control + U | „Viteză, 2 din 11.” de două ori, apoi „Sunteți în: Meniu principal, Setări.” (M0-09, M0-10) |
| 8 | F1 și, în timp ce vorbește, săgeata jos; apoi F1 și, în timp ce vorbește, Control | vorbirea se oprește imediat și auzi doar „Volum, 3 din 11.”; apoi liniște imediată. Nicio pocnitură, la oprire sau la pornire. (M0-11, M0-12, R-03, R-04) |

## B. Setări

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 9 | Home, Enter pe Voce; săgețile până la capăt | doar voci românești, cu „, actual” la vocea de acum, iar ultima opțiune e „Arată toate vocile” (S-01) |
| 10 | Alegi „Paul, RHVoice direct” și Enter | „Vocea a fost schimbată: Paul, RHVoice direct. Setări. Selectat: Voce, 1 din 11.”, rostit deja cu vocea nouă (S-01) |
| 11 | Viteză: Enter; săgeata dreapta de 2 ori, stânga de 2 ori; Escape | „Viteză. 50, viteza normală. Săgețile stânga și dreapta schimbă valoarea. Enter sau Escape vă întoarce.”, „55”, „60”, „55”, „50, viteza normală”, fiecare cu viteza nouă (S-03) |
| 12 | Volum: stânga de 2 ori, dreapta de 2 ori; Escape | „90”, „80” (tot mai încet), „90”, „100, volumul maxim” (S-04) |
| 13 | Culori → „Negru pe galben”, Enter; apoi înapoi la „Galben pe negru” | „Culori: Negru pe galben.” și ecranul se schimbă imediat; apoi „Culori: Galben pe negru.” (S-08) |
| 14 | Capătul listei → „Salt la celălalt capăt”; Escape; în meniul principal End, apoi săgeata jos | „Capătul listei: Salt la celălalt capăt. …”; apoi [sunet] „Setări, 1 din 4.” Pui la loc „Oprire la capăt, cu sunet”. (S-09) |
| 15 | Ecoul tastelor → „Fiecare cuvânt” | „Ecoul tastelor: Fiecare cuvânt. Setări. Selectat: Ecoul tastelor, 5 din 11.” (S-06, continuă la pasul 18) |

## C. Test pronunție (Setări → Diagnostic → Test pronunție)

Enter nu golește câmpul. Ca să pornești de la un câmp gol, apeși Escape, apoi Enter pe Test pronunție.

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 16 | Diagnostic, Enter | „Diagnostic. 5 opțiuni. Selectat: Test pronunție, 1 din 5.” |
| 17 | Enter pe Test pronunție, apoi Enter cu câmpul gol | „Test pronunție. Scrieți un text și apăsați Enter ca să-l auziți așa cum îl rostește HASI. Escape vă întoarce.”, apoi „Câmpul este gol. Scrieți mai întâi un text.” (P-01, P-04) |
| 18 | Scrii `bună ziua ` (cu spațiu la final), apoi Enter | „bună”, „ziua” (literele nu se aud), apoi „bună ziua”. După aceea: Escape, Escape și Ecoul tastelor → „Fiecare caracter”, apoi înapoi în Test pronunție. (S-06) |
| 19 | Scrii `abc`, Backspace; apoi Backspace până se golește câmpul | „a”, „b”, „c”, „Șters: c”, „Șters: b”, „Șters: a” (P-02) |
| 20 | `Plata de 12,50 lei pe 1.03.2026 la 14:30`, Enter | „Plata de doisprezece lei și cincizeci de bani pe întâi martie două mii douăzeci și șase la ora paisprezece și treizeci de minute”; pe ecran, scris și rostit (M0-33) |
| 21 | Pe rând, fiecare într-un câmp gol și cu Enter: `14:00` · `Bilet CFR la ANAF` · `2 autobuze, 2 tramvaie, 21 lei` · `tel. 0722 123 456` | „ora paisprezece” · „Bilet ce fe re la Anaf” · „două autobuze, două tramvaie, douăzeci și unu de lei” · „telefon zero șapte doi doi, unu doi trei, patru cinci șase” (A-01…A-03, C-04, P-06, P-10) |
| 22 | Într-un câmp gol, cu Num Lock **pornit**: 1, 2, 3 de pe tastatura numerică; Num Lock; 8 de pe tastatura numerică; Num Lock | „1”, „2”, „3”; „Tastatura numerică: navigare.”; „Textul scris: 123”; „Tastatura numerică: cifre.” (N-01…N-04) |

## D. Diagnostic

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 23 | Escape, apoi Comparare voci, Enter; Enter pe prima frază | „Varianta unu, Paul prin SAPI. …”, apoi „Varianta doi, Paul prin RHVoice direct. …”. Dacă apare mesajul că lipsește o voce, notează-l și treci mai departe. (R-02) |
| 24 | Escape; Mod de diagnostic, Enter | „Mod de diagnostic. 2 opțiuni. Selectat: Pornit: tastele și mesajele se scriu într-un jurnal, doar pe acest calculator, actual, 1 din 2.” (G-03) |
| 25 | Escape; Control + Shift + D de două ori | „Modul de diagnostic este oprit.”, apoi „Modul de diagnostic este pornit. Tastele și mesajele se scriu într-un jurnal, doar pe acest calculator.” (G-01) |
| 26 | Simulare oprire, Enter; Escape (nu schimbi nimic) | „Simulare oprire. 2 opțiuni. Selectat: Pornită: repornirea și oprirea sunt doar un test, actual, 1 din 2.”, apoi „Diagnostic. Selectat: Simulare oprire, 5 din 5.” (O-05) |

## E. Învață tastele și Despre

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 27 | Escape, Escape; Învață tastele; apoi: săgeata jos, Control + săgeata sus, `ș`, Control + Shift + D, Alt + F4 | „Învață tastele. Apăsați orice tastă …”, „Săgeata jos: trece la opțiunea următoare.”, „Control plus săgeata sus: duce la prima opțiune.”, „Litera ș: când scrieți, scrie acest caracter. …”, „Control plus Shift plus litera D: pornește sau oprește modul de diagnostic.”, „Alt plus F4: închide HASI, după confirmare.”. Nimic nu se execută. (K-02…K-06, G-04) |
| 28 | Escape, altă tastă, apoi Escape de două ori la rând | „Escape: înapoi la ecranul anterior. Apăsați încă o dată Escape …”; după al doilea Escape la rând: „Meniu principal. Selectat: Învață tastele, 2 din 4.” (K-07) |
| 29 | Despre HASI; săgeata jos până la capăt | 9 rânduri; rândul 4: „Sinteza vocală RHVoice, de Olga Yakovleva și colaboratorii. …”; rândul 7: MULTEXT-East; rândul 9: System.Speech (D-01, M0-20) |

## F. Ieșire, fără să închizi nimic

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 30 | Escape; Ieșire, Enter; F1 | „Ieșire. 3 opțiuni. Selectat: Închide HASI, 1 din 3.”; ajutorul se termină cu „Fiecare alegere de aici vă cere confirmare: Enter pentru da, Escape pentru nu.” (E-01) |
| 31 | Repornește calculatorul, Enter; Escape | „Sigur doriți să reporniți calculatorul? Mod de test: calculatorul nu va reporni. Sunt deschise 2 programe: Windows Terminal, Notepad. Dacă aveți modificări nesalvate, se pot pierde. Apăsați Enter pentru da sau Escape pentru nu.” Numele trebuie să fie curate: „Notepad”, nu „Notepad.exe”. Apoi „Anulat. Ieșire. Selectat: Repornește calculatorul, 2 din 3.” (O-01, O-02) |
| 32 | Pe „Repornește calculatorul”, ții Enter apăsat câteva secunde; apoi Escape | doar întrebarea de confirmare; nu se închide nimic (E-07) |
| 33 | Alt + F4, Escape; apoi clic dreapta pe HASI în bara de activități → Închide fereastra, Escape | de fiecare dată „Sigur doriți să închideți HASI? …”, apoi „Anulat. …” (M0-22, E-06) |

## G. Focus și o singură instanță

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 34 | Dai clic pe Notepad (sau Alt + Tab), apoi Control + Alt + H | „HASI nu mai este în prim-plan, iar tastele merg către altă fereastră. Pentru a reveni, apăsați împreună Control, Alt și H.”; după Control + Alt + H, HASI vine în față și spune „Ieșire. Selectat: …” (M0-28) |
| 35 | Tasta Windows + săgeata jos (minimizezi HASI), apoi Control + Alt + H; apoi, într-un al doilea terminal, încă un `dotnet run --project src\Hasi.App` | HASI revine pe tot ecranul și spune ecranul curent. La a doua pornire nu apare a doua fereastră și nu se aude al doilea bun venit; HASI cel deschis vine în față și spune ecranul curent. (I-02, M0-29) |

## H. Jurnalul și ieșirea de test

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 36 | Escape; Setări → Diagnostic → Copiază jurnalul pe Desktop | „Jurnalul a fost copiat pe Desktop.”; pe Desktop apare `HASI-jurnal-<data>.txt`, cu rânduri `KEY` și `SAY` (M0-24) |
| 37 | Escape, Escape; Ieșire → Oprește calculatorul → Enter | întrebarea, cu „Mod de test: calculatorul nu se va opri.” și lista programelor; după Enter: „Mod de test: HASI se închide, iar calculatorul rămâne pornit. La revedere.”. HASI se închide, calculatorul nu. (O-03, O-04) |

## I. Pornire după eroare, Narator, setări păstrate

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| 38 | Pornești `dotnet run --project src\Hasi.App -- --diagnostic --test-error crash` și aștepți 10 secunde | bun venit; după 5 secunde „A apărut o problemă gravă. HASI se repornește.”; HASI pornește singur: bun venit, apoi „HASI a fost repornit după o problemă neașteptată. Ne cerem scuze.”. Nicio fereastră Windows de eroare. (G-07, I-05) |
| 39 | Ieșire → Închide HASI → Enter; pornești Naratorul (Control + tasta Windows + Enter); pornești HASI | după bun venit: „Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta Windows și Enter.”. Oprești Naratorul. (M0-27) |
| 40 | Setări → Voce, apoi Escape; Ieșire → Închide HASI → Enter | „Voce. … Selectat: Paul, RHVoice direct, actual, …” (vocea s-a păstrat); apoi „La revedere.” și HASI se închide (S-02, M0-21) |

## În afara sesiunii (o dată, înainte de 0.11)

Aceste scenarii cer repornirea Windows, alt hardware sau timp de așteptare:
- M0-30: pornirea automată, cu deconectare din cont și autentificare;
- O-06…O-10: repornirea și oprirea reale, inclusiv ecranul de blocare și evenimentul 27;
- R-05…R-07: Bluetooth, schimbarea dispozitivului, repausul;
- G-06, G-08, G-09: celelalte erori de test;
- V-03: o voce care se strică;
- S-11: un fișier de setări stricat.

„Gata” pentru Faza 0 cere aceeași listă și pe **Windows 10** (§4 din `architecture.md`).
