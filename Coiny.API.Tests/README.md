# Coiny API Tests

From the repository root, with .NET 10 installed:

```sh
dotnet restore Coiny.slnx
dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj
```

These xUnit tests use EF Core's SQLite provider with isolated in-memory
connections. They do not use or reset `Coiny.API/coiny.db`, require an API
server or JWT signing key, or change the development user's data.

Coverage includes:

- Anchored weekly/every-two-weeks schedules and three-paycheck months.
- Twice-monthly/monthly schedules, short months, leap years, inclusive date
  bounds, maximum calendar dates, and invalid definitions.
- Resumable shared drafts, household isolation, publication validation,
  expected edit versions, and rollback after a simulated storage failure.
- Published revision selection, preserved history, scheduled future revisions,
  unassigned income, over-budget plans, and calendar-event projections.
- HTTP save/resume/preview/publication, required JSON fields and query parameters,
  authentication, household isolation, and expected error status codes. The in-process
  test host uses test authentication; it does not exercise JWT token issuance.
- Setup status for unpublished, current, and future plans; previews without publication.
- Migration apply/rollback/reapply with ledger data and existing budget
  definitions preserved.

The current suite has 46 passing tests. Build output and TestResults are ignored
inside this project. Frontend tests run separately from `Coiny.Web`.

See [budget rules](../docs/09-household-budgeting.md) for behavior and the
[setup guide](../docs/06-development-setup.md#verification) for repository checks.
