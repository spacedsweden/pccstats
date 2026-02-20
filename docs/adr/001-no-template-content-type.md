# ADR-001: No `template` as a top-level content type

**Status:** Accepted (evolved during review)
**Date:** 2026-02-20
**Deciders:** API design council + Conversation Platform team

## Context

The Sinch Conversation API supports WhatsApp message templates — pre-approved
message structures that Meta requires for business-initiated (outbound)
conversations. The Conversation Platform team's original MessageRouter draft
exposed these as a first-class `content.type: template` alongside `text`,
`card`, `carousel`, etc.

The motivation was real: enterprise customers need to send WhatsApp template
messages with fallback to SMS, and the platform team had direct customer
feedback requesting this in a single API call. ~340 accounts have sent at
least one template message through the Conversation API in the past 12 months.

## Decision

**Replace `content.type: template` with a two-tier approach:**

1. **Template auto-matching (v1 GA):** The WhatsApp channel adapter
   automatically matches standard content types to approved templates.
   Developers send `body` or `content`; the platform selects the right
   template.

2. **`content.type: channel_native` (v1.1):** An explicitly namespaced
   escape hatch for cases where auto-matching is insufficient — complex
   templates with media headers, custom button layouts, or multi-language
   variants.

## Rationale

### 1. The developer's intent is content, not template selection

When a developer sends a shipping notification, their intent is "tell the
customer their order shipped." The fact that WhatsApp requires an approved
template to do this is a platform constraint, not a developer concern. The
channel adapter should handle template matching the same way it handles
character encoding or message segmentation — transparently.

### 2. Channel-specific concepts don't belong in channel-agnostic content types

The other content types (`text`, `media`, `card`, `carousel`, `location`,
`list`, `choices`) work across channels or degrade gracefully. A top-level
`template` type would be the only content type that works on exactly one
channel. This undermines the "channel as a parameter" principle.

### 3. Template lifecycle is not in our control

WhatsApp templates must be submitted to Meta for review (hours to days).
Meta can reject or pause them. The API cannot create, approve, or validate
templates — it can only reference them by ID. Auto-matching moves this
complexity into the channel adapter where the platform team can handle
matching failures gracefully.

### 4. The customer data requires context

Of the ~340 accounts that sent template messages in the past 12 months:
- ~60% were using authentication templates (OTP, 2FA) — these belong in
  the Verification API (see ADR-002)
- ~15-20% were using utility templates (shipping, appointments) — the
  auto-matching approach handles these
- ~5-10% were using complex templates requiring explicit control —
  `channel_native` handles these

The number of accounts needing explicit template control in the messaging
API is closer to 20-35, not 340.

### 5. The failover argument has nuance

WhatsApp delivery rates vary by market:
- Brazil, India: 95%+ delivery
- European markets (privacy settings, opt-in gaps): 70-80%

In markets with lower delivery rates, template-to-SMS failover is
legitimate. But the dispatch `routes` already support per-route `content`
overrides, and with `channel_native` in v1.1, the WhatsApp route can
specify an explicit template while the SMS route uses plain text.

## The `channel_native` design

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

Key properties:
- **Explicitly namespaced** — the channel dependency is visible in the
  payload, not hidden behind a generic `template` type
- **Extensible** — works for KakaoTalk templates, LINE Flex Messages, and
  future channel-specific features without modifying the core content union
- **Opt-in** — developers who never need channel-specific features never
  encounter this content type

## Alternatives considered

### A. Keep `template` as a top-level content type (rejected)

Leaks a single-channel concern into the channel-agnostic content union.
Creates a content type whose validity depends on external state (Meta
approval) that the API cannot query or control.

### B. No channel-specific escape hatch at all (rejected)

Forces developers with complex template needs to use the Conversation API
directly. Acceptable for v1 GA, but leaves ~20-35 accounts without a
migration path. `channel_native` in v1.1 addresses this.

## Consequences

- v1 GA: Developers send standard content types. The WhatsApp channel
  adapter auto-matches to approved templates. No template IDs in the API.
- v1.1: `channel_native` ships for developers who need explicit template
  control. Triggered if auto-matching failure rate exceeds 2%.
- Verification use cases (~60% of template traffic) route to the
  Verification API per ADR-002.
- The core content type union stays channel-agnostic.
