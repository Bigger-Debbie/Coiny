# Development principles

- Keep ownership checks and financial validation in the API. Controllers handle
  HTTP, services handle workflows, DTOs define contracts, and EF configurations
  define storage.
- Treat opening balances and transactions as authoritative. Rebuild snapshots when
  history changes and commit ledger/snapshot changes together.
- Use `decimal` for money. Round allocations deliberately; keep formatting in the UI.
- Prefer focused services and shared UI components over speculative abstractions.
- Test financial boundaries, household isolation, dates, concurrency, and rollback.
  UI tests should verify interactions and contracts rather than CSS declarations.
- Apply migrations deliberately. Resetting demo data is a separate destructive action.
- Update the relevant guide and payload examples with a behavior change. Link to
  existing explanations instead of repeating them.
- Keep secrets, databases, generated files, and local test evidence out of commits.
  Existing tracked API `bin/` and `obj/` artifacts need separate repository cleanup.
