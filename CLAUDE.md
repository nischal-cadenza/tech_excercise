# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Stargate API — an Astronaut Career Tracking System (ACTS) built as a technical interview exercise. Tracks people, astronaut assignments (duties), ranks, and career timelines via a REST API.

## Build & Run

```bash
# From the API project directory
cd api

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (launches on https://localhost:7204 or http://localhost:5204)
dotnet run

# Apply EF Core migrations / generate the SQLite database
dotnet ef database update
```

Swagger UI available at `/swagger` when running in Development mode.

## Architecture

**CQRS pattern via MediatR** — commands and queries are separate request types dispatched through MediatR handlers.

- **Commands** (`Business/Commands/`): Write operations. `CreateAstronautDuty` uses a MediatR `IPipelineBehavior` pre-processor for validation before the handler executes.
- **Queries** (`Business/Queries/`): Read operations using **Dapper** raw SQL against the SQLite database.
- **Data** (`Business/Data/`): EF Core entities (`Person`, `AstronautDetail`, `AstronautDuty`) and `StargateContext` (SQLite DbContext).
- **Controllers**: Thin HTTP layer — dispatch to MediatR, wrap results in `BaseResponse`.

**Dual ORM approach**: EF Core for writes/migrations, Dapper for read queries.

**Database**: SQLite file (`starbase.db`), connection string in `appsettings.json`.

## Key Business Rules

1. Person uniquely identified by Name
2. Only one current duty at a time (current duty has no end date)
3. Previous duty end date = new duty start date - 1 day
4. Duty title "RETIRED" marks career end; career end date = retired start date - 1 day

## API Endpoints

- `GET /person` — all people
- `GET /person/{name}` — person by name with current astronaut detail
- `POST /person` — create person (body: name string)
- `GET /astronautduty/{name}` — duties by person name
- `POST /astronautduty` — create astronaut duty assignment

## Exercise Tasks

Per the exercise README, the expected work includes:
1. Find and resolve code flaws
2. Enforce business rules
3. Improve defensive coding
4. Add unit tests (>50% coverage)
5. Implement process logging (stored in database)
6. Optional: implement a web UI (Angular preferred)
