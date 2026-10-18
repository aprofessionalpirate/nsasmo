param(
    [string]$GameDirectory = 'D:\Scratch\SPIDERMAN',
    [string]$DosBox = "${env:ProgramFiles(x86)}\DOSBox-0.74-3\DOSBox.exe"
)

$ErrorActionPreference = 'Stop'
$gamePath = (Resolve-Path -LiteralPath $GameDirectory).Path
$dosBoxPath = (Resolve-Path -LiteralPath $DosBox).Path
if (-not (Test-Path -LiteralPath (Join-Path $gamePath 'SPIDEY.EXE') -PathType Leaf))
{
    throw "SPIDEY.EXE was not found in $gamePath."
}

# Use a private configuration so user settings cannot enable a cached dynamic core.
# VGA is selected in the game's own menu; SVGA hardware supports its mode 0Dh.
$configuration = @"
[sdl]
fullscreen=false
output=surface
[dosbox]
machine=svga_s3
[cpu]
core=normal
cycles=3000
[autoexec]
mount c "$gamePath"
c:
SPIDEY.EXE
exit
"@

$configurationPath = Join-Path ([System.IO.Path]::GetTempPath()) "spideynet-$([Guid]::NewGuid().ToString('N')).conf"
try
{
    [System.IO.File]::WriteAllText($configurationPath, ($configuration -replace "`r?`n", "`r`n"))
    Write-Host 'Choose 1: VGA, select English, then choose 1: Play New Game.'
    Start-Process -FilePath $dosBoxPath -ArgumentList '-noconsole', '-conf', "`"$configurationPath`"" -WorkingDirectory $gamePath -WindowStyle Normal -Wait
}
finally
{
    Remove-Item -LiteralPath $configurationPath -ErrorAction SilentlyContinue
}
