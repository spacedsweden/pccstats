# ADR-001: No `template` content type in the messaging API

**Status:** Accepted
**Date:** 2026-02-20
**Deciders:** API design team

## Context

The Sinch Conversation API supports WhatsApp message templates — pre-approved
message structures that Meta requires for business-initiated (outbound)
conversations. An earlier draft of the MessageRouter API exposed these as a
first-class `content.type: template` alongside `text`, `card`, `carousel`, etc.

The original motivation was to let developers compose a dispatch with failover
from WhatsApp (using a template) to SMS, all in one call:

```json
{
  "to": "+15551234567",
  "dispatch": {
    "strategy": "failover",
    "routes": [
      {
        "channel": "whatsapp",
        "content": { "type": "template", "id": "order_v2", "parameters": { "name": "Jane" } },
        "ttl": 30
      },
      { "channel": "sms" }
    ]
  }
}
```

## Decision

**Remove `content.type: template` from the API.** Templates are a
channel-specific concern managed outside our API boundary.

## Rationale

### 1. We don't control the lifecycle

WhatsApp templates must be submitted to Meta for review. Approval takes hours
to days. Meta can reject or pause templates at any time. Our API cannot create,
approve, or manage them — it can only reference them by ID. Exposing a
`template` content type implies a level of control we don't have. If a
developer sends `content.type: template` with an unapproved or paused template
ID, the failure comes from Meta, not from us, and we have no way to validate
it ahead of time.

### 2. Channel-specific concepts don't belong in channel-agnostic content types

The MessageRouter API's value proposition is channel abstraction. The other
content types (`text`, `media`, `card`, `carousel`, `location`, `list`,
`choices`) work across channels or degrade gracefully. Templates are
WhatsApp-only (and KakaoTalk, but same argument applies). Mixing
channel-specific features into the polymorphic `content` union undermines the
"channel as a parameter" principle.

### 3. The failover argument is weaker than it looks

The scenario — "send a WhatsApp template, fall back to SMS" — sounds
compelling, but:

- If you have a user's WhatsApp number and they've opted in, delivery rates
  are very high. WhatsApp delivery failures are rare enough that most
  developers don't need a programmatic fallback in the same API call.
- When fallback *is* needed, the fallback message is almost always a
  different message entirely (a plain text SMS), not a template. The dispatch
  `routes` already support per-route `content` overrides, so you can send
  different content on SMS without needing the WhatsApp route to use
  `content.type: template`.
- Developers who genuinely need template-to-SMS failover at scale are
  sophisticated enough to use the Sinch Conversation API directly or configure
  this in the Dashboard.

### 4. Verification use cases belong in the Verification API

A large percentage of "template" usage is for OTP/verification codes.
WhatsApp supports authentication templates specifically for this. But
verification is a distinct product concern with its own delivery logic,
rate limiting, and fraud detection. These should be handled by adding
WhatsApp as a channel option in the Verification API (see ADR-002), not
by cramming template IDs into the messaging API.

## Alternatives considered

### A. Keep `template` as a content type (rejected)

Leaks channel internals into the channel-agnostic API. Creates a content type
whose validity depends on external state we can't query or control.

### B. Channel-specific content extensions via progressive discovery (deferred)

If demand emerges for channel-specific rich features (WhatsApp templates,
RCS suggested actions, Viber-specific cards), we could introduce a
channel-scoped extension model:

```json
{
  "channel": "whatsapp",
  "content": {
    "type": "channel_native",
    "whatsapp": {
      "template": { "id": "order_v2", "parameters": { "name": "Jane" } }
    }
  }
}
```

This keeps channel-specific concerns explicitly namespaced and doesn't pollute
the core content type union. We're deferring this until we see real demand.
The Sinch Conversation API remains available for developers who need direct
template control today.

## Consequences

- Developers who need WhatsApp templates use the Sinch Conversation API
  directly or manage templates through the Dashboard.
- The MessageRouter content type union stays clean and channel-agnostic.
- If we later add WhatsApp as a Verification API channel, OTP templates
  are handled there with proper verification semantics.
- If progressive discovery for channel-native content proves necessary,
  we'll address it in a future ADR with explicit namespacing.
