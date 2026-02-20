# MessageRouter API — Design Review Decisions

**Date:** 2026-02-20
**Participants:** API design council, Conversation Platform team
**API:** Sinch MessageRouter API (wrapper around Sinch Conversation API)
**Built by:** Conversation Platform team (Lars, Annika, Ravi, and team)

---

## Executive Summary

The Conversation Platform team built the MessageRouter API to solve a real
problem: the Sinch Conversation API has 47 endpoints and requires six
setup steps before a developer can send a single message. MessageRouter
reduces that to one call with two fields.

During the design review, the council and the platform team collaborated
to refine the API surface — reducing from 26 operations to 13, sharpening
the boundary between this API and other Sinch products (Verification API,
Dashboard, Conversation API), and evolving the addressing model to support
non-phone channels correctly.

The original team's work directly shaped the final design. The multi-channel
addressing problem identified by the platform team (Ravi's channel identifier
mapping work) became the `channel:identifier` addressing format. The inline
dispatch model (Annika's dispatch orchestrator) replaced routing profiles
as the cleaner design — a conclusion the platform team themselves endorsed.

---

## Decision 1: Contacts resource removed; extensible addressing in `to`

**What changed:** Full CRUD on `/v1/contacts` was removed. The `to` field
was extended to accept both E.164 phone numbers and channel-scoped identifiers.

**Background:** The Conversation Platform team built Contacts to solve a
real problem — not every channel is addressed by phone number. Messenger
uses PSIDs, Instagram uses IGSIDs, LINE uses User IDs. The original contact
model mapped these channel-specific identifiers to a unified resource.

**Why the resource was removed but the problem was preserved:**

- A full Contacts CRUD resource forces developers to create contacts before
  sending, which breaks the "send in one call" promise. But the underlying
  addressing problem is real and was correctly identified by the platform team.
- The solution: extend the `to` field to accept `channel:identifier` format
  (e.g., `messenger:PSID_123`, `instagram:IGSID_456`) alongside E.164 phone
  numbers. Strings starting with `+` are phone numbers; strings containing
  `:` are channel-scoped identifiers.
- Contact management (metadata, preferences, history) remains a CRM concern
  handled by the developer's own database or the Sinch Conversation API's
  contact store.

**Format:**
```
+15551234567                    → E.164 phone number (SMS, WhatsApp, RCS, etc.)
messenger:1234567890123456      → Messenger Page-Scoped ID
instagram:5678901234567890      → Instagram-Scoped ID
line:U1234567890abcdef          → LINE User ID
kakaotalk:abcdef1234567890      → KakaoTalk User ID
```

## Decision 2: "Routing" renamed to "dispatch"

**What changed:** The term "routing" was renamed to "dispatch" throughout.

**Why:**

- "Routing" implies server-side intelligence (load balancing, routing tables,
  policy engines). The feature is more straightforward: an ordered list of
  channels with a strategy.
- "Dispatch" accurately describes the behavior and matches industry usage.

## Decision 3: 26 operations refined to 13

**What survived (13 operations):**

| Resource     | Operations                              | Count |
|-------------|------------------------------------------|-------|
| Messages    | send, list, get                          | 3     |
| Channels    | list, get, update                        | 3     |
| Webhooks    | create, list, get, update, delete, test  | 6     |
| Health      | healthCheck                              | 1     |

**What was removed and why:**

| Removed                          | Reason                                                    |
|---------------------------------|-----------------------------------------------------------|
| Contacts CRUD (5 ops)           | Replaced with extensible `to` field addressing (Decision 1) |
| Batch send                      | The messaging API is for delivering individual messages to customers. High-volume senders (notifications, marketing, OTP) are product-level concerns handled by dedicated APIs. Sequential sends with HTTP/2 handle transactional workloads. |
| Schedule message                | Adds state management surface (cancel, update, list scheduled) disproportionate to the value. Scheduling is application-level logic. Future consideration. |
| Cancel message                  | Near-zero cancel window for most channels. TTL handles expiry for queued messages. Future consideration for channels with long queue times. |
| Message analytics / reporting   | Analytics belongs in the Dashboard or a dedicated analytics API. |
| Routing profiles CRUD           | Inline dispatch is the better design (endorsed by the platform team). Messages carry their own dispatch instructions. |
| Message search (full-text)      | Full-text search is a different product. List with filters covers operational needs. |
| Template operations             | See Decision 5. |

**Principle applied:** If an operation doesn't help you *send a message*,
*receive a message*, or *know what happened to a message*, it doesn't
belong in v1.

**Future considerations:** Batch send, schedule, and cancel are acknowledged
as legitimate future needs. Batch requires clear product scoping (is this
notifications? marketing? transactional?) to avoid the API becoming a
catch-all. Schedule requires state management design (cancel, update, list).
Cancel needs analysis of actual cancel-window duration by channel.

## Decision 4: SMS-first defaults

**Decision:** SMS is the default channel. Omitting `channel` sends via SMS.

**Why:**

- SMS works on every phone — lowest common denominator, first channel
  developers test with.
- Enables the simplest possible request:
  `{ "to": "+15551234567", "body": "Hello" }` — two fields, no configuration.
- Progressive discovery: SMS → add channel → add content type → add dispatch.
  Each step introduces exactly one new concept.

## Decision 5: Templates handled by auto-matching; `channel_native` for edge cases

**What changed:** `content.type: template` was removed as a peer of `text`,
`card`, etc. In its place:

1. **Template auto-matching (v1 GA):** The WhatsApp channel adapter
   automatically matches standard content types (`body`, `text`, `card`,
   etc.) to the customer's approved WhatsApp templates. Developers send
   content; the platform handles template selection.

2. **`content.type: channel_native` (v1.1):** An explicitly namespaced
   escape hatch for channel-specific features that can't be expressed
   through standard content types. WhatsApp templates with complex media
   headers, custom button layouts, or multi-language variants use this path.

**Background:** The platform team correctly identified that WhatsApp
requires approved templates for business-initiated conversations. The
original design exposed template IDs directly. The review evolved this
into a two-tier approach: auto-matching for the common case (utility
templates for shipping, appointments, etc.), explicit `channel_native`
for the advanced case.

**Why auto-matching is the right default:**

- The developer's intent is "send a shipping update," not "use template
  `order_v2` with parameters." The platform should handle the mapping.
- Template lifecycle (approval, pausing, rejection) is managed by Meta,
  not by the developer at send time. Auto-matching keeps this complexity
  in the channel adapter where it belongs.
- When auto-matching fails (no matching approved template), the error is
  clear: "No approved template matches this content. Configure templates
  at dashboard.sinch.com."

**Why `channel_native` is namespaced, not a top-level content type:**

```json
{
  "channel": "whatsapp",
  "content": {
    "type": "channel_native",
    "whatsapp": {
      "template": {
        "id": "order_confirmation_v2",
        "language": "en",
        "parameters": { "name": "Jane", "order_id": "ORD-123" }
      }
    }
  }
}
```

- The channel dependency is explicit — it's obvious this only works on
  WhatsApp.
- The core content type union (`text`, `media`, `card`, etc.) stays
  channel-agnostic.
- Extensible to other channels (KakaoTalk templates, LINE Flex Messages)
  without polluting the shared content model.

**SLA commitment:** If template auto-matching failure rate exceeds 2% in
production, `channel_native` is escalated from v1.1 to a v1 GA patch.

*Full rationale: [ADR-001](001-no-template-content-type.md)*

## Decision 6: WhatsApp verification belongs in the Verification API

**Decision:** Add `whatsapp` as a channel option in the Sinch Verification
API. OTP and 2FA via WhatsApp are verification product concerns, not
messaging API concerns.

**Why:**

- Verification includes code generation, expiry, rate limiting per recipient,
  fraud scoring, and a verify endpoint. All of this exists for SMS and voice
  already. WhatsApp is another delivery rail for the same product.
- WhatsApp authentication templates follow a fixed Meta format. The
  Verification API manages one approved template per account/locale
  automatically.
- Natural cascade: `whatsapp → sms → voice` with one field.
- Data from the platform team confirms ~60% of WhatsApp template usage
  through the Conversation API is authentication/OTP. These customers
  should be on the Verification API with proper verification semantics.

**Impact:** This means the messaging API does not need to handle the
OTP/2FA use case, which was a significant driver for template support.
The API stays focused on its mission: delivering messages, not managing
verification flows.

*Full rationale: [ADR-002](002-whatsapp-verification-channel.md)*

## Decision 7: Flat error responses

**Decision:** Errors are flat JSON: `code`, `message`, `status`, optional
`requestId` and `details`. No `{ "error": { ... } }` wrapper.

**Why:**

- `response.code` is better than `response.error.code`.
- Consistent with the flat-payload philosophy across the API.

## Decision 8: `body` vs `content` — mutually exclusive

**Decision:** A message has either `body` (string shorthand) or `content`
(typed polymorphic object). Sending both returns 400.

**Why:**

- Eliminates ambiguity about precedence. No "which one wins" questions.

## Decision 9: Channel capabilities as a discoverable resource

**Decision:** `GET /v1/channels` and `GET /v1/channels/{channelId}` expose
capabilities (richCards, readReceipts, typingIndicators, media,
maxMessageLength, supportedMediaTypes).

**Why:**

- Developers query capabilities before sending rich content rather than
  failing at send time.
- Progressive discovery applied to infrastructure.

## Decision 10: Inline dispatch, not routing profiles

**Decision:** Multi-channel dispatch is defined inline on each message via
the `dispatch` object, not via pre-created routing profiles.

**Why (endorsed by the platform team):**

- Inline dispatch is self-contained — the message carries its own
  dispatch instructions.
- Routing profiles create a named resource with CRUD overhead
  disproportionate to the value.
- Developers who reuse patterns store them application-side (constants,
  config files), not API-side.

---

## What the API looks like after the review

**13 operations, 4 resources, 8 content types:**

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
`choices`, `channel_native` (v1.1)

**Dispatch strategies:** `failover`, `broadcast`, `round-robin`,
`cost-optimized`

**Channels:** `sms`, `rcs`, `whatsapp`, `messenger`, `viber`, `telegram`,
`mms`, `line`, `kakaotalk`, `instagram`

**Addressing:** E.164 phone numbers or `channel:identifier` for
platform-addressed channels

---

## Principles established

1. **If we don't control it, we don't expose it.** External dependencies
   (Meta template approval, carrier-specific features) are handled by the
   platform, not exposed to the developer.
2. **Channel-agnostic by default, channel-specific by opt-in.** Content
   types work across channels. Channel-specific features use the
   `channel_native` escape hatch with explicit namespacing.
3. **A wrapper must be simpler, not just different.** Every operation must
   justify its existence against "just use the Conversation API directly."
4. **Two-field minimum viable request.** `{ "to": "...", "body": "..." }`
   must work. Everything else is progressive disclosure.
5. **Ship small, learn, expand.** It's easier to add an endpoint in v1.1
   than to deprecate one. When in doubt, omit — but document the rationale
   and revisit with data.
6. **Don't be a catch-all.** Verification is a product. Marketing
   automation is a product. Analytics is a product. This API delivers
   messages. Features that belong in other products stay in other products.

---

## Future considerations

| Feature | Prerequisites | Trigger |
|---------|--------------|---------|
| `channel_native` in GA | Auto-matching failure rate >2% | Production metrics |
| Batch send | Clear product scoping (what product owns bulk send?) | Product decision |
| Schedule message | State management design (cancel, update, list) | Customer demand with data |
| Cancel message | Per-channel cancel-window analysis | Customer demand with data |

---

## Related ADRs

- [ADR-001: No `template` content type](001-no-template-content-type.md)
- [ADR-002: WhatsApp as a Verification API channel](002-whatsapp-verification-channel.md)
