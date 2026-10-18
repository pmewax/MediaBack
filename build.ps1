$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'source\MediaBack.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=None -o (Join-Path $root 'app') --configfile (Join-Path $root 'source\NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
& (Join-Path $root 'installer\BuildMsi.ps1') -Root $root
