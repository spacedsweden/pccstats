# ADR-002: Add WhatsApp as a Verification API channel

**Status:** Proposed
**Date:** 2026-02-20
**Deciders:** API design council + Verification team (Wei)

## Context

WhatsApp supports authentication message templates — pre-approved OTP
delivery formats with one-tap autofill on supported devices. Today,
developers who want to send verification codes via WhatsApp must use the
Sinch Conversation API directly with a pre-approved authentication
template.

Analysis of Conversation API usage shows that ~60% of WhatsApp template
messages are authentication/OTP flows. These customers are using a generic
messaging API for a specialized verification product, without the
protections that a purpose-built verification system provides.

Meanwhile, the Sinch Verification API already handles SMS and voice OTP
delivery with built-in rate limiting, fraud detection, code generation,
expiry management, and code verification.

## Decision

**Add `whatsapp` as a channel option in the Verification API** rather than
supporting WhatsApp authentication templates in the MessageRouter API.

## Rationale

### 1. Verification is a product, not a message

Sending an OTP involves more than delivering a string:

- Code generation and storage
- Expiry management (codes should expire; messages don't)
- Rate limiting per recipient (prevent brute-force)
- Fraud scoring (SIM swap detection, velocity checks)
- Code verification endpoint
- Compliance logging

All of this exists in the Verification API for SMS and voice. WhatsApp is
another delivery rail for the same product. Rebuilding these protections
in the messaging API would duplicate effort and likely miss edge cases
that the Verification team has already handled.

### 2. Template management is handled once, not per-request

WhatsApp authentication templates follow a fixed Meta-defined format. The
Verification API can manage a single approved authentication template per
account/locale and use it automatically. Developers never need to think
about template IDs — they just say "verify this number via WhatsApp."

This is fundamentally different from the messaging API where developers
might need explicit template control for diverse use cases (shipping,
marketing, support).

### 3. Natural fallback cascade

The Verification API already supports channel fallback (try voice if SMS
fails). Adding WhatsApp enables a `whatsapp → sms → voice` cascade with
no additional complexity:

```json
POST /v1/verifications
{
  "to": "+15551234567",
  "channel": "whatsapp",
  "fallback": ["sms", "voice"]
}
```

This is cleaner than building verification-aware dispatch logic into the
messaging API.

### 4. Keeps the messaging API focused

Per ADR-001, the MessageRouter API is a channel-agnostic message delivery
API. Verification is a product concern with its own lifecycle, security
requirements, and compliance obligations. Routing ~60% of current template
traffic to the Verification API removes the primary driver for template
support in the messaging API.

### 5. The messaging API should not be a catch-all

During the design review, multiple features were proposed for the messaging
API that are actually separate products: batch notifications, marketing
campaigns, OTP delivery, analytics. Each of these has specialized
requirements that a generic "send message" endpoint cannot adequately
serve. Verification is the clearest example — it needs fraud detection
and rate limiting that would be inappropriate to enforce on all messages.

## Consequences

- The Verification API gains a `whatsapp` channel option.
- A pre-approved WhatsApp authentication template must be configured per
  account (via Dashboard) before WhatsApp verification works.
- Developers get WhatsApp OTP delivery without learning template concepts.
- The MessageRouter API stays template-free for OTP use cases.
- ~60% of current Conversation API template traffic has a migration path
  to a purpose-built product with proper security controls.
