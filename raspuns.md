# HASI 0.2.0 este publicat

**Pagina versiunii:** https://github.com/AllxCucu/hasi/releases/tag/v0.2.0. E publică, nu e ciornă și nu e marcată „pre-release”. Notele versiunii, în română, apar pe pagină.

## Ce am făcut
- **Urcarea pe GitHub:** am urcat cele 34 de commituri de pe `main`. CI a trecut pe GitHub.
- **Publicarea automată:** un tag `v…` pornește acum `.github/workflows/release.yml`. Acesta construiește HASI, rulează toate testele, ia vocea RHVoice construită în CI, face pachetul și publică versiunea. Pentru tagul `v0.2.0`, toți pașii au trecut. RHVoice e exact construcția pe care ai testat-o: același SHA-256 (`F8D24E07…`), aceeași rulare CI.
- **README:** am trecut starea (0.2.0, cu legătura de descărcare), „Code signing policy” (SignPath, cerere în curs, cu rolurile tale) și „Privacy policy”.
- **Ghidul de instalare:** spune acum de unde se descarcă HASI.
- **Notele versiunii:** sunt în `docs/versiuni/0.2.0.md`.

## Lista exactă de fișiere publicate
| Fișier | Mărime | Pentru ce |
|---|---|---|
| `HASI.App-win-Setup.exe` | 87,2 MB | programul de instalare, singurul pe care îl descarcă un om |
| `HASI.App-0.2.0-full.nupkg` | 80,0 MB | pachetul complet, folosit la actualizări |
| `releases.win.json` | 6 KB | lista versiunilor, citită de HASI când caută actualizări |
| `RELEASES` | 78 B | aceeași listă, în formatul vechi |

## De știut
- **Dacă ai instalat pachetul de test 0.2.1,** HASI nu îți va oferi 0.2.0, pentru că e o versiune mai veche. Dacă vrei să încerci exact ce a fost publicat, dezinstalează testul și instalează din pagina versiunii. Atenție: dezinstalarea șterge și datele personale.
- **Versiunea e nesemnată:** SmartScreen avertizează la instalare, iar ghidul spune ce taste apeși.

## Ce urmează: cererea SignPath (`architecture.md` §5.1)
Partea ta:
1. verifici că ai autentificarea în doi pași pornită pe GitHub;
2. deschizi formularul de pe signpath.org/apply.

Formularul se încarcă dinamic, așa că îl completăm împreună: tu îmi spui câmpurile, eu îți dau textul pentru fiecare. Apoi trec la modulul 4, Calculatorul, dacă nu îmi spui altceva.
