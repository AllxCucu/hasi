# HASI – Human Assisted Information

Mediu de lucru vorbit, simplu, pentru nevăzători și slabvăzători din România.
HASI nu este un cititor de ecran. Este un „shell” cu meniuri liniare care vorbește direct
prin sintetizator: o singură operație odată, puține taste, feedback vocal permanent.

> Stare: în dezvoltare (Faza 0). Încă nu există o versiune pentru utilizatori.

## Pentru dezvoltatori

Cerințe: Windows 10 sau 11, [.NET SDK 10.0.402+](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build
dotnet test
dotnet run --project src/Hasi.App
```

Documentație: [arhitectură](docs/architecture.md), [decizii (ADR)](docs/adr/),
[teste manuale](docs/manual-tests/). Convențiile de dezvoltare sunt în [CLAUDE.md](CLAUDE.md).

## Instalare

Ghidul, cu pașii pentru cititoarele de ecran: [docs/ghid-instalare.md](docs/ghid-instalare.md). Instalarea e
per-utilizator, fără drepturi de administrator. Windows 11: testat. Windows 10: suportat, netestat.

**Dezinstalarea șterge toate setările, jurnalele și datele personale HASI** (`%APPDATA%\HASI` și
`%LOCALAPPDATA%\HASI`, inclusiv `userdata`: ciornele și documentele recente ale editorului, iar din modulul 5
agenda), pe lângă program și pornirea automată. Documentele salvate în Documente rămân. Când va exista agenda,
fă o copie de rezervă înainte de dezinstalare.

## Confidențialitate

HASI nu colectează și nu trimite date despre utilizator. Datele stau local, iar parolele
în Windows Credential Manager. Singura legătură la internet este verificarea actualizărilor: o cerere către
pagina de versiuni a HASI pe GitHub, la pornire (se poate opri din Setări → Actualizări). Vezi
[docs/data-sources.md](docs/data-sources.md).

## Licență

GPL-3.0-or-later. Vezi [LICENSE](LICENSE) și [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
