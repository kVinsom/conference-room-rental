# Conference Room Rental API

Production-style REST API for conference room management, availability search, time-based booking prices, and business reporting. The solution uses ASP.NET Core 10, PostgreSQL, Clean Architecture, SOLID principles, and dependency inversion.

## Features

- Create, read, update, and safely archive conference rooms.
- Maintain a paid service catalogue for each room.
- Search room availability by local date, time, and minimum capacity.
- Prevent overlapping bookings at both the application and database levels.
- Calculate morning, standard, peak, and evening tariff segments.
- Preserve historical room-rate and service-price snapshots.
- Produce revenue and utilization reports with bounded, streaming aggregation.
- Publish OpenAPI 3.1 and Swagger UI documentation.
- Return RFC 7807 Problem Details for validation, conflicts, rate limiting, and unexpected errors.
- Provide PostgreSQL migrations, seed data, Docker Compose, and a Postman collection.
- Cover domain, application, and end-to-end HTTP behavior with automated tests.
- Validate builds, tests, coverage collection, and Docker Compose in CI.

## Local test stack with Docker Compose

Docker with the Compose plugin is required.

```bash
docker compose up --build
```

After the services become ready:

- Swagger UI: [http://localhost:18080/swagger](http://localhost:18080/swagger)
- OpenAPI document: [http://localhost:18080/openapi/v1.json](http://localhost:18080/openapi/v1.json)
- Readiness probe: [http://localhost:18080/health/ready](http://localhost:18080/health/ready)

The API applies pending migrations on startup. An empty database receives the reference data from the specification:

| Room | Capacity | Base hourly rate |
|---|---:|---:|
| Room A | 50 | 2,000 UAH |
| Room B | 100 | 3,500 UAH |
| Room C | 30 | 1,500 UAH |

Each room receives the specified services: projector — 500 UAH, Wi-Fi — 300 UAH, and sound equipment — 700 UAH. All persisted seed names are written in English for repository-wide language consistency.

Stop the services with `docker compose down`. The destructive `docker compose down -v` command also removes the local PostgreSQL volume.

## Run the API without Docker

.NET SDK 10 and PostgreSQL 16 or later are required.

```bash
dotnet tool restore
dotnet restore ConferenceRoomRental.sln
dotnet run --project src/ConferenceRoomRental.Api
```

Override the connection string without editing a tracked file:

```text
ConnectionStrings__Database=Host=localhost;Port=5432;Database=conference_room_rental;Username=postgres;Password=postgres
```

Do not keep production passwords in `appsettings.json`. Supply secrets through the deployment platform. Set `Database__ApplyMigrations=false` when migrations are executed by a separate deployment job.

## Pricing rules

| Local time in `Europe/Kyiv` | Multiplier |
|---|---:|
| 06:00–09:00 | 0.90 |
| 09:00–12:00 | 1.00 |
| 12:00–14:00 | 1.15 |
| 14:00–18:00 | 1.00 |
| 18:00–23:00 | 0.80 |

A booking that crosses a tariff boundary is split into independently rounded segments. For example, Room A from 11:00 to 15:00 costs `1 × 2000 + 2 × 2000 × 1.15 + 1 × 2000 = 8600 UAH`. Selected service fees are charged once per booking.

## Development and verification

```bash
dotnet build ConferenceRoomRental.sln --configuration Release
dotnet test ConferenceRoomRental.sln --configuration Release
dotnet format ConferenceRoomRental.sln --verify-no-changes --severity warn
docker compose config --quiet
```

Integration tests create an isolated PostgreSQL 16 container through Testcontainers, so Docker must be running. Test coverage includes:

- aggregate invariants and monetary validation;
- every pricing period, boundary segmentation, and rounding;
- invalid, ambiguous, and overflowing local date-time ranges;
- booking creation, overlap, identifier, capacity, and service validation;
- streaming revenue and utilization aggregation;
- room lifecycle and service-catalogue replacement;
- concurrent booking and archival conflicts;
- pagination defaults, availability, reports, Problem Details, health, OpenAPI, and Swagger.

Create a new migration with:

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project src/ConferenceRoomRental.Infrastructure \
  --startup-project src/ConferenceRoomRental.Api \
  --output-dir Persistence/Migrations
```

## Documentation

- [Architecture decisions](docs/architecture.md)
- [API routes and contracts](docs/api.md)
- [Code style and refactoring rules](docs/code-style.md)
- [Postman collection](postman/ConferenceRoomRental.postman_collection.json)
- [HTTP scratch file](src/ConferenceRoomRental.Api/ConferenceRoomRental.Api.http)

OpenAPI is the source of truth for complete request and response schemas. The design follows [Microsoft .NET application architecture guidance](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures), while integration tests follow the [Testcontainers for .NET guidance](https://dotnet.testcontainers.org/examples/aspnet/).

## Production boundaries

The source specification does not define users, roles, or an identity provider, so authentication was not invented. Before exposing the API publicly, place it behind an OIDC/OAuth 2.0 gateway, separate read/write/report permissions, add centralized logs and telemetry, and execute migrations as a dedicated deployment step.
