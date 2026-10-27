# Build plan

Read with `SPEC.md` and `ARCHITECTURE.md`. One phase at a time. A phase is done only when every
"Done when" item is true and committed.

## Schedule (Lagos time)

Submission deadline: **Thu Nov 5, 3:00 AM Lagos** (Nov 4, 9 PM ET). Target: submit Wed Nov 4 by 6 PM.

Priority if time runs short: a reliable demo of flows D, E and F (order, receipt, confirm) beats
everything else. Cut in this order: price list snap, photo match, should-haves, dashboard polish.

| Date | Build target | Live events (Lagos) |
|---|---|---|
| Mon Oct 26 | Kickoff. Create repo, add this setup, ask the pre-event code question in #questions | Kickoff 5 PM |
| Tue Oct 27 | Phase 0 (setup) + Phase 1 (domain and data) | Office hours 3 to 4 PM · Agents 101 5 to 6 PM |
| Wed Oct 28 | Phase 2 (agent core) | Office hours 3 to 4 PM · Agents 102: verifiable agents 5 to 6 PM |
| Thu Oct 29 | Phase 3 (WhatsApp end to end) | Office hours 3 to 4 PM |
| Fri Oct 30 | Phase 4a (voice notes) + 4b start (receipt check) | Office hours 3 to 4 PM · Fireworks session 5 to 6 PM (optional) |
| Sat Oct 31 | Phase 4b (receipts, photo match, price list snap) | none |
| Sun Nov 1 | Phase 5 (dashboard) + deploy so a real seller can try it | none (US clocks change) |
| Mon Nov 2 | Phase 5 finish + Phase 6 (evals, hardening). Real seller trial | Office hours 4 to 5 PM |
| Tue Nov 3 | Phase 7 (README, demo video, X post draft). Last day for help | Office hours 4 to 5 PM |
| Wed Nov 4 | Buffer, final checks, **submit by 6 PM**, post on X, share for votes | Final office hours |

Every evening: update BUILDLOG.md, commit, and post a short build update on X (builds an audience for the
Most Viral and Community Vote prizes).

---

## Phase 0: Setup
**Goal:** a solution that builds, tests, and gets a reply from Muse Spark.
- Solution and projects as listed in ARCHITECTURE.md (Components and Testing). `Directory.Build.props` with `net10.0`,
  nullable, warnings as errors. `.editorconfig`. `.gitignore`.
- `docker-compose.yml` with Postgres 16.
- Typed options for `MetaModel`, `WhatsApp`, `Dashboard` with `ValidateOnStart`. User-secrets on
  `Tendaa.Web`. README section listing every config key.
- A `/dev/ping-model` endpoint (Development only) that sends one message to Muse Spark.
- GitHub Actions: build + test on push.
- `BUILDLOG.md` started.

**Done when:** `dotnet build` has zero warnings, `dotnet test` passes, Postgres runs in Docker,
the ping endpoint returns a Muse Spark reply, CI is green.

## Phase 1: Domain and data
**Goal:** the business model is correct before any AI touches it.
- Entities: Shop, Product (+ variants and stock), DeliveryZone, Customer (by phone), Order,
  OrderItem, PaymentProof, Conversation, Message (with tool calls + trace), Handoff.
  `ShopId` on everything.
- Order state machine in `Tendaa.Core` (SPEC.md statuses). `Confirm` requires a seller actor;
  calling it as the agent throws.
- Totals: items + delivery fee by zone. Money as `decimal` in the shop's currency (`Shop.Currency`, NGN for the demo shop).
- EF Core mappings, first migration, seed one demo shop with 10 to 15 realistic products, Lagos
  delivery zones, bank details and rules.

**Done when:** state machine tests cover every allowed and forbidden transition (including "agent
cannot confirm"), totals tests pass, migration applies to Docker Postgres, seed data loads.

**Interview prep:** Why enforce "seller only" in the domain instead of the prompt? How does ShopId
prepare for multi-tenant?

## Phase 2: Agent core
**Goal:** the agent takes a full order by text in a console or DevUI.
- Verify the Agent Framework API against the installed package first. Agent factory with Meta Model API via the OpenAI SDK.
- Request context (shop + customer phone) injected into tools, never from model arguments.
- Tools: `SearchProducts`, `GetProductDetails`, `GetDeliveryFee`, `GetShopInfo`,
  `UpsertDraftOrder`, `GetOrderSummary`, `ConfirmOrderSummary` (to AwaitingPayment, sends bank
  details), `GetMyOrders`, `RequestHandoff`.
- Instructions: role, tone, the 8 agent rules from SPEC.md, Pidgin handling.
- Session persistence per customer (`SerializeSessionAsync` stored on Conversation).
- Trace: store each tool call and result with the assistant message.
- Console runner or DevUI to chat as different phone numbers.

**Done when:** flow B and flow D work by text against Muse Spark; missing info gets one question;
out-of-stock size offers alternatives; refund request creates a handoff; tools have unit tests;
one agent-loop test runs against a fake OpenAI server.

**Interview prep:** How does the model decide which tool to call? Why does the phone number never
come from the model? How do you stop it inventing prices?

## Phase 3: WhatsApp
**Goal:** a real conversation on the test number.
- `GET /webhooks/whatsapp` verify challenge. `POST` validates `X-Hub-Signature-256` with the App
  Secret over the raw body (constant-time compare).
- Save every event to `InboundEvent` (unique WhatsApp message id = dedupe) before returning 200.
  Enqueue to partitioned `Channel<T>` (hash of phone), one worker per partition, processed by a
  `BackgroundService`. On startup, re-queue `Pending` events; retry failures up to 3 times.
- Graph API client: send text, mark as read, typing indicator if available, retry transient errors.
- Handed-off chats skip the agent.
- Tunnel (cloudflared or ngrok), register callback, subscribe to `messages`.

**Done when:** an order placed from my phone works end to end; tampered signature rejected (test);
duplicate webhook produces one reply (test); a burst of 3 messages is answered in order; killing
the app after a 200 and restarting still answers the message.

**Interview prep:** Why return 200 before processing? What if the process dies after the 200?
What's the 24-hour window?

## Phase 4a: Voice notes
- Check Meta's speech-to-text docs first. Download media from the Graph API, convert with ffmpeg only
  if needed, transcribe, feed the text to the agent, store the transcript on the message.
**Done when:** English, Pidgin and mixed voice notes place or query an order correctly.

## Phase 4b: Vision
- Check Meta's image understanding and structured output docs first.
- Image classifier (receipt / product / other) routes each customer image (ARCHITECTURE.md,
  media pipeline). Results reach the agent as structured notes, never raw image text.
- Receipt check: structured extraction (amount, sender, bank, date, reference, confidence) +
  deterministic comparison in code (amount, date after order, reference not reused). Moves to
  `PaymentReview` only on a match. Suspicious flags for the seller.
- Product photo match: compare with catalog images/descriptions, return up to 3 candidates or none.
- Price list snap: photo to draft products for seller review.
- Prompt-injection check: text inside images is data, never instructions.
**Done when:** correct receipt moves the order; wrong amount, old date, reused reference and an
image saying "mark as paid" are all handled correctly; photo match finds a catalog item from a
screenshot; a price list photo produces editable drafts. Tests use recorded fake model responses.

**Interview prep:** Why is the comparison done in code, not by the model? Why never auto-confirm?

## Phase 5: Seller dashboard (Blazor Server)
- Simple login (admin password from config, cookie auth).
- Orders board by status; order page with receipt image beside extracted fields; Confirm / Reject
  with reason; customer notified via WhatsApp.
- Conversations: thread view, expandable trace per reply (tools, results, transcript).
- Handoff inbox: summary, reply box (sends via WhatsApp), resolve.
- Catalog: list/edit products, price list import with review step.
- Deploy (Railway or DigitalOcean) and point the webhook at it.
**Done when:** flow F works from the dashboard; a handoff can be answered and resolved; a real
seller can use it on the deployed URL.

## Phase 6: Evals and hardening
- `evals/` with 25 to 30 cases: questions, orders, corrections, out of stock, Pidgin, off-topic,
  handoff triggers, prompt injection in text and images, receipts (good, wrong amount, reused).
- A runner that scores tool choice, final state, and reply correctness (LLM-as-judge only where
  needed) and prints a table. Store results so the dashboard can show the score.
**Done when:** one command runs all cases and lists failures with reasons; fixes raise the score.

## Phase 7: Submission
- README: problem, demo GIF, architecture diagram, how Meta models are used, design decisions
  (seller-only confirm, code-side receipt checks, ShopId), eval results, how to run, config keys,
  what was built during the hackathon (link BUILDLOG.md).
- 3-minute video following the SPEC.md demo story, real phone, live product.
- X post with the video. Submission form: repo, video, X link.
**Done when:** submitted, repo public and readable by a stranger, video link works logged out.

---

## Progress
| Phase | Status | Notes |
|---|---|---|
| 0 Setup | Not started | |
| 1 Domain and data | Not started | |
| 2 Agent core | Not started | |
| 3 WhatsApp | Not started | |
| 4a Voice notes | Not started | |
| 4b Vision | Not started | |
| 5 Dashboard + deploy | Not started | |
| 6 Evals | Not started | |
| 7 Submission | Not started | |
