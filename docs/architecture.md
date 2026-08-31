# Architecture

## Solution style

The solution is a modular monolith organized with Clean Architecture. Microservices would add network and operational complexity without a matching business benefit for the current bounded context. Project references enforce inward dependencies:

```text
Api ───────────────┐
                   ▼
Application ───► Domain
     ▲
     │
Infrastructure ───► Domain
```

- `Domain` contains aggregates, invariants, and pricing rules. It does not reference EF Core or ASP.NET Core.
- `Application` contains use cases, repository ports, DTOs, validation, and orchestration.
- `Infrastructure` contains EF Core, PostgreSQL, migrations, and repository adapters.
- `Api` contains transport contracts, routes, OpenAPI, Problem Details, rate limiting, and health checks.

Commands and queries use explicit handlers without MediatR. For this bounded context, explicit registration keeps control flow discoverable and avoids an unnecessary runtime dependency.

## Request flow and transaction boundaries

```text
HTTP request
  -> API contract validation and mapping
  -> Application use-case handler
  -> Domain aggregate and invariant enforcement
  -> Repository ports
  -> EF Core unit of work
  -> PostgreSQL constraints
```

API contracts never enter the domain directly. The API maps transport models to application commands and queries, while application handlers coordinate domain objects through repository interfaces. Infrastructure implements those interfaces and remains replaceable without changing use cases.

Each command uses one scoped `AppDbContext` and one `SaveChangesAsync` call as its transaction boundary. Queries never call the unit of work and use no-tracking projections or aggregates. Cancellation tokens flow from HTTP through handlers and repositories to database operations. Application checks provide useful errors, while unique, concurrency, and booking-overlap constraints remain the authoritative protection against races.

## Key decisions

### Time

Clients send local `date` and `time` values. The application interprets them in the configured `Europe/Kyiv` time zone, rejects invalid or ambiguous daylight-saving values, and stores UTC in PostgreSQL `timestamp with time zone` columns.

Bookings must start and end on the same local date within 06:00–23:00. Intervals are half-open: `[start, end)`. Therefore, 10:00–11:00 and 11:00–12:00 do not overlap.

### Money and tariffs

All amounts use `decimal(18,2)` and UAH. Domain-level money guards centralize positive/non-negative validation and currency precision. The rental charge is split at tariff boundaries, and each segment is rounded to cents with `MidpointRounding.AwayFromZero`. Optional services are charged once.

Bookings store snapshots of the base hourly rate, service names, and service prices. Later catalogue changes cannot alter historical totals.

### Concurrency

The application checks availability before writing so ordinary conflicts produce a clear `409 Conflict`. PostgreSQL remains authoritative through an exclusion constraint over `RoomId` and `tstzrange(StartsAtUtc, EndsAtUtc, '[)')`. Two concurrent requests cannot confirm overlapping bookings for the same room.

Room updates use optimistic concurrency through PostgreSQL's `xmin` system column.

### Persistence and resource usage

Read queries are no-tracking. Room list and availability queries use split collection loading with deterministic ordering to avoid unnecessarily wide result sets. Entity insertion uses synchronous EF `Add` because no asynchronous value generator is involved; database I/O remains asynchronous in `SaveChangesAsync`.

Aggregate and child identifiers are generated in the domain. EF mappings therefore use `ValueGeneratedNever` for these keys, keeping persistence metadata aligned with domain ownership and ensuring newly attached child entities are inserted rather than mistaken for existing rows.

Report rows are streamed from EF Core and accumulated in memory by room. Memory therefore grows with the number of rooms rather than the number of bookings. Reports also use a composite `(Status, StartsAtUtc)` index and are limited to 367 calendar days.

### Deletion

Rooms are archived with `IsActive = false` rather than physically deleted. This preserves booking history and reports. Archiving is rejected when a room has a future confirmed booking.

### Error contracts

Transport validation, domain validation, conflicts, rate limiting, and unexpected errors use RFC 7807 Problem Details. Unexpected responses do not expose stack traces and include a trace identifier for support correlation.

## Testing strategy

- Domain unit tests verify aggregate invariants, monetary precision, tariff boundaries, snapshots, and resource bounds without infrastructure.
- Application unit tests verify orchestration, mapping, validation, cancellation-aware repository calls, and streaming report aggregation with focused fakes.
- API integration tests run the real ASP.NET Core pipeline against an isolated PostgreSQL container. They apply production migrations and cover serialization, Problem Details, health/OpenAPI endpoints, lifecycle workflows, reporting, and concurrent booking conflicts.
- PostgreSQL-specific behavior is not replaced with an in-memory provider because transaction, exclusion-constraint, timestamp, and collation behavior must match production.

This split keeps most tests fast while retaining high-confidence coverage at the database and HTTP boundaries.

## Security boundaries

- Data Annotations validate transport shape; domain invariants protect every caller.
- EF Core sends parameterized SQL.
- Pagination, report ranges, service counts, and booking durations are bounded.
- Readiness and liveness probes support orchestration.
- The runtime container uses a non-root user.
- Secrets are supplied through environment variables or the deployment platform.

Authentication is intentionally absent because the specification defines no users, roles, or identity provider. Before public production access, place the API behind OIDC/OAuth 2.0 and add policies such as `rooms:write`, `bookings:write`, and `reports:read`.

## Extension points

Additional bounded contexts can be introduced without changing the domain core: customers and organizations, payments, cancellation and refunds, recurring bookings, outbox/integration events, and materialized reporting views.
