# MessageRouter API — Design Review Decisions

**Date:** 2026-02-20
**Participants:** API design council review session
**API:** Sinch MessageRouter API (wrapper around Sinch Conversation API)

---

## Executive Summary

The MessageRouter API went through a brutal design review to answer one
question: **does this wrapper earn its existence?** The original draft had
26 operations, a Contacts resource, a "routing" abstraction, and channel-
specific features leaking into a channel-agnostic surface. The review cut
it to 13 operations and established clear principles about what belongs in
this API versus the underlying Sinch Conversation API, Dashboard, or other
Sinch products (Verification API).

---

## Decision 1: Remove the Contacts resource entirely

**What was cut:** Full CRUD on `/v1/contacts` — create, list, get, update,
delete contacts with channel identifiers and metadata.

**Why:**

- A phone number *is* the contact. E.164 is the universal identifier across
  SMS, WhatsApp, RCS, Viber, and Telegram. You don't need a contact record
  to send a message — you need a phone number.
- Contact management is a CRM concern. Sinch already has a contact database
  in the Conversation API. Duplicating it in a "simple" wrapper creates a
  sync problem and forces developers to manage contacts in two places.
- Every messaging API that developers actually like (Twilio, MessageBird)
  sends to a phone number, not a contact ID. Adding a contact abstraction
  in front of that is solving a problem developers don't have.
- If contacts exist, developers feel obligated to create them before sending,
  which kills the "send a message in one call" value proposition.

**What developers should use instead:** Their own database, their CRM, or
the Sinch Conversation API's contact management if they need channel-specific
identifiers beyond phone numbers.

## Decision 2: Remove "routing" — rename back to "dispatch"

**What was cut:** The term "routing" was renamed back to "dispatch" throughout
the API.

**Why:**

- "Routing" implies server-side intelligence — load balancing, routing tables,
  policy engines. The feature is simpler than that: you give an ordered list
  of channels with a strategy (failover, broadcast, etc.) and we try them.
- "Dispatch" accurately describes what happens: we dispatch your message to
  channel A, and if that fails, dispatch to channel B.
- The industry uses "dispatch" for this pattern. "Routing" sets expectations
  we're not meeting.

## Decision 3: Cut from 26 to 13 operations

**What survived (13 operations):**

| Resource     | Operations                              | Count |
|-------------|------------------------------------------|-------|
| Messages    | send, list, get                          | 3     |
| Channels    | list, get, update                        | 3     |
| Webhooks    | create, list, get, update, delete, test  | 6     |
| Health      | healthCheck                              | 1     |

**What was cut (13 operations):**

| Cut                              | Reason                                                    |
|---------------------------------|-----------------------------------------------------------|
| Contacts CRUD (5 ops)           | Phone number is the contact (Decision 1)                  |
| Batch send                      | Sequential sends are fine for v1; batch adds complexity with partial failure semantics, rate limiting edge cases, and response format debates |
| Schedule message                | Scheduling is a cron job, not an API feature; adds state management (cancel, update, list scheduled) that bloats the surface area |
| Cancel message                  | Messages deliver in seconds; cancel window is near-zero; false sense of control |
| Message analytics / reporting   | Analytics belongs in the Dashboard or a dedicated analytics API, not on the messaging endpoint |
| Routing profiles CRUD           | Removed with routing rename; inline dispatch on each message is sufficient |
| Message search (full-text)      | Search is a different product (Elasticsearch, not a REST endpoint); list with filters covers real needs |
| Template operations             | See Decision 5                                            |

**Principle applied:** If an operation doesn't help you *send a message*,
*receive a message*, or *know what happened to a message*, it doesn't
belong in v1. Everything else is Dashboard, Conversation API, or a
different product.

## Decision 4: SMS-first defaults

**Decision:** SMS is the default channel. If you omit the `channel` field,
your message goes via SMS.

**Why:**

- SMS works on every phone. It's the lowest common denominator and the
  channel developers reach for first when testing.
- It makes the simplest possible request genuinely simple:
  `{ "to": "+15551234567", "body": "Hello" }` — no channel configuration,
  no content type, no dispatch strategy. Two fields.
- Progressive discovery: start with SMS, add `"channel": "whatsapp"` when
  you need it, add `"content": { "type": "card", ... }` when you need rich
  content, add `"dispatch": { ... }` when you need multi-channel delivery.
  Each step adds exactly one concept.

## Decision 5: No `template` content type

**What was cut:** `content.type: template` with `id` and `parameters` fields.

**Why (full rationale in [ADR-001](adr/001-no-template-content-type.md)):**

1. **We don't control the lifecycle.** WhatsApp templates require Meta
   approval (hours to days). We can't create, approve, or validate them.
   Exposing a template content type implies control we don't have.
2. **Channel-specific concept in a channel-agnostic API.** Every other
   content type (text, media, card, carousel, location, list, choices)
   works across channels. Templates are WhatsApp/KakaoTalk only.
3. **The fallover argument is weak.** WhatsApp delivery rates are high.
   When fallback is needed, the SMS message is different content anyway,
   and dispatch routes already support per-route content overrides.
4. **Verification belongs elsewhere.** Most template usage is OTP codes.
   That's a Verification API concern with its own rate limiting and fraud
   detection. See Decision 6.

**Deferred alternative:** If channel-specific features prove necessary,
a `content.type: channel_native` with namespaced payloads
(`"whatsapp": { "template": { ... } }`) keeps the core union clean.

## Decision 6: WhatsApp verification belongs in the Verification API

**Decision:** Add `whatsapp` as a channel option in the Sinch Verification
API rather than supporting WhatsApp authentication templates in MessageRouter.

**Why (full rationale in [ADR-002](adr/002-whatsapp-verification-channel.md)):**

- Verification is a product, not a message type. It includes code generation,
  expiry, rate limiting, fraud scoring, and a verification endpoint. All of
  that exists already for SMS and voice.
- WhatsApp authentication templates follow a fixed Meta format. The
  Verification API can manage one approved template per account/locale
  automatically. Developers never see template IDs.
- Natural cascade: `whatsapp → sms → voice` fallback with one field.

## Decision 7: Flat error responses, no wrapper object

**Decision:** Error responses are flat JSON objects with `code`, `message`,
`status`, and optional `requestId` and `details`. No `{ "error": { ... } }`
wrapper.

**Why:**

- One level of nesting doesn't add information. `response.code` is better
  than `response.error.code`.
- Consistent with the rest of the API's flat-payload philosophy.
- Easier to destructure in every language.

## Decision 8: `body` vs `content` — mutually exclusive, not layered

**Decision:** A message has either a `body` (string shorthand for plain text)
or a `content` (typed polymorphic object). Sending both returns 400.

**Why:**

- If both were allowed, developers would argue about which one takes
  precedence, whether body is a fallback, or whether content overrides body.
- Mutual exclusivity makes the behavior unambiguous. Want plain text? Use
  `body`. Want rich content? Use `content`. Never wonder which one "wins."

## Decision 9: Channel capabilities as a discoverable resource

**Decision:** `GET /v1/channels` and `GET /v1/channels/{channelId}` expose
capabilities (richCards, readReceipts, typingIndicators, media,
maxMessageLength, supportedMediaTypes).

**Why:**

- Developers need to know what a channel supports before sending rich content.
  Rather than failing at send time with a cryptic error, they can query
  capabilities and build adaptive UIs.
- This is progressive discovery applied to infrastructure: you don't need
  to read documentation to know if WhatsApp supports video cards — you ask
  the API.

## Decision 10: Inline dispatch, not routing profiles

**Decision:** Multi-channel dispatch is defined inline on each message via
the `dispatch` object, not via pre-created routing profiles.

**Why:**

- Routing profiles create a named resource that must be managed (CRUD, sync,
  versioning). Inline dispatch is self-contained — the message carries its
  own routing instructions.
- Most developers have one or two dispatch patterns. They don't need a
  profile management system — they need to copy-paste a dispatch block
  into their send call.
- For developers who *do* reuse patterns, that's application-level
  configuration (a constant in their code or a config file), not something
  the API should store.

---

## What the API looks like after the review

**13 operations, 4 resources, 7 content types:**

```
POST   /v1/messages              → Send a message
GET    /v1/messages              → List messages (with filters)
GET    /v1/messages/{id}         → Get message details

GET    /v1/channels              → List channels + capabilities
GET    /v1/channels/{id}         → Get channel details + health
PUT    /v1/channels/{id}         → Update channel config

POST   /v1/webhooks              → Register webhook
GET    /v1/webhooks              → List webhooks
GET    /v1/webhooks/{id}         → Get webhook details
PUT    /v1/webhooks/{id}         → Update webhook
DELETE /v1/webhooks/{id}         → Delete webhook
POST   /v1/webhooks/{id}/test    → Test webhook

GET    /health                   → Health check
```

**Content types:** `text`, `media`, `card`, `carousel`, `location`, `list`,
`choices`

**Dispatch strategies:** `failover`, `broadcast`, `round-robin`,
`cost-optimized`

**Channels:** `sms`, `rcs`, `whatsapp`, `messenger`, `viber`, `telegram`,
`mms`, `line`, `kakaotalk`, `instagram`

---

## Principles established

1. **If we don't control it, we don't expose it.** External dependencies
   (Meta template approval, carrier-specific features) stay behind the
   curtain.
2. **Channel-agnostic or don't ship it.** Content types must work across
   channels or degrade gracefully. Channel-specific features are deferred
   to a future namespaced extension model.
3. **A wrapper must be simpler, not just different.** Every operation must
   justify its existence against "just use the Conversation API directly."
4. **Two-field minimum viable request.** `{ "to": "...", "body": "..." }`
   must work. Everything else is progressive disclosure.
5. **Cut it now, add it later.** It's easier to add an endpoint in v1.1 than
   to deprecate one. When in doubt, cut.

---

## Related ADRs

- [ADR-001: No `template` content type](adr/001-no-template-content-type.md)
- [ADR-002: WhatsApp as a Verification API channel](adr/002-whatsapp-verification-channel.md)
