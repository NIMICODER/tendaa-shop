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
