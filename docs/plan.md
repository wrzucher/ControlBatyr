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
| Domain connections `[On<T>]` and their startup wiring | `ControlBatyr` | done; wiring moves to an extension (step 1) |
| Business API documents regenerated at build, one file per domain, OpenAPI-like | `ControlBatyr.Inspection` | done |
| Requirements from tests `[Requirement<T>]` | `ControlBatyr.Requirements` | done |
| Shape rules CB0001–CB0007 | `ControlBatyr.Rules` | done, optional |
| Packages and a package smoke test | — | built, not published |

Product settings: `ControlBatyrDocumentationDirectory` (where the documents go), `ControlBatyrCheckDocuments`
(fail when committed documents are stale, off by default).

## Next steps

Each step is a separate pull request.

| No. | What we do | Done when |
|---|---|---|
| 1 | **The model.** Reading, model and rendering are separated; nodes carry identifiers; the model is also written as a file next to the documents. Connection wiring moves to its own extension package. The coverage counter and "No requirements" are removed: they push towards a test per method, which the concept calls harmful. | The generated Markdown is the same except the removed lines; a test reads the model file |
| 2 | **ControlBatyr documents itself.** The library's own entry points and model are marked with its attributes and documented by itself. | The library's documents are generated at its build and committed |
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

## Critique we keep in mind

- The library checks shape, not meaning: `Process(Guid id)` passes every rule.
- A toy example is forgiving; the real test is a real product.
- Three products are close together: the documentation generator, the rules, and knowledge with AI. The first
  must stay simple and must not be swallowed by the third.
- Every primitive must earn its place: it removes code from the example or the documentation cannot be built
  without it.
