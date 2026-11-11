param(
 [string]$Package = 'C:/Users/Admin/Downloads/com.dreamgames.royalkingdom_20584-20584_2arch_1feat_4d80b75aad360e3124b1abd39e0fd839_apkmirror.com.apkm'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=$PSScriptRoot
$raw=Join-Path $root 'Raw'
$apks=Join-Path $root 'Apks'
New-Item -ItemType Directory -Force -Path $raw,$apks | Out-Null
$packageHash=(Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant()
$inventory=[Collections.Generic.List[object]]::new()
$outer=[IO.Compression.ZipFile]::OpenRead($Package)
try {
 foreach($entry in $outer.Entries) {
  $inventory.Add([pscustomobject]@{container='APKM';path=$entry.FullName;length=$entry.Length;compressed=$entry.CompressedLength;sha256=$null})
 }
 foreach($name in @('base.apk','split_config.arm64_v8a.apk','split_UnityDataAssetPack.apk')) {
  $archiveFile=Join-Path $apks $name
  if(-not (Test-Path -LiteralPath $archiveFile)) {
   $entry=$outer.GetEntry($name)
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$archiveFile,$false)
  }
  $prefix=switch($name) {'base.apk' {'base'} 'split_config.arm64_v8a.apk' {'arm64'} default {'data'}}
  $destinationRoot=[IO.Path]::GetFullPath((Join-Path $raw $prefix))
  New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null
  $zip=[IO.Compression.ZipFile]::OpenRead($archiveFile)
  try {
   foreach($entry in $zip.Entries) {
    $row=[pscustomobject]@{container=$name;path=$entry.FullName;length=$entry.Length;compressed=$entry.CompressedLength;sha256=$null}
    if($entry.FullName.EndsWith('/')) { $inventory.Add($row); continue }
    # Retain the APK itself; materialize only useful authored payloads and native code.
    if($entry.FullName -match '^(assets/|lib/arm64-v8a/|classes[0-9]*\.dex$|AndroidManifest\.xml$)') {
     $destination=[IO.Path]::GetFullPath((Join-Path $destinationRoot $entry.FullName))
     if(-not $destination.StartsWith($destinationRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes output root' }
     New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
     if(-not (Test-Path -LiteralPath $destination)) { [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$false) }
     if((Get-Item -LiteralPath $destination).Length -ne $entry.Length) { throw ('Length mismatch: '+$entry.FullName) }
     $row.sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $inventory.Add($row)
   }
  } finally { $zip.Dispose() }
 }
} finally { $outer.Dispose() }
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'archive-inventory.json') -Encoding utf8
$apksInfo=Get-ChildItem -LiteralPath $apks -File | ForEach-Object { [pscustomobject]@{name=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} }
$status=[ordered]@{updated=(Get-Date -Format o);phase='package-extracted';package=$Package;packageSha256=$packageHash;inventoryRows=$inventory.Count;apks=$apksInfo;rawRoot=$raw;accountQuotaPercent=$null;quotaNote='Not observable through available tools';next='Inspect IL2CPP metadata; inspect Unity asset headers and tool availability'}
$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'STATUS.json') -Encoding utf8
$status | ConvertTo-Json -Depth 6
