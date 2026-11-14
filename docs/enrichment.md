# Enrichment: sections added to the business API document

Status: design, agreed direction (2026-11-14). Replaces plan steps 4 (extension points) and 6 (AI extension).

## Idea

The core builds `business-api.json` from code; it stays deterministic and knows nothing about extensions.
A product then says **"I want this in the document"**: a *section* — a summary of each operation, a risk note on
each model, a list of scenarios that cover an operation. Each section is computed by a *producer*: any command —
a script, a test-report reader, an AI call. The result is stored in the same document, next to the element it is
about, and rendered in Markdown with it. Other users add their own sections and producers without touching the
core.

## Declaring sections

In the product project, as MSBuild items next to `ControlBatyrOutputs`:

```xml
<ItemGroup>
  <ControlBatyrSection Include="summary" Elements="operation" Command="dotnet tool run my-summarizer" />
  <ControlBatyrSection Include="risks" Elements="model" Command="pwsh ./tools/risks.ps1" />
</ItemGroup>
```

- `Include` — the section name, unique in the document.
- `Elements` — which elements get the section: `domain`, `operation`, `event`, `model`.
- `Command` — the producer. Optional `Enabled="false"` keeps the declaration without running it.

## Producer contract

- The producer is started once per element that needs the section.
- Standard input: an **element view** — not the internal JSON of `business-api.json`, but a separate, documented
  and versioned contract that ControlBatyr owns. The document format can change; the view stays stable for
  producers. It contains:
  - the element itself: kind, name, identifier, description, parameters, result;
  - its **neighbourhood** — the walk around the element: what it calls, which events it raises, which models it
    reads and writes, who calls it;
  - its **code**: file, lines and the source of the method or type (optional per section: `Code="true"`).
- Standard output: the section content as Markdown text. Exit code 0 — success; anything else — the element keeps
  its previous section and the run reports the failure.
- The producer does not write files and does not see the rest of the document. What it needs beyond the view is
  added to the view, not read on the side: then the input hash covers everything the section depends on.
- First version: element and its direct neighbours, code on request. A deeper walk or more kinds of context — when
  a real producer asks for it.

## Storage and incremental runs

Each element in `business-api.json` gets `sections`:

```json
"sections": {
  "summary": { "text": "...", "producer": "dotnet tool run my-summarizer", "input": "sha256:..." }
}
```

`input` is the hash of the element view (with code, when the section asks for it). On the next run a section is
recomputed only when the hash or the producer changed — a change in the method body makes its summary stale; otherwise the stored text is kept. This makes expensive producers (AI) pay only for what
changed in code, and makes a stale section visible: an element whose current input hash differs from the stored
one is reported as stale when the producer is not run.

Sections of elements that no longer exist are dropped. Sections not declared any more are dropped.

## When it runs

Not during a normal build: producers can be slow and cost money. A separate command —
`dotnet controlbatyr enrich` — runs the declared producers, writes the document and the Markdown. The build only
reports stale sections (as a warning; as an error with `ControlBatyrCheckDocuments`).

## Markdown

The domain document shows each section under its element, with the section name as a heading. Sections are
clearly marked as produced, not as part of the code-derived API.

## Order of work

1. Model: `sections` in `business-api.json`; Markdown shows them; stale detection by input hash.
2. Element view (element, direct neighbours, code on request) and the `enrich` command; a trivial sample producer
   in the samples (no AI).
3. AI producer as a separate package or sample (`Microsoft.Extensions.AI`), grounded statements carry the
   identifiers they are about — only after step 2 is used on a real product.

## Not now

- Producers adding new nodes or links to the model (Reqnroll scenarios as requirements) — a section first; a
  structural extension only when a real case needs it.
- Parallel producers, retries, budgets.
