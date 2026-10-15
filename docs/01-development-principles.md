# Development Principles

[Documentation index](../README.md#documentation)

## Agree on behavior before code changes

Discuss the scope, business rules, and user experience before implementation.
Get approval for the proposed code changes, then complete and verify that
increment. Discuss meaningful scope changes before adding them. Commit and push
when requested.

## Let business rules lead

Model household ownership and financial behavior explicitly. The API owns
validation and authorization; frontend checks improve the experience but do not
replace backend rules.

Controllers handle HTTP contracts. Services own workflows and financial rules.
DTOs keep API contracts separate from EF entities. EF configurations describe
relationships, indexes, and storage mapping.

## Keep the ledger authoritative

An account's opening balance and transaction history define its balance.
Financial history should be preserved where practical. Current transaction APIs
support creation and listing; edit/correction workflows still need design.

Avoid independently maintained totals. Daily account snapshots are an intentional
stored projection of the ledger, introduced to support historical trends. They
are recalculated when backdated transactions affect them and saved atomically
with the originating account or transaction change. They are not a second source
of truth. See [balance history](03-account-trends.md).

## Prefer the simplest correct implementation

Use readable, focused code and existing services before adding abstractions.
Keep generic trend calculations independent of account selection and snapshot
storage. Reuse UI components where their behavior is shared, without introducing
a framework for every similar row or card.

Use C# `decimal` for financial calculations. Do not round underlying trend values
for presentation. Formatting and semantic color decisions belong in the UI.

## Verify meaningful behavior

Check financial boundaries, household isolation, dates, and rollback behavior.
Frontend tests should cover interactions, states, and contracts rather than
repeat styling declarations. Run the checks appropriate to the change and report
known failures honestly. Temporary local checks are not a committed test suite.

Database migrations belong in source control. Apply them deliberately; resetting
local demo data is a separate, destructive workflow documented in
[demo data](05-demo-data.md).

## Maintain the documentation with the feature

Document what exists, why it behaves that way, and what remains planned. Keep
business rules in the relevant guide and link to them from setup and API docs.
Update payload examples when contracts change.

Do not commit local secrets, SQLite files, or generated output as part of feature
changes. The repository currently contains tracked API `bin/` and `obj/` artifacts;
cleaning that history and expanding ignore rules is separate maintenance work.
