# ADR-002: Add WhatsApp as a Verification API channel

**Status:** Proposed
**Date:** 2026-02-20
**Deciders:** API design team

## Context

WhatsApp supports authentication message templates — pre-approved OTP
delivery formats with one-tap autofill on supported devices. Today,
developers who want to send verification codes via WhatsApp must use the
Sinch Conversation API directly with a pre-approved authentication
template, bypassing our API entirely.

Meanwhile, our Verification API already handles SMS and voice OTP delivery
with built-in rate limiting, fraud detection, code generation, and
verification logic.

## Decision

**Add `whatsapp` as a channel option in the Verification API** rather than
exposing WhatsApp authentication templates in the messaging API.

## Rationale

### 1. Verification is a product, not a message

Sending an OTP involves more than delivering a string:

- Code generation and storage
- Expiry management
- Rate limiting per recipient
- Fraud scoring (SIM swap detection, velocity checks)
- Code verification endpoint

All of this already exists in the Verification API for SMS and voice.
WhatsApp is just another delivery rail for the same product.

### 2. Template management is handled once, not per-request

WhatsApp authentication templates follow a fixed Meta-defined format. The
Verification API can manage a single approved authentication template per
account/locale and use it automatically. Developers never need to think
about template IDs — they just say "verify this number via WhatsApp."

### 3. Natural failover

The Verification API already supports channel fallback (try voice if SMS
fails). Adding WhatsApp gives developers a `whatsapp → sms → voice`
cascade with no additional API complexity:

```json
POST /v1/verifications
{
  "to": "+15551234567",
  "channel": "whatsapp",
  "fallback": ["sms", "voice"]
}
```

### 4. Keeps the messaging API clean

Per ADR-001, we don't want channel-specific template concepts in the
messaging API. Routing WhatsApp OTP through the Verification API avoids
this entirely.

## Consequences

- The Verification API gains a `whatsapp` channel option.
- A pre-approved WhatsApp authentication template must be configured per
  account (via Dashboard) before WhatsApp verification works.
- Developers get WhatsApp OTP delivery without learning template concepts.
- The messaging API remains template-free.
