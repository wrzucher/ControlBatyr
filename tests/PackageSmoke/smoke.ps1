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
if ($packages.Count -ne 5) { throw "Expected five packages, found $($packages.Count)." }
foreach ($id in 'ControlBatyr', 'ControlBatyr.Connections', 'ControlBatyr.Requirements', 'ControlBatyr.Inspection', 'ControlBatyr.Rules') {
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
        if ($id -eq 'ControlBatyr' -and @($metadata.dependencies.group.dependency | Where-Object { $_ }).Count -ne 0) {
            throw 'The attribute package must have no package dependencies.'
        }
        if ($id -eq 'ControlBatyr.Inspection' -and
            ($metadata.developmentDependency -ne 'true' -or
             $metadata.frameworkReferences -or
             !$archive.GetEntry('build/ControlBatyr.Inspection.targets') -or
             !$archive.GetEntry('tools/net10.0/any/ControlBatyr.Inspection.dll') -or
             !$archive.GetEntry('tools/net10.0/any/ControlBatyr.Inspection.runtimeconfig.json') -or
             @($archive.Entries | Where-Object FullName -Like 'lib/*').Count -ne 0)) {
            throw 'Inspection must contain build integration and a tool, with no runtime library.'
        }
        if ($id -eq 'ControlBatyr.Rules' -and
            (!$archive.GetEntry('build/ControlBatyr.Rules.targets') -or
             !$archive.GetEntry('tools/net10.0/any/ControlBatyr.Rules.dll') -or
             @($archive.Entries | Where-Object FullName -Like 'lib/*').Count -ne 0)) {
            throw 'Rules must contain build integration and validation, with no runtime library.'
        }
    } finally { $archive.Dispose() }
}

Copy-Item (Join-Path $PSScriptRoot 'Consumer.csproj'), (Join-Path $PSScriptRoot 'Consumer.cs') $consumer
Copy-Item (Join-Path $PSScriptRoot 'ProjectDependency'), (Join-Path $PSScriptRoot 'Runner') $consumer -Recurse
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
    <packageSource key="nuget.org"><package pattern="Microsoft.*" /><package pattern="Markdig" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
$project = Join-Path $consumer 'Consumer.csproj'
Invoke-Dotnet restore $project --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache "-p:ControlBatyrPackageVersion=$version"
Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version"
$document = Join-Path $consumer 'docs/business-api/ticket.md'
if (!(Test-Path (Join-Path $consumer 'docs/business-api/index.md')) -or !(Test-Path $document)) {
    throw 'The package build did not generate business API documents.'
}
$modelFile = Join-Path $consumer 'docs/business-api/business-api.json'
if (!(Test-Path $modelFile) -or
    !(Get-Content (Join-Path $consumer 'docs/business-api/.controlbatyr-generated')).Contains('business-api.json')) {
    throw 'The model must be generated and owned by the manifest.'
}
$model = Get-Content $modelFile -Raw | ConvertFrom-Json
if ($model.Domains[0].Operations[0].Node.Id -ne 'A:Consumer|M:PackageSmoke.TicketManager.Issue(System.Int32)') {
    throw 'The model must expose the compiled operation identifier.'
}
if ($model.Domains[0].Node.Id -ne 'A:Consumer|T:PackageSmoke.TicketManager') {
    throw 'The internal manager must appear in the model.'
}
$markdown = Get-Content $document -Raw
foreach ($text in 'Issues a ticket with the requested identifier.', 'TicketManager', 'Issue', 'Issued ticket keeps its identifier', '[Source]') {
    if (!$markdown.Contains($text)) { throw "Generated document is missing: $text" }
}
$output = Join-Path $consumer 'bin/Debug/net10.0'
if (Test-Path (Join-Path $output 'Markdig.dll')) {
    throw 'The core must exercise a NuGet dependency that is not copy-local.'
}
$references = Get-Content (Join-Path $consumer 'obj/Debug/net10.0/controlbatyr-references.txt')
if (!($references | Where-Object { $_ -like '*Markdig.dll' }) -or
    !($references | Where-Object { $_ -like '*ProjectDependency.dll' }) -or
    ($references | Where-Object { $_ -match '[/\\]ref[/\\]' })) {
    throw 'Inspection must receive runtime NuGet and project assemblies, without reference assemblies.'
}
$tool = Join-Path $cache "controlbatyr.inspection/$version/tools/net10.0/any/ControlBatyr.Inspection.dll"
$legacy = & dotnet $tool (Join-Path $consumer 'legacy-docs') `
    (Join-Path $consumer 'obj/Debug/net10.0/controlbatyr-assemblies.txt') `
    (Join-Path $consumer 'obj/Debug/net10.0/copy-local-references.txt') '' false 2>&1
if ($LASTEXITCODE -eq 0 -or ($legacy -join "`n") -notmatch "Could not load file or assembly 'Markdig") {
    $legacy | Write-Output
    throw 'The class library must reproduce the preview.3 copy-local-only resolution failure.'
}
Write-Output 'PASS: copy-local-only resolution fails on Markdig; runtime assets generate documents for the class library.'
if ((Test-Path (Join-Path $output 'ControlBatyr.Inspection.dll')) -or
    (Get-Content (Join-Path $output 'Consumer.deps.json') -Raw).Contains('ControlBatyr.Inspection')) {
    throw 'Inspection must not be a consumer runtime dependency.'
}
Invoke-Dotnet run --project (Join-Path $consumer 'Runner/Runner.csproj') "-p:ControlBatyrPackageVersion=$version" "-p:RestorePackagesPath=$cache"

Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version" "-p:ControlBatyrCheckDocuments=true"
Add-Content $document 'Stale document'
$stale = & dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version" "-p:ControlBatyrCheckDocuments=true" 2>&1
if ($LASTEXITCODE -eq 0 -or ($stale -join "`n") -notmatch 'documents are stale.*rebuild and commit' -or
    !(Get-Content $document -Raw).Contains('Stale document')) {
    $stale | Write-Output
    throw 'Document checking must reject stale documents without rewriting them.'
}
Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version"
Write-Output 'PASS: check mode accepts current documents and rejects stale documents without writing.'

Invoke-Dotnet restore $project --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache "-p:ControlBatyrPackageVersion=$version" "-p:UseRules=true"
Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version" "-p:UseRules=true"
Write-Output 'PASS: internal manager operations are documented with an ASP.NET Core framework dependency, with and without Rules.'
Invoke-Dotnet restore $project --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache "-p:ControlBatyrPackageVersion=$version"

Add-Content (Join-Path $consumer 'Consumer.cs') @'

[ObjectModel]
public sealed class BrokenModel
{
    public int Value { get; set; }
}

public static class UnattachedRequirements
{
    [Requirement<TicketManager>("Missing")]
    public static void Missing_operation_is_ignored() { }
}
'@
Invoke-Dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version"
if ((Get-Content $document -Raw).Contains('Missing operation is ignored')) {
    throw 'A requirement targeting a missing operation must not appear in documents.'
}
Write-Output 'PASS: without Rules, a mutable model and an invalid requirement target build successfully.'
Invoke-Dotnet restore $project --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache "-p:ControlBatyrPackageVersion=$version" "-p:UseRules=true"
$failure = & dotnet build $project --no-restore "-p:ControlBatyrPackageVersion=$version" "-p:UseRules=true" 2>&1
$failureCode = $LASTEXITCODE
if ($failureCode -eq 0 -or ($failure -join "`n") -notmatch 'CB0006' -or ($failure -join "`n") -notmatch 'CB0007') {
    $failure | Write-Output
    throw 'Packaged rules must reject a mutable model with CB0006 and an invalid target with CB0007.'
}
Write-Output 'PASS: with Rules, the same consumer fails with CB0006 and CB0007.'
if ((Test-Path (Join-Path $output 'ControlBatyr.Rules.dll')) -or
    (Get-Content (Join-Path $output 'Consumer.deps.json') -Raw).Contains('ControlBatyr.Rules')) {
    throw 'Rules must not be a consumer runtime dependency.'
}
Write-Output 'PASS: generated index, manager, model and requirement documentation; no inspection runtime dependency.'
$packages.Name | Write-Output
Write-Output 'PASS: packaged Connections delivers events; base attributes have no package dependencies; JSON model is manifest-owned.'
Write-Output 'Package smoke test passed.'
exit 0
