# OpenIron

A lightweight, self-hosted web application for tracking workouts, logging body weight, and monitoring fitness progress. Perfect for homelab enthusiasts who want full control over their fitness data.

## What is OpenIron?

OpenIron is a gym tracking platform that you can run on your own server. Plan your sessions as templates, log them, and watch your lifts and body weight trend over time - all without sharing your data with third-party services.

I created OpenIron when I became frustrated with the lack of decent fitness tracking software available for gym users. Most apps were focused on cardio/running, required subscriptions, or didn't have the features I wanted. So I created my own - one that focuses primarily on weightlifting/gym activities, and is completely open source and self-hosted.

## Features

- **Accounts** - register and log in; every user's data is private to them.
- **Exercise library** - a built-in set of common lifts, plus your own custom exercises.
- **Workout templates** - save a routine (e.g. "Leg day") with target sets and reps.
- **Workout logging** - start from a template and the weights from your last session are pre-filled, or log an empty workout.
- **Progress charts** - estimated 1RM, top set weight and volume per exercise, weekly training volume, and personal records.
- **Body weight** - log weigh-ins and chart the trend over 30 days, 90 days, a year, or all time.

Weights are in kilograms.

## Architecture

| Path   | What it is |
|--------|------------|
| `api/` | ASP.NET Core 8 Web API, EF Core, PostgreSQL, JWT authentication. Tests in `api/tests`. |
| `web/` | React + TypeScript single-page app built with Vite. Charts are hand-rolled SVG. |

In Docker the web container serves the app and proxies `/api` to the API container, so the browser talks to a single origin.

## Running with Docker

```sh
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD and JWT_SECRET (e.g. `openssl rand -base64 48`)
docker compose up --build
```

The app is then at <http://localhost:8080> (change `WEB_PORT` in `.env` to move it). Database migrations are applied automatically when the API starts. Put a TLS-terminating reverse proxy in front of it if you expose it beyond your LAN.

## Development

Requirements: .NET 8 SDK and Node.js 20+. PostgreSQL is optional - the API can use a local SQLite file instead.

```sh
# API on http://localhost:8080 (SQLite file, dev JWT secret from appsettings.Development.json)
cd api
Database__Provider=Sqlite ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/OpenIron.Api

# Web on http://localhost:5173, proxying /api to the API
cd web
npm install
npm run dev
```

To develop against PostgreSQL instead, start `postgres` from the compose file and run the API without `Database__Provider`; the default connection string in `appsettings.Development.json` matches `docker compose`'s database when `POSTGRES_PASSWORD=openiron_dev_password`.

### Tests

```sh
cd api && dotnet test      # API integration and unit tests (in-memory SQLite)
cd web && npm test         # front end unit tests
cd web && npm run build    # type-check and production build
```

### Database migrations

Migrations target PostgreSQL and live in `api/src/OpenIron.Api/Data/Migrations`.

```sh
dotnet tool install --global dotnet-ef --version 8.0.*
cd api
dotnet ef migrations add <Name> --project src/OpenIron.Api --output-dir Data/Migrations
```

## API overview

All routes are under `/api`. Everything except register and login needs an `Authorization: Bearer <token>` header.

| Route | Purpose |
|-------|---------|
| `POST /auth/register`, `POST /auth/login`, `GET /auth/me` | Accounts |
| `GET/POST /exercises`, `PUT/DELETE /exercises/{id}` | Exercise library (built-ins are read-only) |
| `GET/POST /templates`, `GET/PUT/DELETE /templates/{id}` | Workout templates |
| `GET/POST /workouts`, `GET/PUT/DELETE /workouts/{id}` | Logged workouts |
| `GET /workouts/draft/{templateId}` | A template pre-filled with your last-used weights |
| `GET/POST /body-weight`, `DELETE /body-weight/{id}` | Body weight (one entry per day; logging a day again replaces it) |
| `GET /progress/exercises`, `GET /progress/exercises/{id}`, `GET /progress/overview` | Chart and dashboard data |

## License

See [LICENSE](LICENSE).
