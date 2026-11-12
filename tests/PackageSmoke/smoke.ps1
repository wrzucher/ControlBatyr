$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$run = Join-Path $repo "artifacts/package-smoke/$([Guid]::NewGuid().ToString('N'))"
$feed = Join-Path $run 'feed'
$consumer = Join-Path $run 'consumer with spaces'
$cache = Join-Path $run 'packages'
$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $args failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
Invoke-Dotnet pack (Join-Path $repo 'ControlBatyr.slnx') -c Release -o $feed
$packages = @(Get-ChildItem $feed -Filter '*.nupkg')
if ($packages.Count -ne 3) { throw "Expected three packages, found $($packages.Count)." }
foreach ($id in 'ControlBatyr', 'ControlBatyr.Requirements', 'ControlBatyr.Inspection') {
    $package = Join-Path $feed "$id.$version.nupkg"
    if (!(Test-Path $package)) { throw "Missing $id $version" }
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $reader = [IO.StreamReader]::new($archive.GetEntry("$id.nuspec").Open())
        try { $metadata = ([xml]$reader.ReadToEnd()).package.metadata } finally { $reader.Dispose() }
        if ($metadata.id -ne $id -or $metadata.version -ne $version -or
            !$metadata.description -or $metadata.license.type -ne 'expression' -or
            $metadata.license.InnerText -ne 'MIT' -or !$metadata.repository.url -or
            $metadata.readme -ne 'README.md' -or !$archive.GetEntry('README.md')) {
            throw "Missing or incorrect package metadata in $id."
        }
        if ($id -eq 'ControlBatyr.Inspection' -and
            ($metadata.developmentDependency -ne 'true' -or
             !$archive.GetEntry('build/ControlBatyr.Inspection.targets') -or
             !$archive.GetEntry('tools/net10.0/any/ControlBatyr.Inspection.dll') -or
             !$archive.GetEntry('tools/net10.0/any/ControlBatyr.Inspection.runtimeconfig.json') -or
             @($archive.Entries | Where-Object FullName -Like 'lib/*').Count -ne 0)) {
            throw 'Inspection must contain build integration and a tool, with no runtime library.'
        }
    } finally { $archive.Dispose() }
}

Copy-Item (Join-Path $PSScriptRoot 'Consumer.csproj'), (Join-Path $PSScriptRoot 'Consumer.cs') $consumer
# Isolate the consumer from repository build defaults and configured package sources.
Set-Content (Join-Path $consumer 'Directory.Build.props') '<Project />'
Set-Content (Join-Path $consumer 'Directory.Build.targets') '<Project />'
$escapedFeed = [System.Security.SecurityElement]::Escape($feed)
Set-Content (Join-Path $consumer 'NuGet.Config') @"
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="ControlBatyr*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="Microsoft.*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
$project = Join-Path $consumer 'Consumer.csproj'
Invoke-Dotnet restore $project --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache "-p:ControlBatyrPackageVersion=$version"
Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version"
$document = Join-Path $consumer 'docs/ticket.md'
if (!(Test-Path (Join-Path $consumer 'docs/index.md')) -or !(Test-Path $document)) {
    throw 'The package build did not generate business API documents.'
}
$markdown = Get-Content $document -Raw
foreach ($text in 'Issues a ticket with the requested identifier.', 'Ticket', 'Issued ticket keeps its identifier', '[Source]') {
    if (!$markdown.Contains($text)) { throw "Generated document is missing: $text" }
}
$output = Join-Path $consumer 'bin/Debug/net10.0'
Invoke-Dotnet (Join-Path $output 'Consumer.dll')
if ((Test-Path (Join-Path $output 'ControlBatyr.Inspection.dll')) -or
    (Get-Content (Join-Path $output 'Consumer.deps.json') -Raw).Contains('ControlBatyr.Inspection')) {
    throw 'Inspection must not be a consumer runtime dependency.'
}

Add-Content (Join-Path $consumer 'Consumer.cs') @'

[ObjectModel]
public sealed class BrokenModel
{
    public int Value { get; set; }
}
'@
$failure = & dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version" 2>&1
$failureCode = $LASTEXITCODE
if ($failureCode -eq 0 -or ($failure -join "`n") -notmatch 'CB0006') {
    $failure | Write-Output
    throw 'The packaged rule must reject a mutable model with CB0006.'
}
Write-Output 'PASS: packaged rule rejected a mutable model (CB0006).'
Write-Output 'PASS: generated index, manager, model and requirement documentation; no inspection runtime dependency.'
$packages.Name | Write-Output
Write-Output 'Package smoke test passed.'
exit 0
