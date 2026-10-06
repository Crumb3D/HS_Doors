# HS_Doors — rebuild FBX from Blender, then the Unity asset bundle.
# Requires Unity 2022.3.62f2 (the game's version). Do not use Unity 6.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BlenderScript = Join-Path $Root "_blender\hsdoors_models.py"
$Models = Join-Path $Root "_unity\Assets\HSDoors\Models"
$UnityProj = Join-Path $Root "_unity"
$UnityLog = Join-Path $Root "_unity_build.log"
$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"

function Find-OfficialBlender {
    $candidates = @()
    $pf = "C:\Program Files\Blender Foundation"
    if (Test-Path $pf) {
        $candidates += Get-ChildItem $pf -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $local = Join-Path $env:LOCALAPPDATA "Programs\Blender"
    if (Test-Path $local) {
        $candidates += Get-ChildItem $local -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and $cmd.Source -notmatch '\\WindowsApps\\') {
        $candidates += $cmd.Source
    }
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }
    return $null
}

$blender = Find-OfficialBlender
if (-not $blender) { throw "Official blender.exe not found. Install from blender.org (not the Microsoft Store)." }
if (-not (Test-Path $UnityExe)) { throw "Unity 2022.3.62f2 not found at $UnityExe" }

Write-Host "Blender: $blender"
& $blender --background --factory-startup --python $BlenderScript -- $Models
if ($LASTEXITCODE -ne 0) { throw "Blender export failed ($LASTEXITCODE)" }

Write-Host "Unity: $UnityExe"
& $UnityExe -batchmode -nographics -projectPath $UnityProj -executeMethod HSDoorsBuild.Build -quit -logFile $UnityLog
if ($LASTEXITCODE -ne 0) { throw "Unity bundle build failed ($LASTEXITCODE). See $UnityLog" }
Write-Host "Done. Bundle: $(Join-Path $Root 'Resources\HSDoors.unity3d')"
