# Company.Product Architecture Blueprint (Implemented Baseline)

## Delivery Model
- Modular monolith with explicit seams for future extraction.
- Clean Architecture dependency direction:
  - `Api -> Application -> Domain`
  - `Infrastructure -> Application + Domain`
  - `Domain` references no outer layer.

## Implemented Projects
- `src/Company.Product.Domain`: aggregates, invariants, domain exceptions.
- `src/Company.Product.Application`: use-case handlers, abstractions, pagination/read models.
- `src/Company.Product.Infrastructure`: EF Core SQL Server persistence, repository/read services, unit of work, clock.
- `src/Company.Product.Api`: HTTP delivery, auth policies, middleware pipeline, OpenAPI.
- `src/Company.Product.WorkerService`: worker host using same application/infrastructure composition.
- `src/Company.Product.Gateway`: edge policy configuration sample.

## Implemented Security Layers
- JWT bearer authentication in API.
- Policy-based authorization (`orders.read`, `orders.write`).
- Correlation IDs on every request.
- Global exception middleware returning RFC7807 Problem Details with error codes.
- Health endpoints split for liveness and readiness.

## Data Access Baseline
- EF Core SQL Server configured in infrastructure only.
- `DbContext` scoped lifetime.
- `AsNoTracking` query projections for read paths.
- Pagination on list reads (`page`, `pageSize`).
- Concurrency token property (`RowVersion`) included on aggregate.

## Testing Baseline
- Domain unit tests: invariant and behavior checks.
- Application unit tests: use-case orchestration and validation.
- Infrastructure tests: EF-backed query behavior.
- API integration tests: endpoint behavior and auth guard.
- Contract tests: OpenAPI document route/shape smoke checks.

## Guardrails
- Controllers must not contain business rules or direct persistence logic.
- Mutations must go through application use-case handlers.
- API contracts are DTOs from `Contracts` project, never EF entities.
- New endpoints require OpenAPI exposure and tests.
- Never log secrets, tokens, or raw sensitive payloads.
