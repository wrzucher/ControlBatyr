# ControlBatyr plan

Status: 2026-11-12, step 1 complete. [D] means decided by the owner; [P] means lead proposal.

## Why

The pattern is described in [concept.md](concept.md), which is in Russian. The business logic core follows it:
a manager as the domain API, object models, business rules, events, and domain connections. In each product,
the rules are discussed again, and the code still drifts. The library should enforce the shape itself,
for both people and AI agents. [D]

The minimum task is **business API generation**: the document is generated from the core, like OpenAPI from
controllers. [D]

- The document is regenerated **at build time** and stored in the repository. Pull request reviews always
  show the current business API diff, out of the box. [D]
- If a rule is broken or the document cannot be generated, the build fails. [D]
- Editor hints are not needed. [D]
- The product configures the documentation root folder. There may be several documents. [D]

**Documentation is the product.** We want good documentation: text, decisions, diagrams, and schematics.
Content generated from the core is part of it: handwritten Markdown and generated documents will include
each other. We are not doing this now, but choose the format and build setup to support it later. [D]
- The library does not handle reason codes or how operations fail: that is up to the product. [D]

## Three levels

Start simple, then add complexity. [D]

| Level | What we describe | Status |
|---|---|---|
| 1. External core API | manager methods, object models, events, domain connections | first |
| 2. Requirements | tests linked to manager methods; a separate module | second |
| 3. Method internals | which event a method raises, how it fails | may not be implemented |

We do not inspect method internals: we describe the external API, as OpenAPI describes controllers.
There is no source parsing (Roslyn). The library reads the compiled assembly through reflection and takes
comments from the xmldoc file. [D]

**An attribute defines the role, not the name.** A type marked as a manager must follow manager rules:
the library describes the type and checks its rules. This keeps the library independent of others' naming
conventions. [D]

| Attribute | Meaning for the library |
|---|---|
| manager | public methods go into the document; manager rules apply |
| object model | data and domain methods go into the document; data is immutable |
| event publisher | events are named after domain facts, such as `BetCancelled`, and go into the document; notification methods use the `Notify…` prefix |
| subcontext | excluded from the document; we check who uses it |
| `[On]` | domain connection: this event triggers this action |
| `[Requirement]` | a test is a requirement for this manager method (level 2) |

Exact attribute names: step 1, with owner review.

## Repository layout

```
Directory.Build.props         .NET 10, Nullable, warnings as errors
src/ControlBatyr              referenced by the product core: attributes only
src/ControlBatyr.Inspection   core inspection, rules, document generation; runs during the product build
tests/ControlBatyr.Tests      library tests: small invalid cores on which a rule must fail
sample/Game.Model             object models
sample/Game.Persistence       subcontext using in-memory SQLite
sample/Game.Core              one manager, specification, event publisher
sample/Game.Tests             requirement tests and a test that the document is generated and up to date
sample/docs/                  generated example documentation; stored in the repository for reading
.github/workflows/ci.yml      build and tests
```

The example is a fictional betting game: one domain, 2–3 manager methods. This is the example's vocabulary;
we are not designing the game itself.

## Steps

Each step is a separate pull request. After each step, show it to the owner, who decides whether to continue.

| No. | What we do | Done when |
|---|---|---|
| 1 (done) | Example following the pattern and role attributes | Build and tests pass; a manager method reads like text; every test is a requirement |
| 2 | Level 1 document: methods, objects, events. Run by a test for now | The document in `sample/docs/` is generated; the owner has read it and judged the presentation |
| 3 | Regenerate the document during the build; configure the documentation folder | `dotnet build` of the example updates the document in the configured folder; CI fails if the repository copy is stale |
| 4 | Shape rules (below) | The example has no violations; each rule has an invalid core where the build fails with a clear message |
| 5 | Second domain and `[On]` connections; connections in the document | The connection works in a test and appears in the document as “event → action” |
| 6 | Level 2: requirements module, `[Requirement]` | Requirements appear under their method in the document |
| 7 | Decisions based on results: NuGet package, trial in a real product | — |

**Step 4 rules**, all checked through reflection:

- a manager does not receive another manager or `IServiceProvider` in its constructor;
- manager methods accept only simple values, with no object models as inputs;
- a subcontext has exactly one manager;
- a publisher has no generic method or method accepting “any event”;
- public manager methods and events have comments; otherwise the document cannot be generated;
- an object model does not change its data: no mutable properties.

A method without requirements does not fail the build: the document labels it “no requirements”.

## Critique

- **The build reads what it has just compiled.** Step 3 is the least explored part of the plan: after
  compilation, the package must read the core assembly and write a file. If this proves fragile, a test is
  the fallback.
- **The library checks shape, not meaning.** A method named `Process(Guid id)` passes every rule. Name quality
  is still a review concern.
- **A toy example is too forgiving.** The hard parts of the pattern—long-running work, workers, in-memory
  data—are left out. The real test is a trial in a product, step 6.
- **xmldoc and warnings as errors.** Enabling the documentation file requires a comment for every public
  member, which leads to restating the code. We disable this warning; our rule requires comments only where
  they go into the document.
- **Risk of a makeshift framework.** The only safeguard: add a primitive only if it removes code from the example.

## Undecided

- Package and attribute names: owner review in step 1.
- How strictly to enforce the test shape.
- A dedicated type for connections or a service: decide before step 5.
