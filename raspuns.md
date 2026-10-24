# HASI – Faza 1, modulul 3: Citire documente

Modulul 3 e gata de testat. Build-ul trece fără avertismente, toate cele 1051 de teste trec, iar rularea de verificare a trecut: 7 opțiuni în meniul principal, vocea RHVoice, nicio eroare. Nu am publicat nimic.

**Pornire:** `dotnet run --project src\Hasi.App -- --diagnostic`

**Scenarii:** `docs/manual-tests/faza-1-modul-3.md`, de la C3-01 la C3-49. Secțiunea J îți spune ce retestezi din modulul 2. Regresia e în `docs/manual-tests/regresie.md`, pașii 1–55; pașii 51–55 sunt noi.

## Recenziile Codex pe nucleu
- **Modulul 2:** 5 probleme reale, toate corectate.
  - Trei te privesc direct: tastele apăsate după „La revedere” se pierdeau; cuvinte dintr-un document ajungeau în jurnal; o copie de siguranță întreruptă trecea drept completă.
  - **De retestat:** C3-49 (tastele ignorate după „La revedere”) și C3-38 (jurnalul).
- **Modulul 3:** 7 probleme reale, toate corectate. Cele mai importante: „Citește tot” se putea opri sau putea sări text, iar RHVoice anunța sfârșitul unei propoziții înainte să se fi auzit.
  - **De retestat:** secțiunea G, „Citește tot” cu fiecare voce. Ascultă dacă se taie ultimul cuvânt sau dacă apar pauze lungi între propoziții.

## Ciornele (decizia ta 3)
Acum HASI păstrează câte o ciornă pentru fiecare document nesalvat, cel mult 10. La pornire le oferă într-o listă. Când se ajunge la limită, HASI spune ce ciornă se șterge înainte să o șteargă. Scenariile sunt C3-44…C3-48.

## Decizii de confirmat
1. **O tastă apăsată în timpul citirii** doar oprește citirea și nu spune nimic. Escape și F10 opresc citirea și își fac apoi treaba obișnuită.
2. **Tasta 5 de pe tastatura numerică** pornește „Citește tot” în cititor.
3. **Un PDF scanat în întregime** nu se deschide. HASI spune că documentul conține doar imagini.
4. **La „Citește tot”**, titlurile se citesc ca propoziții obișnuite, fără „Titlu 2:”.
5. **Poziția de citire** se salvează la 5 secunde. Dacă închizi cu Alt+F4 în timp ce citești, se pot pierde ultimele câteva secunde.

## Lista pentru publicarea 0.2.0 (plan §6)

| Condiție | Stare |
|---|---|
| Salvarea, ciorna și recuperarea după o eroare gravă | Pentru o singură ciornă, **verificată de tine** (modulul 2). Ciornele multiple sunt noi: **de verificat de tine** (C3-44…48). |
| Jurnalul de diagnostic nu conține textul scris | **Verificată de mine** prin teste, plus cele două recenzii Codex; pentru editor, verificată și de tine (C2-36). Pentru documentele citite: **de verificat de tine** (C3-38). |
| Tastele noi merg cu NVDA | Editorul: **verificată de tine** (C2-38). Cititorul: **de verificat de tine** (C3-39, C3-40). |
| O actualizare instalată păstrează documentele, ciornele și pozițiile de citire | **Nefăcută.** Datele stau în `%LOCALAPPDATA%\HASI\userdata`, iar actualizarea nu atinge acel folder, dar trebuie încercat. Îți pot pregăti două pachete de test într-un folder local, ca la X-05; instalarea o faci tu. |
| Ghidul și README spun ce se șterge la dezinstalare și cum se face copia de siguranță | Partea cu ștergerea: **verificată de mine**. Copia de siguranță: **nefăcută**, pentru că vine cu agenda (modulul 5). Pentru 0.2.0 propun ca ghidul să spună că încă nu există copie de siguranță, iar documentele salvate rămân în Documente. |
| (Riscul F9) Licențele sunt în pachet | **Verificată de mine.** Am găsit o lipsă: licența HASI, textul MIT și `THIRD-PARTY-NOTICES.md` nu intrau în pachet. Am adăugat-o în folderul `licente\` și am verificat cu o publicare de test. |

## Ce nu e făcut
- **Nu am publicat nimic.** Pachetele de test pentru verificarea actualizării le pregătesc doar dacă îmi spui.
- **„Citește tot” e verificat doar prin teste**, care simulează vocea. Cât de bine se aude (pauze, sfârșitul propozițiilor) se poate afla doar ascultând.
