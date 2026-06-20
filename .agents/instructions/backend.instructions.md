---
description: "Use when writing ASP.NET Core APIs, backend services, or domain models in C#. Covers modular monolith boundaries, endpoint orchestration, validation, testing, and avoiding HTTP/provider concerns in domain code."
applyTo: "src/backend/**"
---

# Backend Instructions

Scope: `.NET` backend and API work under `src/backend`.

- Preserve modular-monolith boundaries; domain code has no HTTP, DFHack, or provider DTOs.
- Keep endpoints thin and orchestration in application modules.
- Validate external input and use cancellation for I/O.
- Prefer built-in ASP.NET Core features over new dependencies.
- Add focused unit or integration tests for changed behavior.
- Run the narrowest relevant `dotnet test` and formatting check.
