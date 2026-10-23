# HASI – Răspuns după corecturi și etapa 0.6

Toate cele **240 de teste automate** trec, local și pe GitHub:

| Proiect de test | Teste |
|---|---|
| nucleu | 154 |
| normalizator | 26 (fișiere cu peste 220 de cazuri) |
| motoare de voce | 29 |
| transcrieri și ecrane | 22 |
| arhitectură | 9 |

Mă opresc aici, cum ai cerut.

---

## 1. Cum pornești

RHVoice direct e acum **motorul implicit**, deci nu mai ai nevoie de opțiuni:

```
dotnet run --project src\Hasi.App -- --diagnostic
```

**Unde caută HASI fișierele RHVoice** (DLL-ul și vocea Paul), în ordine:
1. în folderul `rhvoice` de lângă `hasi.exe` (acolo le va pune installer-ul, în etapa 0.11);
2. în `%LOCALAPPDATA%\HASI\rhvoice`. Am mutat acolo copia ta de test, din `rhvoice-test`.

Dacă nu le găsește în niciunul, HASI folosește Paul prin SAPI, apoi OneCore.

**Opțiunile pentru teste rămân**, doar pentru sesiunea curentă:

| Opțiune | Efect |
|---|---|
| `--engine sapi5` / `onecore` / `rhvoice-dll` | alege motorul |
| `--rhvoice-path <folder>` | caută RHVoice în alt folder |
| `--voice "nume"` | alege vocea |
| `--rate`, `--pitch` | viteza și tonul (-10…10) |
| `--volume` | volumul (0…100) |
| `--audio-idle-seconds N` | după câte secunde de liniște se închide fluxul audio (implicit 60; 0 = niciodată) |
| `--test-voice-failure` | vocea principală „se strică” după trei mesaje |

O valoare dată în linia de comandă **nu se salvează**. Se salvează doar dacă o schimbi apoi din Setări.

---

## 2. Ce am făcut

### Deciziile tale

- **RHVoice.dll e motorul implicit**, cu SAPI și OneCore ca rezerve; ADR 0004 e acceptat. Compilarea DLL-ului în CI e programată înainte de etapa 0.11, cum ai cerut, nu acum.
- **E-mail:** n-am atins istoricul.

### Corecturile la normalizator

- **Numărul de după substantiv e o etichetă** și se citește în forma de bază: „pagina 2” devine „pagina doi”, „camera 1” „camera unu”, „linia 2” „linia doi”, „etajul 1” „etajul unu”, „sala 12” „sala doisprezece”.
  - Genul se aplică doar înaintea substantivului: „2 pagini” devine „două pagini”.
  - Recunosc substantivul din fața numărului după dicționar, după lista mea scrisă de mână și după o listă de substantive-etichetă (pagina, etajul, linia, sala, camera, capitolul, autobuzul și altele).
  - O singură excepție, ca să nu stric orele: „ora 2” rămâne „ora două”.
  - Am scos „Pagina 2 goală” din riscurile acceptate: acum se citește „Pagina doi goală”.
- **Pluralele necunoscute după un număr:**
  - cele în **-e** sau **-uri** primesc „două” („două smartfoane”);
  - cele în **-i** rămân la regula de până acum și apar ca `LEXICON?` în jurnal.
- Cazurile noi sunt în `Cases\09-etichete.cases` și `Cases\08-genuri-dictionar.cases`. ADR 0005 e actualizat.

### Redarea audio

- **Fluxul audio se închide după un minut fără vorbire.** Așa calculatorul poate intra în repaus și bateria se consumă mai puțin.
  - Durata se configurează în `settings.json` (`AudioIdleSeconds`) sau cu `--audio-idle-seconds`.
  - Fluxul nu se închide niciodată cât timp se vorbește.
  - La următorul mesaj se redeschide, cu 150 ms de liniște înainte.
- **Verificat pe calculatorul tău**, cu 3 secunde: ambele fluxuri (vocea principală și vocea din ecranul de comparare) s-au închis la 3 secunde după ultimul mesaj.
- **Neverificat de mine:**
  - `powercfg /requests` cere un terminal de administrator;
  - redeschiderea cere o tastă.

  Le găsești ca R-13 și R-14. Documentația e în ADR 0004.
- **Vocea se preîncarcă la pornire,** pe un fir separat, cât se construiește fereastra. Primul sunet al mesajului de bun venit a scăzut de la 280–400 ms la **16 ms**.

### Etapa 0.6 – Setări

Toate setările se aplică **imediat** și se salvează în `%APPDATA%\HASI\settings.json`. Dacă fișierul e deteriorat, HASI îl păstrează ca `settings.json.bad`, pornește cu setările inițiale și îți spune asta.

| Setare | Cum funcționează |
|---|---|
| **Voce** | Lista tuturor vocilor găsite („Paul, RHVoice direct”, „Paul, prin SAPI”, „Microsoft Andrei, prin SAPI” …). Lista se deschide pe vocea actuală, marcată „actual”. După alegere, confirmarea se aude deja cu vocea nouă. Dacă vocea aleasă nu pornește, HASI îți spune și rămâne la cea veche. |
| **Viteză, Ton** | Scara 0–100, unde 50 înseamnă valoarea normală a vocii; săgețile schimbă cu câte 5. Fiecare pas se aude deja la noua valoare. SAPI nu are reglaj de ton. |
| **Volum** | Scara 0–100, cu pași de 10; 100 este volumul maxim. |
| **Ecoul tastelor** | „Fiecare caracter”, „Fiecare cuvânt” sau „Fără ecou”. Pentru moment se aplică doar câmpului „Test pronunție”, singurul câmp de text de acum. |
| **Culori** | Patru combinații cu contrast mare. |
| **Mărimea textului** | Cinci mărimi, de la „Mic” la „Uriaș”. |
| **Capătul listei** | „Oprire la capăt, cu sunet” (implicit) sau „Salt la celălalt capăt”. |

**Ce am schimbat în texte:**
- textul de ajutor din Setări, pe care îl validaseși (M0-08), spune acum „Aici schimbați vocea… Fiecare schimbare se salvează imediat.”, în loc de „Aici veți putea schimba…”, pentru că setările există acum;
- în transcrierea 02, exemplul de setare încă neimplementată e acum „Pornire automată”, nu „Viteză”.

---

## 3. Scenariile (și în `docs/manual-tests/faza-0.md`)

### Corecturi (Test pronunție)

| ID | Ce scrii | Ce trebuie să auzi |
|---|---|---|
| C-01 | `pagina 2`, `camera 1`, `linia 2`, `etajul 1`, `sala 12` | „pagina doi”, „camera unu”, „linia doi”, „etajul unu”, „sala doisprezece” |
| C-02 | `2 pagini`, `Pagina 2 goală` | „două pagini”; „Pagina doi goală” |
| C-03 | `la ora 2`, `ora 1` | „la ora două”, „ora unu” |
| C-04 | `2 smartfoane`, `2 tramvaiele`, `2 meciurile` | „două …” la toate trei |
| R-12 (actualizat) | `2 autobuze`; `2 smartfoane`; `2 bitcoini` | „două autobuze”; „două smartfoane”; „doi bitcoini”, cu `LEXICON? bitcoini` în jurnal. Adaugi `bitcoini => f` în `src\Hasi.Text.Ro\Data\genuri.txt`, repornești și auzi „două bitcoini”. |

### Redarea audio

| ID | Ce faci | Ce urmărești |
|---|---|---|
| R-13 | Navighezi puțin. Într-un terminal **pornit ca administrator** rulezi `powercfg /requests`. Apoi aștepți peste un minut fără să apeși nimic și îl rulezi din nou. | prima dată apare o cerere audio, de obicei la `SYSTEM`. A doua oară cererea a dispărut, dacă nu redă altceva sunet. |
| R-14 | Cu căștile Bluetooth: 2 minute de liniște, apoi săgeata jos | prima silabă se aude întreagă |
| R-15 | `--audio-idle-seconds 0`, apoi `--audio-idle-seconds 10`; `powercfg /requests` după 20 de secunde de liniște | cu 0, cererea rămâne; cu 10, dispare |
| R-16 | Asculți mesajul de bun venit; apoi deschizi jurnalul | bun venit fără întârziere; în jurnal `LATENCY … preload=…` și un `first-sound` mic la bun venit |

### Setări (0.6)

| ID | Ce faci | Ce trebuie să auzi |
|---|---|---|
| S-01 | Setări → Voce; alegi „Paul, prin SAPI”, Enter | „Vocea a fost schimbată: Paul, prin SAPI.”, rostit deja cu SAPI |
| S-02 | Închizi și redeschizi HASI | aceeași voce aleasă; apoi revii la „Paul, RHVoice direct” |
| S-03 | Setări → Viteză; dreapta, dreapta, stânga; End; dreapta | „Viteză. 50, viteza normală. …”, „55”, „60”, „55”, „100”, [sunet] „Valoarea maximă. 100.” |
| S-04 | Setări → Volum; stânga de 2 ori | „Volum. 100, volumul maxim. …”, „90”, „80”, din ce în ce mai încet |
| S-05 | Setări → Ton; dreapta de 2 ori | „Ton. 50, tonul normal. …”, „55”, „60”, cu ton tot mai înalt |
| S-06 | Ecoul tastelor → „Fiecare cuvânt”; în Test pronunție scrii `bună ziua ` | „bună”, „ziua”; literele nu se mai aud |
| S-07 | Ecoul tastelor → „Fără ecou”; scrii ceva | liniște; textul apare pe ecran; Enter îl rostește |
| S-08 | Culori (toate patru) și Mărimea textului (toate cinci) | ecranul se schimbă imediat; auzi, de exemplu, „Culori: Negru pe galben.” |
| S-09 | Capătul listei → „Salt la celălalt capăt”; în meniul principal, săgeata jos pe „Ieșire” | [sunet] „Setări, 1 din 4.” |
| S-10 | Pornești cu `--rate 5`; schimbi doar volumul; închizi; pornești fără opțiuni | după repornire viteza e cea salvată înainte (nu 5), iar volumul e cel nou |
| S-11 | Scrii ceva fără sens în `%APPDATA%\HASI\settings.json`, apoi pornești | [sunet] „Setările salvate nu au putut fi citite. Folosesc setările inițiale. …”; apare `settings.json.bad` |

---

## 4. Ce nu e făcut sau nu e testat

| Ce | Detalii |
|---|---|
| Durata de închidere a fluxului audio în meniul Setări | Acum se schimbă doar din `settings.json` și din linia de comandă. Dacă o vrei și în meniu, îți propun o setare nouă, „Repausul sunetului”; spune-mi. |
| Voci englezești în lista de voci | Lista conține toate vocile găsite, inclusiv David, Zira și Mark (în engleză). Pot ascunde vocile care nu sunt românești; spune-mi dacă vrei. |
| Setările de vorbire pe ecranul „Comparare voci” | Viteza și volumul se aplică doar vocii principale; ecranul de comparare folosește vocile cu setările de la pornire. |
| Pornire automată, Cititor de ecran, Actualizări | Rămân pentru etapele 0.10, 0.9 și 0.11. |
| Compilarea RHVoice.dll în CI | Înainte de 0.11, cum ai cerut. |
| Redeschiderea fluxului, `powercfg`, Bluetooth, tastarea și setările în fereastra reală | Logica e acoperită de teste, dar n-am apăsat nicio tastă (regula). Am pornit HASI de cinci ori, câte 5–18 secunde, fără input. |
