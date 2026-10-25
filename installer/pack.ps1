<#
.SYNOPSIS
Builds the HASI installer with Velopack (phase 0.11, ADR 0007).

.DESCRIPTION
Publishes HASI self-contained for win-x64 (no .NET install, no administrator prompt), adds HASI's CI build of
RHVoice as rhvoice\ next to hasi.exe, and packs it: Setup.exe, the full package and, when an older package is
in the output folder, a delta package. The output folder is also an update source ("folder:<path>").

.PARAMETER Version
Version of this release, for example 0.1.0. It is also the version HASI says in "Despre HASI".

.PARAMETER RhVoice
Folder with HASI's CI build of RHVoice (artifact rhvoice-x64 of .github/workflows/rhvoice.yml). It must contain
BUILD-INFO.txt: only that build is shipped, never a copy installed by hand.

.PARAMETER Output
Where Setup.exe and the packages go. Default: artifacts\installer.

.PARAMETER ReleaseNotes
Optional path to a Markdown file with release notes, passed to vpk pack --releaseNotes when provided.

.EXAMPLE
pwsh installer\pack.ps1 -Version 0.1.0 -RhVoice $env:LOCALAPPDATA\HASI\rhvoice-ci-1.18.4
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $RhVoice,
    [string] $Output = 'artifacts\installer',
    [string] $ReleaseNotes
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $buildInfo = Join-Path $RhVoice 'BUILD-INFO.txt'
    if (-not (Test-Path $buildInfo)) { throw "No BUILD-INFO.txt in '$RhVoice': only HASI's CI build of RHVoice is shipped." }
    "RHVoice: $(Get-Content $buildInfo -TotalCount 1)"

    $publish = "artifacts\publish\$Version"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish src\Hasi.App -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    Copy-Item $RhVoice (Join-Path $publish 'rhvoice') -Recurse

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
    $releaseNotesArgs = @()
    if ($ReleaseNotes) { $releaseNotesArgs = @('--releaseNotes', $ReleaseNotes) }
    dotnet vpk pack --packId HASI.App --packTitle HASI --packAuthors 'HASI contributors' --packVersion $Version `
        --packDir $publish --mainExe hasi.exe --outputDir $Output --noPortable @releaseNotesArgs
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }
    Get-ChildItem $Output | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
}
finally {
    Pop-Location
}
