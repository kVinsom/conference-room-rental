# Code style and refactoring rules

This document records the conventions applied during the architecture and resource-usage review. They supersede inconsistent choices in the initial implementation.

## Language and formatting

- Source comments, XML documentation, tests, commit-facing documentation, and operational messages are written in English.
- Seed and reference data are also written in English so the entire repository is language-consistent.
- File-scoped namespaces, four-space C# indentation, LF line endings, nullable reference types, and implicit global usings are enforced through `.editorconfig` and `Directory.Build.props`.
- Explicit local types are preferred over `var`. This differs from the initial `.editorconfig`, which preferred `var` when a type was apparent. Explicit types make handler, mapping, and query code easier to scan without relying on the right-hand expression.
- Collection expressions are used for literal or empty data. LINQ materialization remains explicit with `ToArray()` so allocation and deferred-execution boundaries are visible.
- Primary constructors are used when they remove dependency-injection boilerplate without hiding state or validation. Conventional constructors remain appropriate for aggregates, overload chains, and test fixtures.

## Design rules

- Dependencies point toward the domain. Domain projects do not reference ASP.NET Core, EF Core, or deployment concerns.
- Domain invariants are enforced inside aggregates and shared domain guards, even when transport validation already exists.
- Handlers orchestrate one use case and remain free of HTTP and EF Core details.
- Repository interfaces describe required behavior rather than mirroring EF Core APIs.
- Database operations are asynchronous; purely in-memory EF change tracking such as `Add` remains synchronous.
- Read-only EF queries use `AsNoTracking`. Collection loading is split when it prevents wide duplicated result sets.
- Potentially large result sets are streamed or aggregated in the database instead of being unconditionally materialized.
- Time values have explicit local-versus-UTC semantics. Invalid and ambiguous daylight-saving inputs are rejected.
- Money uses `decimal`, a maximum of two fractional digits, and explicit rounding.

## Readability and duplication

- Repeated validation and mapping logic belongs in focused helpers with domain-specific names.
- Methods should expose one level of abstraction. SQL/EF details stay in Infrastructure; HTTP details stay in Api.
- Comments explain constraints and trade-offs, not syntax that is already visible in code.
- Ordering used with pagination or equal report values includes a deterministic tie-breaker.

## Tests

- Test names follow `Method_WhenCondition_ExpectedResult` or a concise scenario equivalent.
- Unit tests follow arrange, act, assert with visible blank-line boundaries.
- Shared fakes live in test-support files instead of being copied into each test class.
- New behavior requires success, validation, boundary, and regression coverage where applicable.
- Integration tests cover behavior that depends on PostgreSQL, migrations, ASP.NET Core binding, middleware, or serialization.

## Automated enforcement

Release builds treat warnings as errors and enable the latest recommended .NET analyzers. CI runs build, tests with coverage collection, and Docker Compose validation. Before merging, run:

```bash
dotnet format ConferenceRoomRental.sln --verify-no-changes --severity warn
dotnet build ConferenceRoomRental.sln --configuration Release
dotnet test ConferenceRoomRental.sln --configuration Release
docker compose config --quiet
```
