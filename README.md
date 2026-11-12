# ControlBatyr

A library that reads the compiled business logic core and its tests and generates documentation from
their attributes. Pattern validation is an optional extension, `ControlBatyr.Rules`.

Experimental repository. The design pattern is in [docs/concept.md](docs/concept.md).
What we do and in what order is in [docs/plan.md](docs/plan.md). Developer rules are in
[AGENTS.md](AGENTS.md).

The sample regenerates its business API during `dotnet build`. Commit the generated documents, including
the `.controlbatyr-generated` ownership manifest, alongside code changes. Document checking is off by
default; this repository enables it in CI.
Handwritten files can share the documentation folder; generation only replaces or removes files in its
manifest and refuses to overwrite an existing unowned document.

## Getting started

Use .NET 10. Add `ControlBatyr` to the core and model projects that use its attributes and connections:

```xml
<PackageReference Include="ControlBatyr" Version="0.1.0" />
```

In the requirement test project, reference the core project and add:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <ControlBatyrDocumentationDirectory>../docs/business-api</ControlBatyrDocumentationDirectory>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="ControlBatyr.Requirements" Version="0.1.0" />
  <PackageReference Include="ControlBatyr.Inspection" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

Enable `GenerateDocumentationFile` in the core and model projects too, so their XML comments are available.
The only ControlBatyr setting needed is the documentation folder. Inspection runs during the test project's
build and adds no runtime dependency. Reference Inspection directly in each project that generates documents.

This setup generates documentation only. Missing comments and pattern violations do not fail inspection;
a requirement pointing to no documented operation is simply omitted. Assembly loading and other errors
that prevent document generation still fail the build.

To also validate the six shape rules (CB0001–CB0006) and requirement targets (CB0007), add this reference
alongside Inspection in the project that generates documents:

```xml
<PackageReference Include="ControlBatyr.Rules" Version="0.1.0" PrivateAssets="all" />
```

Rules run in the same inspection step and fail the build on violations. The Rules package's build target
passes its assembly path to Inspection, which loads it only when supplied. This keeps validation in its
own package without a public extension API or another inspection process. Neither build tool is a
product runtime dependency.

To check committed documents in CI, use:

```powershell
dotnet build -p:ControlBatyrCheckDocuments=true
```

Or set `<ControlBatyrCheckDocuments>true</ControlBatyrCheckDocuments>` in the generating project.
Check mode compares documents and their ownership manifest without writing; differences fail the build
with “documents are stale; rebuild and commit”. Run a normal build locally and commit the generated files.

Mark test methods with `[Requirement<GameManager>(nameof(GameManager.CancelBetAsync))]` from the separate
`ControlBatyr.Requirements` module. The test summary, or its name with underscores replaced by spaces,
appears under the operation with a source link. Unmarked tests are omitted.

For `[On<TPublisher>]` connections, register publishers as singletons, managers as scoped services and logging.
After building the service provider, call once at startup:

```csharp
using ControlBatyr;

services.ConnectDomains(typeof(GameManager).Assembly);
```

Pass every assembly containing connection methods. Each event runs its action in a fresh scope; failures are logged.

Paths are relative to the test project. By default, inspection reads that project's compiled assembly and discovers
referenced cores, models and requirements;
`ControlBatyrAssemblies` can specify a semicolon-separated list of assembly paths for multiple cores.
Adjacent XML comments and portable PDBs supply descriptions and source links.
The sample runs both rules and generation in `Game.Tests`; `Game.Core` does not write documents.
Generation runs after every build in a separate .NET 10 process, fails the build on error, and leaves
unchanged files unwritten. The tool needs a .NET runtime compatible with the inspected product.

No packages are published. Build and verify all four locally with:

```powershell
dotnet pack -c Release -o artifacts/packages
pwsh -File tests/PackageSmoke/smoke.ps1
```

The smoke test verifies documentation without rules, rejection of an invalid model and requirement target
with rules, and document checking. It packs its own local feed and restores a consumer with an isolated cache.
ControlBatyr packages come only from that feed; Microsoft dependencies come from nuget.org.
CI runs it after the main build and tests.
To try the packages in a product, add `artifacts/packages` as a NuGet source alongside nuget.org.

The repository sample references the inspection project with
`ReferenceOutputAssembly="false"`, imports `src/ControlBatyr.Inspection/build/ControlBatyr.Inspection.targets`,
and sets `ControlBatyrToolPath` to its built DLL. The package layout includes the same target under `build/`
and the executable and runtime dependencies under `tools/net10.0/any/`.
The sample also references the Rules project with `ReferenceOutputAssembly="false"` and sets
`ControlBatyrRulesPath` to its built DLL, matching the Rules package target.
