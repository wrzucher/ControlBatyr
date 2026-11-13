# ControlBatyr plan

Status: 2026-11-12. [D] means decided by the owner; [P] means lead proposal.
Drafts and the owner's ideas in his own words: [drafts/](drafts/). The design pattern: [concept.md](concept.md)
(in Russian).

## What the product is

A simple, extensible library that turns the code of a business core into **whole documentation based on the
code, with the code as the single source of truth**. [D]

- It is more than "OpenAPI for the business layer": the end goal is complete documentation — API, requirements,
  diagrams, written text — where every statement is tied to the code it describes.
- You can quickly find where a requirement is implemented in the code, extend it and move on.
- It is a tool against the growth of AI slop in projects developed by AI agents: the documentation is derived,
  checkable and always current.
- Simple by default; everything beyond documentation is an extension.

## Principles

1. **Read the code once, get a typed description, and let anyone work with it.** Between the code and the
   documents there is a **business API model**: domains, operations, events, models, connections, requirements.
   Markdown, diagrams, rules and AI enrichment are consumers of the model. [D]
   **The model is the product.** Its vocabulary — domain, operation, event, model, connection, requirement —
   describes how the business works. Attributes are only one way to fill it, and documents are only one way to
   use it. The roles of the pattern (manager, event publisher, persistence) belong to the reader, not to the
   model. [D]
2. **Traversal with extension points.** The library walks the model and gives extensions the nodes; what an
   extension does with them — one AI call, a supervised workflow, nothing — is not the library's concern. [D]
3. **One identifier for everything.** Every node has a stable identifier (the xmldoc member ID). The same
   identifier is the document anchor, the diagram element, the requirement link, a scenario tag, an insert in a
   handwritten document and the ground of a generated statement. This is what makes the documentation whole. [D]
4. **A role comes from an attribute, not from a name.** [D]
5. **We do not look inside methods.** Only the external shape: signatures, attributes, comments. [D]
6. **Documentation first; validation is an extension.** An attribute is there — describe it; it is not — skip
   it. [D]
7. **AI is never part of the build.** Enrichment is a separate command; its result is committed and reviewed;
   the build only uses what is stored. [D]
8. **No fixed prompts.** Defaults exist, but a product can replace any prompt any way it likes — through a
   prompt provider. It must be obvious where a prompt comes from. [D]
9. **Start simple, add when it hurts.** [D]

## What exists

| Part | Package | State |
|---|---|---|
| Role attributes: manager, object model, event publisher, persistence | `ControlBatyr` | done |
| Domain connections `[On<T>]`; startup wiring | `ControlBatyr`; `ControlBatyr.Connections` | done |
| Business API model with identifiers, written as `business-api.json` | `ControlBatyr.Inspection` | done |
| Business API documents regenerated at build, one file per domain, OpenAPI-like | `ControlBatyr.Inspection` | done |
| Requirements from tests `[Requirement<T>]` | `ControlBatyr.Requirements` | done |
| Shape rules CB0001–CB0007 | `ControlBatyr.Rules` | done, optional |
| Packages and a package smoke test | — | published as previews on nuget.org by a version tag |

Product settings: `ControlBatyrDocumentationDirectory` (where the documents go), `ControlBatyrCheckDocuments`
(fail when committed documents are stale, off by default).

## Next steps

Each step is a separate pull request.

| No. | What we do | Done when |
|---|---|---|
| 1 | ~~The model.~~ Done. | — |
| 2 | ~~ControlBatyr documents itself.~~ Postponed [P]: the library has no managers; marking a reader as a manager would be a lie. It needs a second reader (see "Profiles") and gives little value now. | — |
| 3 | **Class diagram** (Mermaid) per domain from the model, following the architecture diagram profile (`classDiagram` + `%% @arch/1` directives) where GitHub can render it. | Diagrams are in the documents and render on GitHub |
| 4 | **Extension points.** An extension can add nodes and links to the model (Reqnroll scenarios, handwritten documents) and add output. | Reqnroll scenarios with a tag appear as requirements through an extension, without changes in the core |
| 5 | **Publish to NuGet** and try on a real product. | Owner's word |
| 6 | **AI extension** (`Microsoft.Extensions.AI`, `IChatClient`): a separate project and command; prompt provider; generated statements carry the identifiers they are grounded on; stale statements are detected by the fingerprint of their grounds. | To be discussed before work |

## Ideas kept for later [D]

- **Handwritten Markdown and generated parts together.** A handwritten document inserts a generated fragment by
  identifier, or declares which node it is about and gets linked from it. A reference to an identifier that no
  longer exists is reported.
- **More substance in the business layer:** use cases and scenarios in words, each with grounds that can be
  validated — not slop.
- **Supervised generation.** One AI call by default; a product may replace it with a supervised workflow. The
  library only provides the nodes and the place for the result.
- **Reqnroll and the documentation** joined, possibly with an AI helper that proposes which operations a scenario
  belongs to; a person accepts the proposal.

- **Profiles: other readers for the same model.** A profile is a reader that fills the model from another
  kind of source. Candidates: a library (static entry points instead of managers) and a workflow engine (steps
  and transitions stored as data, not code). Not before the business-core profile proves itself on a real
  product. Parallels noticed between a workflow and a business core:

  | Business core | Workflow | Note |
  |---|---|---|
  | operation | step | a named action with inputs and a result |
  | object model | step data, bindings | what actions exchange |
  | event + connection | transition | "after this, that happens" |
  | requirement (test) | evaluated scenario | proof that it works as said |
  | — | order of steps | a core has no explicit process |
  | code, read at build | data, read at run time | only the reader differs |

  A set of connections is an implicit process (choreography); a workflow is an explicit one (orchestration).
  The core model cannot show a process end to end: it does not know which operation raises which event (we do
  not look inside methods). Open question: declare it, or take sequences from scenarios.
- **Outputs are settings.** Markdown, the model file and diagrams are a list of outputs a product turns on and
  points to a folder.

## Critique we keep in mind

- The library checks shape, not meaning: `Process(Guid id)` passes every rule.
- A toy example is forgiving; the real test is a real product.
- Three products are close together: the documentation generator, the rules, and knowledge with AI. The first
  must stay simple and must not be swallowed by the third.
- Every primitive must earn its place: it removes code from the example or the documentation cannot be built
  without it.
