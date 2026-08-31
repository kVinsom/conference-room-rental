# API v1

Base path: `/api/v1`. Interactive documentation is available at `/swagger`; the OpenAPI document is available at `/openapi/v1.json`.

## Rooms

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/rooms?page=1&pageSize=20` | List active rooms with pagination. Both values are optional. |
| `GET` | `/rooms/{id}` | Get one active room. |
| `POST` | `/rooms` | Create a room and its service catalogue. |
| `PUT` | `/rooms/{id}` | Replace editable room data and synchronize its services. |
| `DELETE` | `/rooms/{id}` | Archive a room. |
| `GET` | `/rooms/available?date=2030-09-01&start=10:00&end=14:00&minimumCapacity=50` | Find rooms with enough capacity and no overlap. |

## Bookings

`POST /bookings` accepts a local date, local start time, duration in minutes, attendee count, and room-service identifiers:

```json
{
  "roomId": "00000000-0000-0000-0000-000000000000",
  "date": "2030-09-01",
  "start": "10:00:00",
  "durationMinutes": 240,
  "attendeeCount": 50,
  "selectedServiceIds": []
}
```

The response contains the UTC interval, business time-zone identifier, price snapshots, total amount, and a `priceBreakdown` entry for every tariff segment. `GET /bookings/{id}` returns a confirmed booking.

## Reports

- `GET /reports/revenue?from=2030-09-01&to=2030-09-30`
- `GET /reports/utilization?from=2030-09-01&to=2030-09-30`

Report dates are inclusive. A booking belongs to a report period according to its local start date. A single request cannot exceed 367 calendar days.

## Errors

Errors use `application/problem+json` and RFC 7807:

```json
{
  "type": "about:blank",
  "title": "Request conflict",
  "status": 409,
  "detail": "The room is no longer available for the requested time.",
  "instance": "/api/v1/bookings",
  "traceId": "..."
}
```

- `400` — transport or business validation.
- `404` — room or booking not found.
- `409` — duplicate value, optimistic concurrency conflict, or booking overlap.
- `429` — rate limit exceeded.
- `500` — unexpected error without internal implementation details.

## Operational endpoints

- `GET /health/live` — the process is running.
- `GET /health/ready` — the process and PostgreSQL can accept traffic.
