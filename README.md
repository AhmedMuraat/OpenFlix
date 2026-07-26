# OpenFlix

OpenFlix is a full-stack, Netflix-inspired streaming application for public-domain and openly licensed films. It uses .NET 10 microservices, React, PostgreSQL, Redis, OpenTelemetry, YARP, and .NET Aspire. It is designed to run as a private, local production installation without a domain, cloud account, or payment provider.

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

## Local production

The production stack runs entirely on your computer. Docker Desktop is the only runtime prerequisite; the script builds the application, creates private secrets, starts every service, waits for a health check, and opens the site.

From PowerShell in the repository root:

```powershell
.\scripts\local-production.cmd
```

OpenFlix will be available at `http://localhost:5173`. Both exposed ports bind to `127.0.0.1`, so the site and API are accessible only from this computer.

The first start creates `.env.local` with cryptographically strong database, JWT, and internal API secrets. The file is ignored by Git. PostgreSQL and Redis data are stored in persistent Docker volumes and survive restarts.

Useful operations:

```powershell
# Show container health and status
.\scripts\local-production.cmd -Action Status

# Follow application logs
.\scripts\local-production.cmd -Action Logs

# Create a timestamped PostgreSQL backup in .\backups
.\scripts\local-production.cmd -Action Backup

# Stop OpenFlix while preserving data
.\scripts\local-production.cmd -Action Stop
```

To rebuild after pulling or changing code, run the default start command again. To start without automatically opening a browser, add `-NoBrowser`.

### Payments

Stripe is optional. With the `STRIPE_*` values left blank, the rest of OpenFlix remains fully functional and the Supporter checkout is clearly shown as unavailable instead of failing. No Stripe account is needed for the local installation.

If you later choose to test payments, add Stripe test-mode credentials to `.env.local`; never use live keys on a development computer.

### Local development

For local development without containers:

```powershell
dotnet run --project src/OpenFlix.AppHost
cd src/openflix-web
pnpm install --frozen-lockfile
pnpm dev
```

Development prerequisites are the .NET 10 SDK, Node.js 22+, pnpm, and Docker Desktop for the Aspire-managed database and cache.

## Demo accounts

Development seeding creates no fixed-password accounts. Register through the UI, then assign admin roles through the documented operational workflow rather than committing credentials.

## Security

- Passwords use ASP.NET Core Identity hashing.
- Access tokens are short-lived; refresh tokens are stored as SHA-256 hashes, rotated after use, and sent only in an HttpOnly cookie.
- Stripe webhook signatures are verified.
- Gateway endpoints are rate limited; the web server emits a restrictive content security policy and other browser security headers.
- Local production secrets are generated automatically, kept in `.env.local`, and excluded from source control and container build contexts.
- The browser uses the same local origin for the site and API, and session restoration does not expose the refresh token to JavaScript.
- Containers run as non-root users.

## Media policy

Only add a title when its license permits streaming. Each catalog item records `SourceUrl`, `StreamUrl`, `License`, and `Attribution`. Internet Archive content must be reviewed because upload metadata can be inaccurate. Operators remain responsible for rights verification.

## CI

Pull requests restore, build, test, lint, audit, and build containers. Local production does not require a domain, a cloud deployment, or published container images.

## API

Each service exposes `/health`, `/alive`, and OpenAPI in development. The gateway routes:

- `/api/auth/*` → Identity
- `/api/catalog/*` → Catalog
- `/api/subscriptions/*` → Subscriptions

## License

Application source is MIT licensed. Streamed media retains its original license.
