# ControlBatyr

A library that keeps business logic cores in a consistent shape: it reads the compiled core and its tests,
generates a document from them, and fails if rules are broken.

Experimental repository. The design pattern the library enforces is in [docs/concept.md](docs/concept.md).
What we do and in what order is in [docs/plan.md](docs/plan.md). Developer rules are in
[AGENTS.md](AGENTS.md).

The sample regenerates its business API during `dotnet build`. Commit the generated documents, including
the `.controlbatyr-generated` ownership manifest, alongside code changes. CI rejects stale documents.
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

No packages are published. Build and verify all three locally with:

```powershell
dotnet pack -c Release -o artifacts/packages
pwsh -File tests/PackageSmoke/smoke.ps1
```

The smoke test packs its own local feed and restores a consumer with an isolated cache. ControlBatyr packages
come only from that feed; Microsoft dependencies come from nuget.org. CI runs it after the main build and tests.
To try the packages in a product, add `artifacts/packages` as a NuGet source alongside nuget.org.

The repository sample references the inspection project with
`ReferenceOutputAssembly="false"`, imports `src/ControlBatyr.Inspection/build/ControlBatyr.Inspection.targets`,
and sets `ControlBatyrToolPath` to its built DLL. The package layout includes the same target under `build/`
and the executable and runtime dependencies under `tools/net10.0/any/`.
