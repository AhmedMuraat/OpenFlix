# OpenFlix

OpenFlix is a full-stack, Netflix-inspired streaming application for public-domain and openly licensed films. It uses .NET 10 microservices, React, PostgreSQL, Redis, Stripe test-mode subscriptions, OpenTelemetry, YARP, and .NET Aspire.

> OpenFlix does not scrape pirate sites or redistribute protected media. Catalog entries must include a source and license. The built-in Internet Archive adapter only exposes allow-listed public-domain or Creative Commons items.

## Architecture

| Component | Responsibility |
|---|---|
| Web | Responsive React/Vite browsing and playback UI |
| Gateway | YARP edge routing, rate limiting, CORS, health aggregation |
| Identity | Registration, login, JWT refresh rotation, user roles |
| Catalog | Licensed media metadata, search, trending rows, watch progress |
| Subscriptions | Plans, Stripe Checkout, signed webhook processing |
| AppHost | Aspire orchestration and service discovery |

## Quick start

Prerequisites: .NET 10 SDK, Node 22+, Docker Desktop.

1. Copy `.env.example` to `.env` and replace every placeholder.
2. Run `docker compose up --build`.
3. Open `http://localhost:5173`.

For local development without containers:

```bash
dotnet run --project src/OpenFlix.AppHost
cd src/openflix-web
npm ci
npm run dev
```

## Demo accounts

Development seeding creates no fixed-password accounts. Register through the UI, then assign admin roles through the documented operational workflow rather than committing credentials.

## Security

- Passwords use ASP.NET Core Identity hashing.
- Access tokens are short-lived; refresh tokens are stored as SHA-256 hashes and rotated.
- Stripe webhook signatures are verified.
- Gateway endpoints are rate limited and emit security headers.
- Secrets are configuration-only and excluded from source control.
- Containers run as non-root users.

## Media policy

Only add a title when its license permits streaming. Each catalog item records `SourceUrl`, `StreamUrl`, `License`, and `Attribution`. Internet Archive content must be reviewed because upload metadata can be inaccurate. Operators remain responsible for rights verification.

## CI/CD

Pull requests restore, build, test, lint, audit, and build containers. Pushes to `main` publish versioned images to GHCR. Production deployment is intentionally environment-neutral: configure the `production` GitHub environment and its `DEPLOY_WEBHOOK_URL` secret to trigger your container host.

## API

Each service exposes `/health`, `/alive`, and OpenAPI in development. The gateway routes:

- `/api/auth/*` → Identity
- `/api/catalog/*` → Catalog
- `/api/subscriptions/*` → Subscriptions

## License

Application source is MIT licensed. Streamed media retains its original license.

