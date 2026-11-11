$ErrorActionPreference='Stop'
$python='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if(-not (Test-Path -LiteralPath $python)) { throw 'Bundled Python not found. Locate workspace dependencies before continuing; do not reinstall or rerun extraction automatically.' }
& $python (Join-Path $PSScriptRoot 'verify-recovery.py')
if($LASTEXITCODE -ne 0) { throw 'Recovery verification failed; inspect the reported artifact before resuming.' }
Write-Output 'Recovery checkpoint verified. Read the latest PHASE_*_REPORT.txt and STATUS.json for the next unfinished step.'
