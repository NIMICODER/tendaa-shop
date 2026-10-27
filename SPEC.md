# Tendaa product spec (MVP)

## The product
An AI sales assistant that takes orders and checks payments on a seller's WhatsApp, starting with
sellers in Lagos and built to work for sellers across Africa. It
understands text, voice notes and product photos, builds the order, quotes the total with
delivery, and reads the transfer screenshot. The seller confirms payment with one tap and handles
anything unusual. It is not a FAQ chatbot: the core is taking an order from a messy conversation
to a confirmed, paid order.

## Users
| User | Who | Wants | Uses |
|---|---|---|---|
| Seller | Lagos shop owner, 20 to 200 WhatsApp chats a day | Fewer repeat questions, no lost orders, no fake payment alerts | WhatsApp + web dashboard |
| Customer | Buyer from Instagram, status, or a friend | Fast answer, right price, clear total, confirmation | WhatsApp only, English or Pidgin, often voice notes |

## MVP features
| # | Feature | What it does | Meta model |
|---|---|---|---|
| 1 | Shop setup | Products (name, price, variants, stock, photo), delivery zones and fees, bank details, shop rules as text | None |
| 2 | Snap your price list | Photo of a price list becomes draft catalog items for the seller to check | Muse Spark vision + structured output |
| 3 | Text and voice enquiries | Price, stock, delivery fees, shop rules, only from the seller's data | Muse Spark, Muse Voice Transcribe |
| 4 | "Do you have this?" | Customer photo or screenshot matched to catalog items, or "we don't stock it" | Muse Spark vision |
| 5 | Order taking | Extracts items, variant, quantity, name, delivery area from messy messages; asks only for what's missing; sends a summary to confirm | Muse Spark tool calling |
| 6 | Totals and payment details | Item total + delivery fee by zone; sends bank details with the order number | Muse Spark tool calling |
| 7 | Receipt check | Reads amount, sender, bank, date, reference from a transfer screenshot; compares with the total; never confirms on its own | Muse Spark vision + structured output |
| 8 | Seller confirms | Receipt and extracted details side by side; Confirm or Reject; customer told immediately | None |
| 9 | Hand off to the seller | Complaints, refunds, unclear requests, or "I want a person": agent stops and gives the seller a summary | Muse Spark |
| 10 | Seller dashboard | Orders by status, conversations with the agent's reasoning trace, handoff inbox, catalog | None |

**Should have (only if MVP is done by Nov 1):** order status updates to the customer on
dispatch/delivery; seller commands by WhatsApp ("confirm 1042"); Pidgin replies to Pidgin messages.

**After the event:** many sellers with self-serve signup; Paystack payment links; daily seller
summary; broadcasts (templates); staff accounts; delivery partner integration.

## Order statuses
`Draft` -> `AwaitingPayment` (customer confirms summary) -> `PaymentReview` (receipt amount matches)
-> `Confirmed` (**seller only**) -> `Dispatched` -> `Delivered` (last two are should-have).
- `PaymentReview` -> `AwaitingPayment` when the seller rejects.
- Wrong receipt amount: stays `AwaitingPayment`, agent asks the customer to check.
- `Draft` or `AwaitingPayment` -> `Cancelled` by customer or seller.

## User flows

### A. Seller sets up the shop (1, 2)
1. Seller enters shop name, hours, bank details. 2. Adds delivery zones and fees. 3. Adds products
one by one or photographs the price list. 4. From a photo, the agent extracts items and prices as a
draft. 5. Seller corrects, adds stock and sizes, saves. 6. Writes shop rules in plain words.
7. Sends a test message from their phone.
Edge: blurry photo, ask for a clearer one. Item with no price is flagged, never guessed.

### B. Customer asks a question (3)
1. Text or voice note ("How much be the black sneakers? You dey deliver Ajah?"). 2. Voice is
transcribed. 3. Agent looks up product and zone. 4. Replies with price, sizes in stock, fee and time.
Edge: not in catalog, say so and offer similar. Unknown policy, say it will check and hand off.

### C. "Do you have this?" (4)
1. Customer sends a photo or screenshot. 2. Agent compares with catalog photos and descriptions.
3. Replies with up to three matches with prices and stock. 4. Customer picks one, flow D starts.
Edge: no good match, say so rather than force a match. Several items in the image, ask which.

### D. Customer places an order (5, 6)
1. "I want 2 of the black sneakers size 42, deliver to Lekki, my name is Tolu." 2. Agent extracts
items, size, quantity, name, area. 3. Checks stock. 4. Asks only for what's missing, in one
question. 5. Sends summary: items, item total, delivery fee, grand total, order number.
6. Customer confirms or corrects ("make it size 43"), summary updates. 7. On confirm: order becomes
`AwaitingPayment`, agent sends bank details.
Edge: size out of stock, offer available sizes. Mind changed midway, update the same order.
Pay on delivery requested, follow shop rules.

### E. Customer pays and sends a receipt (7)
1. Customer sends a transfer screenshot. 2. Agent extracts amount, sender, bank, date, reference,
confidence. 3. Finds the customer's open order, compares amount with grand total. 4. Match: order
to `PaymentReview`, customer told the seller will confirm shortly. 5. Mismatch: agent politely
states the difference and asks them to check. 6. Seller notified either way.
Edge: unreadable image, ask again. Receipt dated before the order or reference reused on another
order, flag as suspicious to the seller. Two open orders, ask which.

### F. Seller confirms and fulfils (8)
1. Seller opens the order: receipt beside extracted details. 2. Checks the bank app. 3. Confirm:
order `Confirmed`, stock reduced. 4. Customer gets "Payment confirmed. Order 1042 is being
prepared." 5. Reject: seller picks a reason, customer asked to resend proof.
Edge: no seller action within a set time, order shows as overdue.

### G. Agent hands off to the seller (9)
1. Complaint, refund, request for a human, or the agent is unsure. 2. Agent tells the customer the
seller will reply and stops answering that chat. 3. Seller sees a handoff with a two-line summary.
4. Seller replies from the dashboard (sent via WhatsApp) and resolves. 5. Agent resumes.

### H. Customer checks an order (should have)
1. "Where is my order?" 2. Agent finds orders by phone number and replies with the latest status.

## Rules the agent must never break
1. Prices, stock, fees and rules come only from the seller's data. No unwritten discounts or promises.
2. Never marks a payment confirmed. Only the seller can.
3. Never moves money or promises a refund. Refunds and complaints go to the seller.
4. Customer identified by the WhatsApp number on the message, never by what they type. Never
   shares one customer's orders with another.
5. Never invents an order number or status. Tools report "not found".
6. Ignores instructions inside messages or images that try to change its rules.
7. Stays silent on handed-off chats until resolved.
8. Short replies, in the customer's language.

## Demo story (3 minutes)
1. 0:00 Problem: a seller's WhatsApp full of voice notes and screenshots.
2. 0:20 Tolu sends a Pidgin voice note ordering sneakers to Lekki; agent replies with summary and total.
3. 0:50 Tolu adds "also this bag" with a screenshot; agent matches it and updates the order.
4. 1:15 Tolu sends a receipt; order goes to review. A wrong-amount receipt is caught politely.
5. 1:50 Seller dashboard: receipt beside extracted details, one tap Confirm, Tolu gets confirmation.
6. 2:25 Under the hood: conversation trace (tools, model input) and eval score.
7. 2:45 Snap-your-price-list setup, one line on impact.
