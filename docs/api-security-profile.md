# API Security Profile

This document is the deployable endpoint inventory and access-control baseline for the repository. Update it whenever an endpoint, scope, public flow, or gateway route changes.

## Company Product API

| Surface | Authentication | Authorization | Abuse and validation controls |
| --- | --- | --- | --- |
| `GET /api/v1/operational/ping` | Anonymous, explicit | None | Global per-IP rate limit; no-store response |
| `GET /health/live` | Anonymous, explicit | None | Minimal liveness output |
| `GET /health/ready` | Anonymous, explicit | None | Minimal readiness output |
| `GET /openapi/v1.json` | Anonymous on the internal API network | None | Gateway inventory; do not route publicly |
| `GET /api/v1/orders` | JWT | `orders.read` scope | Global per-subject limit; bounded pagination |
| `GET /api/v1/orders/{id}` | JWT | `orders.read` scope | Global per-subject limit; GUID route constraint |
| `POST /api/v1/orders` | JWT | `orders.write` scope | 30 requests/minute per subject, 1 MB body limit, strict JSON schema, application validation |

All controller endpoints require authentication through the fallback policy unless they are explicitly marked anonymous. Authentication and authorization failures use ProblemDetails and are logged with correlation ID, subject, route, method, and remote IP.

## Root Intranet API Surface

| Surface | Authentication | Authorization | Abuse and validation controls |
| --- | --- | --- | --- |
| `POST /api/zendesk/reset-password` | Shared webhook secret | Explicit anonymous endpoint | Fixed-time secret comparison, route-specific rate limit, constrained DTO, audit record |
| `GET /api/technews` | Intranet authentication fallback | Authenticated user | Bounded cache and HTTPS-only external article links |
| `POST /security/csp-report` | Explicit anonymous endpoint | None | 16 KB body limit, route-specific rate limit, sanitized logging |

## Ownership Boundary

The current order scopes are service-level permissions and allow access to all orders. Before exposing this API directly to customer identities, define a trusted customer identifier claim and enforce it in list, get, and create handlers. A caller must not be allowed to select another customer's `CustomerId` unless a separate privileged on-behalf-of scope is explicitly designed, audited, and tested.

## Deployment Controls

- Keep `/openapi/v1.json` reachable by the gateway and internal security tooling, but do not publish it through an internet-facing route.
- Keep the API behind the configured gateway, TLS policy, CORS allowlist, and network segmentation.
- Alert on repeated 401, 403, 413, and 429 responses, malformed JSON, Zendesk pagination rejections, and external response-size failures.
- Add tests for every new endpoint covering anonymous access, insufficient scope, correct scope, invalid input, and relevant object ownership.
