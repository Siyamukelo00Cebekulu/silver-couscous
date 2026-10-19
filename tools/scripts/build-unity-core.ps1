$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$project = Join-Path $repositoryRoot "src\GrandPrix.Domain\GrandPrix.Domain.csproj"
$destination = Join-Path $repositoryRoot "UnityProject\Assets\Plugins\GrandPrixCore"

dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Building GrandPrix.Core failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item (Join-Path $repositoryRoot "src\GrandPrix.Core\bin\Release\netstandard2.1\GrandPrix.Core.dll") $destination
Copy-Item (Join-Path $repositoryRoot "src\GrandPrix.Domain\bin\Release\netstandard2.1\GrandPrix.Domain.dll") $destination
