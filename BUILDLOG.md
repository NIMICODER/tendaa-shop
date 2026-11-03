# Build log

What was built during the Meta Global AI Developer Hackathon (Oct 26 to Nov 4, 2026), day by day.
Judges: everything in this repo was built in this period unless listed under "Before the
hackathon".

## Before the hackathon
- Planning only, no product code: `SPEC.md` (product spec), `ARCHITECTURE.md` (design),
  `PLAN.md` (build plan), `README.md` (placeholder), `.gitignore`, and this file.

## Oct 26

## Oct 27
- Solution scaffolded: .NET 10 across Core, Infrastructure, Agent and Web projects, Postgres
  running in Docker, CI on GitHub Actions building and testing every push.
- Agent wired up to Meta's Muse Spark model via the OpenAI-compatible Meta Model API.
- Decided to build the seller dashboard as a React single-page app instead of Blazor, for a
  stronger long-term base after the hackathon — updated the design and build docs before writing
  any dashboard code.

## Oct 28
- React dashboard scaffolded (Vite + TypeScript), proxied to the backend in development and built
  straight into the .NET app for production, with one page proving the health-check endpoint works
  end to end.
- Full business domain modeled: shops, customers (identified by WhatsApp's own id, never phone
  number), products with variants and stock, delivery zones, orders and order items, payment
  proofs, conversations/messages/tool traces, and seller handoffs — every table scoped to a shop
  from day one, ready for more than one seller later.
- Order rules enforced in code, not just instructions: only the seller can mark a payment
  confirmed — the agent is structurally blocked from doing it.
- Database layer wired up: per-shop data isolation applied automatically to every query, first
  migration, and a demo shop seeded with a real catalog and Lagos delivery zones.
- Restructured the whole solution to match established .NET conventions: added a dedicated
  business-logic layer, moved every endpoint to Controllers, simplified the data-access pattern —
  same behavior, same tests passing, cleaner foundation for everything still to build.

## Oct 29
- Agent can now take a full order by text — find the item, check stock, build a running total,
  and send bank details once confirmed — tested live against the real Muse Spark model, not mocks.
- Nine tools wired up (search products, product details, delivery fee, shop info, build order,
  order summary, confirm order, order history, hand off to seller), each reading the customer's
  identity from the server side, never from the model, so one customer can never see another's data.
- Confirmed live: the agent asks for missing order details one at a time instead of listing several
  questions, and offers in-stock alternatives when a requested size is unavailable.
- A refund/complaint message correctly hands the conversation off to the seller instead of the
  agent trying to resolve it itself.
- Found and fixed two real bugs during testing: an Entity Framework default that silently corrupted
  a customer's second edit to their draft order, and a product search that failed on multi-word
  item names (e.g. "black sneakers").
- 219 automated tests passing (up from 195 before today), including one that runs the full
  tool-calling loop without hitting the real model API.
- WhatsApp webhook pipeline built and proven end to end: signature-verified receipt, durable
  event storage with crash-recovery on restart, strict per-customer ordering with parallel
  processing across customers, real replies sent back through the Graph API. First real order
  placed from a real phone, through a real WhatsApp number, start to finish.
- Diagnosed and fixed a real WhatsApp Business API gotcha that nothing in the docs makes obvious:
  Meta's test number comes pre-subscribed to Meta's own demo app, not ours, so a correctly
  verified webhook URL still delivers nothing until the app explicitly subscribes to the WABA.
  Documented the fix in README and the multi-tenant implication in ARCHITECTURE.
- Found and fixed five real bugs by testing against two real phone numbers at once: the agent
  could say a payment was "confirmed" before the seller approved it; it could type the bank
  account number itself instead of a system-guaranteed exact copy; it did its own money math
  instead of using shop-formatted amounts; pre-tool narration ("let me check...") was leaking into
  customer replies; and a failed WhatsApp send was silently re-running the whole AI turn (and
  duplicating the reply) up to three times instead of just retrying the send.
- Added sending a real product photo on WhatsApp when a customer asks to see one, and surfaced
  Meta's exact error code and message on a failed send (found via a real failure: a second test
  number not yet on the account's allowed recipient list).
- Verified — and now protected with automated tests — that two different real customers talking
  to the shop at once never see each other's name, orders, or chat history; added saved customer
  names that carry across conversations and automatic fresh-start handling for conversations idle
  more than 12 hours.
- 262 automated tests passing by end of day.

## Oct 31
- Voice notes: a customer can now order by sending a voice note instead of typing. WhatsApp's
  native audio format gets converted (ffmpeg) and transcribed (Muse Voice Transcribe), and the
  transcript flows through the exact same order-taking path as a typed message.
- Vision, receipts: one Muse Spark call reads a payment screenshot, pulls out the amount, bank,
  date and reference, and a separate deterministic check (plain C#, not the model) compares it
  against the order — matching amount, date on/after the order, reference not reused elsewhere.
  Only a clean match moves the order to payment review; the model is never the one deciding.
- Vision, product photos: a second Muse Spark call matches a customer's photo against the shop's
  own catalog and suggests up to three candidates — every suggested id is checked against the
  real catalog before the agent ever sees it, so a hallucinated id can't reach the customer.
- 286 automated tests passing.

## Nov 1
- Seller dashboard backend: cookie login, every API route behind it, SignalR for live updates.
  Orders (list, detail, confirm or reject a payment — confirming is the first place in the app
  that actually reduces stock), a handoff inbox that replies to the customer over WhatsApp and
  resolves, conversation history with the agent's tool-call trace, catalog editing, and shop
  settings including delivery zones — all calling the same services the WhatsApp agent uses,
  no separate set of business rules.
- A customer-messaging rule that was previously just documented is now enforced in code: a
  seller's reply from the dashboard only goes out if the customer messaged within Meta's 24-hour
  window, otherwise it's skipped rather than silently failing.

## Nov 2
- Seller dashboard frontend: all six pages built and wired to the backend above, plus two pages
  the plan didn't originally call for — a public landing page explaining what the product does,
  and a proper login screen — both added because a dashboard a stranger can't understand from the
  outside isn't actually done.
- A real visual design pass, not just working-but-plain pages: a studied color and layout
  direction (not the default scaffold look) applied consistently across the sidebar, every table,
  every status indicator, the login screen, and the new landing page.
- Found and fixed a real bug in `.gitignore`: a rule meant to exclude locally-saved receipt
  photos was also silently matching source folders named "Media" — the entire voice-note and
  vision codebase, plus its tests, was never going to be committed to git at all. Fixed and
  confirmed those files are now tracked.
- 330 of 344 automated tests passing; the other 14 all fail for the same single reason (a
  database migration that's needed but hasn't been generated yet), not 14 separate bugs.
