# HASI – Human Assisted Information

Mediu de lucru vorbit, simplu, pentru nevăzători și slabvăzători din România.
HASI nu este un cititor de ecran. Este un „shell” cu meniuri liniare care vorbește direct
prin sintetizator: o singură operație odată, puține taste, feedback vocal permanent.

> Stare: 0.2.0 este prima versiune pentru utilizatori, cu ceas și calendar, editor de text și citire de documente.
> Descarcă `HASI.App-win-Setup.exe` de la [cea mai recentă versiune](https://github.com/AllxCucu/hasi/releases/latest).
> Dezvoltarea continuă în Faza 1.

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
`%LOCALAPPDATA%\HASI`, inclusiv `userdata`: ciornele și documentele recente ale editorului, documentele recente
din „Citire documente” cu pozițiile de citire, iar din modulul 5 agenda), pe lângă program și pornirea automată.
HASI nu are încă o copie de siguranță pentru datele personale. Documentele pe care le-ai salvat în Documente
rămân; dezinstalarea nu le atinge.

## Confidențialitate

HASI nu colectează și nu trimite date despre utilizator. Datele stau local, iar parolele
în Windows Credential Manager. Singura legătură la internet este verificarea actualizărilor: o cerere către
pagina de versiuni a HASI pe GitHub, la pornire (se poate opri din Setări → Actualizări). Vezi
[docs/data-sources.md](docs/data-sources.md).

## Code signing policy

Releases are currently unsigned. HASI plans to apply for free code signing by SignPath Foundation before its pilot
with real users; no application has been made yet.

If accepted, the following attribution will apply: "Free code signing provided by
[SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/)".

- Committers and reviewers: AllxCucu.
- Approvers: AllxCucu.

Only binaries built from this repository by GitHub Actions ([release.yml](.github/workflows/release.yml)) will
be signed. RHVoice is HASI's own CI build from upstream source ([rhvoice.yml](.github/workflows/rhvoice.yml)).
Each signing request will require manual approval.

## Privacy policy

HASI does not transfer information to other networked systems except for the update check: one request to this
repository's releases page at startup, with no personal data. You can turn it off in Settings → Updates.
User data stays on your computer.

## Licență

GPL-3.0-or-later. Vezi [LICENSE](LICENSE) și [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
