# HASI – Răspuns după repornire și oprire, etapa 0.8 și etapa 0.10

Am implementat și am pus pe GitHub soluția pentru repornire și oprire, etapa 0.8 și etapa 0.10. Toate cele **295 de teste automate** trec, iar CI-ul este verde:

| Proiect de test | Teste |
|---|---|
| nucleu | 175 |
| transcrieri și ecrane | 55 |
| motoare de voce | 29 |
| normalizator | 27 |
| arhitectură | 9 |

Nu am pornit deloc HASI: nimic din aceste trei părți nu a fost ascultat încă, asta rămâne pentru dumneavoastră.

**Pornire:** `dotnet run --project src\Hasi.App -- --diagnostic`.

---

## 1. Ce s-a făcut

### Repornire și oprire (`4bfc96a`)

- Înainte de confirmare, HASI numește programele deschise (doar numele, nu titlul ferestrei) și avertizează că modificările nesalvate se pot pierde.
- După confirmare HASI rămâne deschis și vă spune ce se întâmplă: dacă Windows refuză, dacă repornirea nu începe în 10 secunde, dacă un program o blochează (mesajul se repetă la 30 de secunde) sau dacă a fost anulată.
- Repornirea forțată nu se face niciodată implicit. Apare doar ca a doua confirmare, după o încercare blocată, și numai dacă programele sunt încă deschise. Oprirea este completă, fără Pornirea rapidă.
- Setarea „Simulare oprire” (Setări → Diagnostic) este pornită implicit, exact cum ați cerut. Există și `--real-power`, care face repornirea și oprirea reale doar pentru sesiunea curentă. În planul pentru 0.11 am notat că valoarea implicită devine „oprită”. Decizia e descrisă în ADR 0006.

### Etapa 0.8 (`7c46aad`)

- Modul de diagnostic se pornește și se oprește din meniu sau cu Control+Shift+D și Control+Shift+F12, oriunde în HASI.
- „Copiază jurnalul pe Desktop” pune sesiunea și sfârșitul jurnalului de erori într-un singur fișier text.
- Jurnalul de erori este permanent. Când trece de 1 MB, fișierul vechi e păstrat separat și începe unul nou.
- La o eroare gravă HASI se repornește singur și vă spune ce s-a întâmplat. Eroare gravă înseamnă: o prăbușire, trei erori în 30 de secunde sau o fereastră care nu mai răspunde 20 de secunde. După 3 reporniri automate în 10 minute, HASI se oprește și vă spune de ce. Aceste praguri le-am ales eu și se pot schimba ușor.

### Etapa 0.10 (`e9bdfd6`)

- HASI rulează într-o singură instanță.
- Control+Alt+H aduce HASI în față din orice program.
- Pornirea automată se alege din Setări.
- Dacă Naratorul e pornit, HASI o spune la pornire, cu mesajul exact pe care l-ați cerut.

---

## 2. Scenarii de testat

Lista completă, cu textele exacte, este în `docs/manual-tests/faza-0.md`, la secțiunile O, G și I.

| Ce apăsați | Ce trebuie să auziți |
|---|---|
| Ieșire → Repornește, cu Notepad deschis | „… Mod de test: calculatorul nu va reporni. Este deschis un program: Notepad. Dacă aveți modificări nesalvate, se pot pierde. …” |
| Setări → Diagnostic → Simulare oprire → Oprită, apoi porniți HASI din nou | o singură dată: „Atenție: repornirea și oprirea calculatorului sunt reale.” |
| (real, cu Notepad nesalvat) Repornește → Enter, așteptați | „Repornirea așteaptă. Probabil un program are modificări nesalvate …”; după „Anulare” pe ecranul Windows: „Repornirea a fost anulată. …” |
| Control+Shift+D | „Modul de diagnostic este pornit. …”, iar a doua oară „… este oprit.” |
| Copiază jurnalul pe Desktop | „Jurnalul a fost copiat pe Desktop.” |
| Porniți cu `-- --test-error ui` (apoi `crash` și `hang`) | „A apărut o problemă gravă. HASI se repornește.”, apoi „HASI a fost repornit după o problemă neașteptată. Ne cerem scuze.” |
| Treceți în Notepad, apoi Control+Alt+H | „… Pentru a reveni, apăsați împreună Control, Alt și H.”, apoi ecranul curent |
| Porniți HASI a doua oară | nu se deschide a doua fereastră; HASI cel deschis vine în față |
| Porniți cu Naratorul activ | „Naratorul Windows este pornit și va vorbi peste HASI. …” |

---

## 3. Ce trebuie să-mi spuneți după test

1. Când apare ecranul Windows de blocare (O-07 și O-08):
   - se aude vocea HASI peste el?
   - ce taste funcționează pe ecran?
   - ce fereastră rămâne în față după „Anulare”?

   După răspunsuri fixez textul final.
2. Ce arată evenimentul 27 după o oprire din HASI (O-10): 0x0 înseamnă oprire completă, 0x1 înseamnă Pornire rapidă.
3. Cât a durat tăcerea la `--test-error hang` (G-08).
4. **Rezultatele pentru 0.7**: câmpul din mesajul trecut a rămas necompletat.

Am observat și o greșeală de-a mea: în documentele mai vechi am scris „24 octombrie”, dar acele modificări sunt din 23 octombrie. Le corectez dacă vreți.

Mă opresc aici și aștept raportul dumneavoastră.
