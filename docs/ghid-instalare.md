# Ghid de instalare HASI

Pentru cine instalează HASI pentru sine sau pentru altcineva. Toți pașii merg doar cu tastatura.

## Ce primești

Descarcă `HASI.App-win-Setup.exe` din lista „Assets” de pe [pagina celei mai recente versiuni](https://github.com/AllxCucu/hasi/releases/latest).

Un singur fișier: `HASI.App-win-Setup.exe` (programul de instalare). Nu are nevoie de drepturi de administrator și nu instalează nimic altceva.
Vocea românească Paul și tot ce trebuie pentru ea sunt incluse.

## Instalarea

1. Deschizi fișierul `HASI.App-win-Setup.exe` (Enter pe el, în Explorer).
2. Cât timp HASI nu este semnat digital, Windows poate afișa ecranul albastru
   „Windows a protejat computerul” (SmartScreen). Cu un cititor de ecran (de exemplu NVDA):
   - apeși Tab până la legătura „Mai multe informații” și apeși Enter;
   - apeși Tab până la butonul „Executare oricum” și apeși Enter.
3. Instalarea durează câteva secunde și arată doar o bară de progres, fără voce.
4. La final, HASI pornește singur și spune „Bun venit în HASI”. La această primă pornire mai spune o singură dată:
   „HASI va porni automat când porniți calculatorul. Puteți schimba asta din Setări, Pornire automată.”

HASI se instalează în `%LOCALAPPDATA%\HASI.App`. Pe Desktop și în meniul Start apare o scurtătură „HASI”.

## Pornire automată

După instalare, HASI pornește singur de fiecare dată când intri în Windows. Se oprește din
Setări → Pornire automată → Nu. Dacă HASI ajunge în spatele altei ferestre, apeși împreună Control, Alt și H.

## Actualizări

La pornire, HASI verifică dacă există o versiune nouă (o singură cerere către pagina de versiuni a HASI, pe GitHub;
nu se trimit date despre tine). Dacă există, te întreabă o singură dată pentru acea versiune:
Enter actualizează, Escape amână. Poți actualiza oricând din Setări → Actualizări → Caută actualizări acum.
Verificarea automată se oprește din Setări → Actualizări → Verificare automată la pornire → Nu.

## Dezinstalarea

**Atenție: dezinstalarea șterge toate setările, jurnalele și datele personale din HASI.** Se șterg:
- programul (`%LOCALAPPDATA%\HASI.App`) și scurtăturile;
- pornirea automată;
- setările (`%APPDATA%\HASI`): vocea, viteza, culorile și toate celelalte;
- jurnalele de diagnostic și de erori (`%LOCALAPPDATA%\HASI`);
- datele personale (`%LOCALAPPDATA%\HASI\userdata`): ciornele și documentele recente ale editorului, documentele
  recente din „Citire documente” cu pozițiile de citire (și, din modulul 5, agenda).

**Documentele pe care le-ai salvat în Documente rămân**; dezinstalarea nu le atinge.
HASI nu are încă o copie de siguranță pentru datele personale; aceasta vine cu agenda (modulul 5).

Dacă vrei să păstrezi jurnalele (de exemplu ca să le trimiți cuiva), folosește înainte
Setări → Diagnostic → Copiază jurnalul pe Desktop.

Dezinstalarea: Setări Windows → Aplicații → Aplicații instalate → HASI → Dezinstalare.

## Sisteme suportate

- Windows 11: testat.
- Windows 10: suportat, **netestat** până acum.
