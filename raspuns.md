# Semnarea se mută în faza 4

Am notat că instalarea 0.2.0 a mers fără probleme, inclusiv pasul cu SmartScreen. Semnarea (cererea SignPath și integrarea ei în publicare) se mută în faza 4, înainte ca HASI să ajungă la utilizatorii din pilot.

**Ce am schimbat:**
- `docs/architecture.md`: §7 (punctul fix despre semnare), §5.1, riscul R7 și decizia 7;
- `docs/plan-faza-1.md` și `docs/plan-0.11.md`: o notă despre mutare;
- `README.md`: „Code signing policy” spune acum că versiunile sunt nesemnate și că cererea SignPath nu a fost încă făcută;
- `release.yml`: comentariul de la început.

Până la faza 4, versiunile rămân nesemnate. Ghidul de instalare păstrează pașii pentru SmartScreen.

**Următorul pas:** modulul 4 (Calculator). Îl încep când îmi spui.
