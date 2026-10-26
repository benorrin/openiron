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
- **Calorie calculator** - a daily calorie and macro target from your profile and goal (lose, maintain or gain). It then watches your weigh-ins: it smooths them into a trend, compares your real rate of change with your goal, re-estimates your actual maintenance calories, and tells you whether you're on track or should adjust. See below.
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

## How the calorie calculator works

1. **Starting point:** your resting burn from the Mifflin-St Jeor equation (sex, age, height and your smoothed weight), multiplied by an activity factor. Each 0.5 kg a week you want to change adds or removes about 550 calories a day (7,700 kcal per kg). Protein is 2 g per kg of body weight, fat 25% of calories, and carbs the rest.
2. **Follow a target:** the app doesn't log food, so you tell it the number you're aiming for. That is the one assumption the check rests on, and you can correct it any time with "I eat something different".
3. **The check:** after a settling period of 5 days, the weigh-ins since you applied the target give your real rate of change. If you ate about *A* kcal and your weight moved *r* kg a week, your true maintenance is *A − r × 1,100*. With at least 14 days and 5 weigh-ins that figure starts to outweigh the formula; the more data, the more it does (it is never allowed to stray more than about a third from the formula, so a bad weigh-in can't wreck the target).
4. **Verdict:** on track, faster or slower than planned, going the wrong way, or drifting while maintaining. When it is off, the page suggests a new target; when it is on track it deliberately suggests nothing, to avoid chasing noise.

Guard rails: adults only (18 to 100), a minimum target (1,200 kcal for women, 1,500 for men, 1,350 otherwise), weight loss capped at 1 kg a week and 1% of body weight, and gain at 0.5 kg a week. It is a guide, not medical advice. Nutrition data is private to you: it never appears on community profiles or in the admin panel.

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
| `GET /nutrition`, `PUT /nutrition/settings`, `POST /nutrition/apply`, `DELETE /nutrition/apply` | Calorie plan: profile and goal, the target you're following, and the check on your progress |
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
