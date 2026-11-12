# ControlBatyr development rules

This repository is public. Commits, tests, examples, and documents must not contain work data, people's names,
email addresses, tokens, or names and code from private repositories. Use only fictional domains in examples.

- What we do and in what order is in [docs/plan.md](docs/plan.md). One plan step means one branch and one pull request.
- Everything is in English, except `docs/concept.md` and drafts in `docs/drafts/`, which may be in Russian for now.
- .NET 10, `Nullable` enabled, warnings as errors. Delivery requires passing `dotnet build` and `dotnet test`.
- Choose the simplest option. Add a library primitive only if it removes code from the example or is needed
  to generate the document.
- Show any new public library type or method to the owner first.
- Every test checks a requirement. A rule check must fail on an example that breaks the rule.
- Comments say what the code does not. Do not restate the code in words.
