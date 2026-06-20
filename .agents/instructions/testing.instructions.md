---
description: "Use when writing unit tests, integration tests, test fixtures, or golden file contracts. Covers deterministic testing, boundary testing, fake paths, and regression testing."
applyTo: "**/*.{test,spec}.{ts,cs}"
---

# Testing Instructions

Scope: automated tests, fixtures, and golden files.

- Test observable behavior at the cheapest reliable level.
- Use unit tests for pure logic, integration tests for boundaries, and one fake-path smoke test.
- Keep tests deterministic; no live DFHack or LLM dependency in automated tests.
- Use stable samples and review golden-file diffs as contract changes.
- Add a regression test for each fixed defect.
- Report commands run and explain any skipped validation.
- Behavioral tests are the most important; While unit tests are useful, they are not a substitute for behavioral tests.
