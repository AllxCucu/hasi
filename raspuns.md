# HASI – Planul 0.11 (propunere)

Planul e în **`docs/plan-0.11.md`**. E o propunere: nu am scris cod și nu am atins nimic din program. Aștept rezultatele regresiei înainte de orice aprobare.

## Pe scurt

1. **RHVoice.dll construit în CI (primul pas).**
   - Aducem RHVoice la commitul exact al versiunii 1.18.4 și construim cu SCons, cum face și CI-ul lor de Windows.
   - Inițializăm doar submodulele necesare: depozitul are peste 100 (toate vocile), iar noi avem nevoie de Paul și de datele românești, care sunt și ele submodule acolo.
   - Testăm automat că DLL-ul construit vorbește.
   - Apoi asculți tu: acum folosești 1.18.2, iar noi construim 1.18.4. **Oprire.**
   - Dacă SCons nu poate construi doar biblioteca x64, mă opresc și îți dau variantele, cum ai cerut.
2. **Pachetul Velopack.**
   - Id `HASI.App`, instalare per-utilizator, fără administrator.
   - .NET inclus în pachet, deci fără nicio fereastră UAC.
   - La dezinstalare se șterg pornirea automată, `%LOCALAPPDATA%\HASI` și `%APPDATA%\HASI`.
   - Pornirea automată indică `hasi.exe` de pornire, cu cale stabilă la actualizări.
   - „Simulare oprire” devine oprită implicit pentru utilizatorii noi. La tine rămâne pornită: am verificat, în `settings.json` ai `"SimulatePower": true`.
3. **Actualizări din GitHub Releases, cu anunț rostit.**
   - La pornire, după bun venit, o confirmare: Enter actualizează, Escape amână.
   - Progresul se spune, iar după repornire auzi „HASI a fost actualizat la versiunea …”.
   - Fără internet: tăcere la verificarea automată, iar la „Caută acum” mesajul „Nu există conexiune la internet.”
   - Poți testa și dintr-un folder local, fără să publicăm nimic. **Oprire.**
4. **CI și prima versiune.**
   - La fiecare push, installer-ul rămâne artifact CI, nepublicat.
   - Versiunea 0.1.0 se publică doar cu aprobarea ta explicită, nesemnată.
   - README: politica de semnare, rolurile tale și politica de confidențialitate.
   - Apoi completăm împreună formularul SignPath.

Am trecut în documentație și deciziile tale: P-13 e decisă definitiv („unu din patru”), iar celelalte șapte sunt în tabelul de la începutul planului.

## Ce trebuie să-mi spui

1. **Windows 10:** definiția lui „Gata” cere și Windows 10, iar tu nu ai. Variantele:
   - a) doar Windows 11, o schimbare de plan pe care o decizi tu;
   - b) caut o variantă legală și gratuită pentru o mașină virtuală;
   - c) amânăm testul pe Windows 10.
2. **Pornirea automată după instalare:** pornită implicit sau oprită?
3. **Opririle din 0.11:** propun trei: după pasul 1, după pașii 2–3 și înainte de publicarea 0.1.0. Ți se pare bine?

Aștept rezultatele regresiei și răspunsurile de mai sus.
