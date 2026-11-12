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

With the inspection package installed in the requirement test project, the product opts in with one property:

```xml
<ControlBatyrDocumentationDirectory>../docs/business-api</ControlBatyrDocumentationDirectory>
```

Mark test methods with `[Requirement<GameManager>(nameof(GameManager.CancelBetAsync))]` from the separate
`ControlBatyr.Requirements` module. The test summary, or its name with underscores replaced by spaces,
appears under the operation with a source link. Unmarked tests are omitted.

Paths are relative to the test project. By default, inspection reads that project's compiled assembly and discovers
referenced cores, models and requirements;
`ControlBatyrAssemblies` can specify a semicolon-separated list of assembly paths for multiple cores.
Adjacent XML comments and portable PDBs supply descriptions and source links.
The sample runs both rules and generation in `Game.Tests`; `Game.Core` does not write documents.
Generation runs after every build in a separate .NET 10 process, fails the build on error, and leaves
unchanged files unwritten. The tool needs a .NET runtime compatible with the inspected product.

No packages are published yet. The sample instead references the inspection project with
`ReferenceOutputAssembly="false"`, imports `src/ControlBatyr.Inspection/build/ControlBatyr.Inspection.targets`,
and sets `ControlBatyrToolPath` to its built DLL. The package layout includes the same target under `build/`
and the executable and runtime dependencies under `tools/net10.0/any/`.
