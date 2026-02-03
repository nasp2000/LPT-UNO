param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$project = Join-Path .. LPTUnoApp\LPTUnoApp.csproj
$publishDir = Join-Path .. LPTUnoApp publish

Write-Host "Publishing project to $publishDir..."
$pub = dotnet publish $project -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed"; exit 1 }

# Check for Inno Setup Compiler (ISCC)
$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    Write-Warning "Inno Setup Compiler (ISCC.exe) not found. Install Inno Setup (https://jrsoftware.org/) and ensure ISCC.exe is in PATH. Published files are in: $publishDir";
    exit 0
}

$scriptPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) 'LPT-UNO-Installer.iss'
Write-Host "Building installer with ISCC: $scriptPath"
& iscc.exe $scriptPath
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed"; exit 1 }

Write-Host "Installer build complete. Check the 'installer\output' folder for the generated installer."