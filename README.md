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
- **Settings** - change your password, choose whether to share your training, or delete your account and all your data.
- **Admin panel** - for admins: see who has an account, grant or revoke admin, disable or enable users, reset a password, delete a user, and open or close registration, and create single-use invite codes. Admins see workout counts only, never anyone's training or body weight.
- **Community** - opt in to share your training with other people on the same server, and see theirs: workouts and volume over the last 30 days, weekly volume, main lifts and recent workouts. Sharing is off by default, body weight and notes are never shared, and non-sharing users are indistinguishable from users who don't exist.

Weights are in kilograms.

## Architecture

| Path   | What it is |
|--------|------------|
| `api/` | ASP.NET Core 8 Web API, EF Core, PostgreSQL, JWT authentication. Tests in `api/tests`. |
| `web/` | React + TypeScript single-page app built with Vite. Charts are hand-rolled SVG. |

In Docker the web container serves the app and proxies `/api` to the API container, so the browser talks to a single origin.

## Inviting people

By default anyone who can reach the server can create an account. To restrict that, an admin turns off **Anyone can create an account** in the admin panel. Sign-up is then by invitation only: create an invite there (optionally with a note and an expiry), and give the person the code or the invite link. Each code works once, for one person.

- The code is shown only when you create it. The server stores a hash, so a lost code can't be recovered: revoke it and make another.
- A code isn't used up if the sign-up fails (for example, the username is taken).
- Existing users aren't affected, and turning sign-up back on makes codes unnecessary.

## Running with Docker

```sh
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD and JWT_SECRET (e.g. `openssl rand -base64 48`)
docker compose up --build
```

To get an admin account, set `ADMIN_USERNAME` and `ADMIN_PASSWORD` in `.env`: the account is created (or an existing one promoted) when the API starts. The password is only used if the account doesn't exist yet, so you can remove it from `.env` afterwards. Without it there is no admin.

The app is then at <http://localhost:8080> (change `WEB_PORT` in `.env` to move it). Database migrations are applied automatically when the API starts. Put a TLS-terminating reverse proxy in front of it if you expose it beyond your LAN.

## Development

Requirements: .NET 8 SDK and Node.js 20+. PostgreSQL is optional - the API can use a local SQLite file instead.

```sh
# API on http://localhost:8080 (SQLite file, dev JWT secret from appsettings.Development.json).
# The launch profile also creates an admin account: admin / admin-dev-password
cd api
Database__Provider=Sqlite ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/OpenIron.Api

# Web on http://localhost:5173, proxying /api to the API
cd web
npm install
npm run dev
```

The SQLite database is created from the current model and is never migrated. If you pull a change that alters the schema and the API starts failing with `no such column` errors, stop it and delete `api/src/OpenIron.Api/openiron.db`.

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
| `PUT /account/password`, `POST /account/delete` | Change your password, delete your account |
| `GET /auth/config` | Public: whether anyone can register (otherwise sign-up needs an invite code) |
| `GET /admin/overview`, `GET/PUT /admin/settings` | Admin only: server stats and the registration switch |
| `GET/POST /admin/invites`, `DELETE /admin/invites/{id}` | Admin only: create (the code is returned once), list and revoke invite codes |
| `GET /admin/users`, `PUT/DELETE /admin/users/{id}`, `POST /admin/users/{id}/reset-password` | Admin only: manage users (never your own account) |
| `GET/PUT /community/settings` | Whether you share your activity (default off) |
| `GET /community/users`, `GET /community/users/{username}` | Members who share, and one member's general progress |
| `GET /progress/exercises`, `GET /progress/exercises/{id}`, `GET /progress/overview` | Chart and dashboard data |

## License

See [LICENSE](LICENSE).
