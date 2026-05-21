# 0004 API Idempotency Request Hash

## Status

Accepted.

## Context

M2 needs retry-safe mutating endpoints before agents depend on event and memory proposal writes. The schema already stores idempotency records by principal, endpoint, idempotency key, request hash, response, status, and expiry.

The implementation needs a deterministic request fingerprint rule that is simple enough for the first `POST /api/events` and `POST /api/memory/proposals` endpoints, while preserving a conservative conflict boundary.

## Decision

Mutating API endpoints use the `Idempotency-Key` request header.

The idempotency scope is:

- authenticated principal id
- explicit endpoint id, such as `POST /api/events`
- idempotency key

The request hash is the lowercase hex SHA-256 digest of the exact request body bytes after authentication and before endpoint handling. Stored values use the `sha256:` prefix.

For M2, query string values, headers other than the idempotency key, and semantic JSON normalization are not part of the hash. Two JSON bodies that mean the same thing but differ by whitespace, property order, or formatting are treated as different requests when reused with the same idempotency key.

## Runtime Behavior

- A new key inserts a `processing` idempotency record before endpoint side effects.
- A completed retry with the same principal, endpoint, key, and request hash returns the stored response.
- Reusing the same principal, endpoint, and key with a different request hash returns `409 Conflict`.
- A concurrent retry while the first request is still `processing` returns `409 Conflict`.
- Completed and failed records expire after the configured retention period; the default retention period is 24 hours.

## Consequences

- Clients should retry with the same idempotency key and exact same request body bytes.
- The implementation is conservative: formatting-only JSON differences conflict rather than being treated as equivalent.
- Future endpoints can introduce canonical JSON hashing with a new decision if clients need semantic equivalence across differently formatted payloads.
