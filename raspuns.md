# HASI – Testul actualizării pentru 0.2.0

Am notat rezultatele tale pentru modulul 3 și deciziile 1–5 în `docs/plan-faza-1.md`. Testele C3-39 și C3-40 au trecut, deci tasta 5 ajunge la HASI și cu NVDA pornit. N-a fost nevoie de o tastă nouă; de altfel, Enter pornește deja „Citește tot” de pe tastatura obișnuită. Nu am publicat nimic.

## Ce testezi acum
**Scenariul:** `docs/manual-tests/actualizare-0.2.md`, pașii U-01…U-09:
1. instalezi 0.2.0;
2. creezi un document salvat (`Test actualizare`), o ciornă (`Ciornă de test`) și o poziție de citire;
3. actualizezi la 0.2.1 dintr-un folder local;
4. verifici că toate au rămas.

**Pachetele de test** (doar pe calculatorul tău):
- programul de instalare: `C:\Users\cucua\Documents\dev\workspace\Hasi\artifacts\HASI-0.2.0-Setup.exe`;
- sursa actualizării: `C:\Users\cucua\Documents\dev\workspace\Hasi\artifacts\feed-0.2`.

Am verificat că în pachet sunt licențele, RHVoice și versiunea corectă.

**Comanda pentru actualizare (U-05):** după ce ai închis HASI, Windows + R, apoi:
`%LOCALAPPDATA%\HASI.App\hasi.exe --update-source "folder:C:\Users\cucua\Documents\dev\workspace\Hasi\artifacts\feed-0.2"`

**De știut înainte:**
- HASI instalat și HASI pornit din build folosesc același folder de date (`%LOCALAPPDATA%\HASI\userdata`). Pot apărea ciorne din testele vechi; la lista de ciorne apeși Escape, ca să le păstrezi.
- Nu dezinstala la final dacă vrei să păstrezi ciornele vechi: dezinstalarea șterge tot folderul.

## Ce am schimbat în documente
- **Ghidul și README:** spun acum că HASI încă nu are copie de siguranță și că documentele salvate rămân în Documente. Am adăugat și pozițiile de citire în lista datelor personale care se șterg la dezinstalare.
- **`docs/architecture.md` §7, planul mare:** fazele sunt acum în ordinea ta: 1 Uzul zilnic, 2 Comunicare și informare, 3 Servicii românești, 4 Pilot cu utilizatori reali, 5 Human Assisted, 6 Extinderi. Tot acolo scrie că cererea SignPath se face imediat după 0.2.0 și că testul pe Windows 10 se face înainte de pilot.

## Ce urmează
După ce trece testul actualizării, pregătesc publicarea 0.2.0 ca la pasul 4 din `plan-0.11.md`: README, notele versiunii în română și lista exactă a fișierelor. Apoi mă opresc. Public doar după ce îmi scrii „publică 0.2.0”.

## Ce nu e făcut
- Actualizarea e verificată doar de mine, prin construirea pachetelor. Încă nu a fost instalată.
- Codex a scris documentele de mai sus, iar eu am verificat textele rostite față de `ro.json`. Sandboxul lui implicit, cel elevat, nu pornește pe calculatorul tău („setup refresh had errors”). L-am rulat cu sandboxul neelevat, doar pentru această rulare, fără să-i schimb configurarea.
