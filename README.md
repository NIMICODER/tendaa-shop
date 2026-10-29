# Tendaa

A WhatsApp sales agent for African sellers, starting in Lagos. It takes orders from text, voice notes and photos,
quotes the right total with delivery, checks transfer receipts, and hands off to the seller when
a person is needed. Built with C#/.NET, Microsoft Agent Framework and Meta's Muse models.

Entry for the Meta Global AI Developer Hackathon (Indie developer track, "Build for Your World").
Built Oct 27 to Nov 4, 2026; see [BUILDLOG.md](BUILDLOG.md).

Work in progress. This README will be completed with setup steps, architecture and a demo video
during the hackathon.

- What it does: [SPEC.md](SPEC.md)
- How it's built: [ARCHITECTURE.md](ARCHITECTURE.md)
- Build plan: [PLAN.md](PLAN.md)
- Seller dashboard: a React app in [`web/`](web/) (Vite + TypeScript), talking to a REST API and
  SignalR hub in `Tendaa.Web`. See ARCHITECTURE.md's "Seller dashboard" section for how it fits
  together, and "Deployment" for the two run modes (dev: two processes; prod: one).

## Running locally

Two run modes, depending on whether you're touching the frontend.

**Dev (frontend + backend, with hot reload):** two processes.
```
dotnet run --project src/Tendaa.Web      # webhooks, /api, OpenAPI doc — http://localhost:5093
npm run dev --prefix web                 # Vite dev server, proxies /api, /hubs, /webhooks to the .NET port
```
Open the Vite dev server's URL (prints on startup, typically `http://localhost:5173`). Requests to
`/api`, `/hubs` and `/webhooks` are proxied through to the ASP.NET Core app so the browser only
ever talks to one origin.

**Prod-like (one process, what actually ships):**
```
npm run build --prefix web               # outputs into src/Tendaa.Web/wwwroot
dotnet run --project src/Tendaa.Web      # serves the built React app + the API from the same host
```
Open `http://localhost:5093` directly — no Vite dev server, no proxy.

## Configuration

Set these with `dotnet user-secrets set <key> <value>` in `src/Tendaa.Web` for local development.
Production uses environment variables with the same names (`:` becomes `__`, e.g. `MetaModel__ApiKey`).
No values are listed here — ask a project maintainer for the real ones.

| Key | What it's for |
|---|---|
| `MetaModel:ApiKey` | Bearer key for the Meta Model API |
| `MetaModel:BaseUrl` | Meta Model API base URL (`https://api.meta.ai/v1`) |
| `MetaModel:ChatModel` | Chat model name for Muse Spark (e.g. `muse-spark-1.3-contributor`) |
| `MetaModel:TranscribeModel` | Speech-to-text model name for Muse Voice Transcribe |
| `WhatsApp:PhoneNumberId` | WhatsApp Cloud API phone number id used to send messages |
| `WhatsApp:WabaId` | WhatsApp Business Account id |
| `WhatsApp:AccessToken` | Graph API access token |
| `WhatsApp:AppSecret` | Meta app secret, used to verify the webhook signature |
| `WhatsApp:VerifyToken` | Token checked on the webhook's `GET` verification challenge |
| `ConnectionStrings:Postgres` | PostgreSQL connection string (matches `docker-compose.yml` for local dev) |
| `Dashboard:AdminPassword` | Password for the seller dashboard's cookie login |
| `Agent:StaleConversationHours` | Optional, defaults to `12`. How long a conversation sits idle before the next message starts a fresh agent session instead of continuing the old one |

## WhatsApp webhook setup

For local development, expose the running app with a tunnel (cloudflared or ngrok) and register
`<tunnel-url>/webhooks/whatsapp` as the Callback URL in Meta's App Dashboard (WhatsApp →
Configuration → Webhook), with `WhatsApp:VerifyToken`'s value as the Verify token. Subscribe to
the `messages` field.

Then **subscribe the app to your WhatsApp Business Account**: `POST
/{waba-id}/subscribed_apps`. Meta's test numbers come subscribed to Meta's own demo app, so this
step is required even after the webhook URL itself is verified — without it, messages are
accepted by Meta but never delivered to this app's webhook.
