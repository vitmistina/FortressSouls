# Three-Layer Knowledge Store — Architecture Design Notes

Status: Draft / thinking-through (not an accepted spec, not a backlog item)
Date: 2026-06-22
Relates to: `docs/big-idea.md` §6.7-6.8, §9 (dwarf knowledge model),
`docs/specs/perception-tools-v0.2.md` (`remember`-style tool shape),
`docs/architecture/0001-architecture-overview.md` (read-only, modular monolith).

This document turns the original idea note into a concrete, low-custom-code
architecture. It deliberately splits the system into two halves with a hard
seam between them, because they have completely different constraints:

- An **offline build pipeline** (Python CLIs + an LLM, run occasionally by the
  maintainer) that produces a versioned knowledge artifact.
- An **online query slice** (the existing .NET modular monolith) that the dwarf
  agent calls through a `remember` perception tool.

The artifact between them is the only contract. The runtime never scrapes,
never calls a build tool, and never runs Python. This keeps the read-only,
deterministic-seam, and content-free-telemetry guarantees intact.

---

## 1. Original idea, restated

The maintainer wants to give dwarves a `remember` tool backed by a curated
corpus, built through a medallion pipeline:

1. AI-assisted list of domains/topics (a YAML file).
2. For each domain, identify and fetch sources (wiki, Steam guides, Reddit,
   tutorials). Some sources are always downloaded wholesale (game wiki, Steam
   guides).
3. **Bronze** = raw fetched content.
4. **Silver** = cleaned, readable markdown (boilerplate stripped).
5. **Gold** = tagged, curated, and re-voiced into "dwarven" phrasing (facts a
   dwarf would plausibly express, without citing exact wiki numbers), plus a
   guiding table/YAML so a dwarf agent retrieves only what it would reasonably
   know. Role-filtering is post-MVP.
6. A `remember` query tool. Hits are answered; **misses are logged** as signal
   for what to add next.

The two reframings below shaped every choice that follows.

---

## 2. Two framing decisions

### 2.1 This is a "scripts + files" problem, not a data platform

At thousands of documents, run occasionally, by one person, almost every
workflow platform (Airflow, Dagster, Prefect, Kestra, Argo, MinIO, lakeFS,
OpenLineage) is solving scale, scheduling, and multi-team problems that do not
exist here. A medallion pipeline **is a build graph** (`bronze → silver →
gold`), so the laziest correct orchestrator is a build runner with incremental,
dependency-aware rebuilds.

> Default verdict: prefer a `Taskfile` + folders + one small database. Make any
> heavier tool earn its place.

### 2.2 Build the gold/query slice yourself; do not depend on Kernel Memory

Microsoft **Kernel Memory is an archived, deprecated research project** (NuGet
packages marked legacy, never reached 1.0). It is an excellent _blueprint_ for
the ingest → chunk → embed → store → `Ask`-with-citations shape — and citations
are exactly what a `remember` tool needs — but adopting it means vendoring
frozen, unsupported code. Use it as a reference, not a dependency.

The supported .NET seam is `Microsoft.Extensions.AI` (`IEmbeddingGenerator`,
`IChatClient`, plus `Microsoft.Extensions.VectorData`). The runtime slice is
thin: embed the query, search the vector store, return curated chunks.

---

## 3. The seam: the knowledge artifact

The build pipeline's only output that the runtime consumes is a **versioned,
read-only gold artifact**:

```text
knowledge/gold/<corpusVersion>/
  chunks.parquet        # or chunks.duckdb — curated chunks + metadata
  index.qdrant/         # prebuilt vector collection snapshot (optional)
  manifest.json         # corpusVersion, build date, source provenance summary
  domains.yaml          # the guiding table (domains, tags, epistemic class)
```

Rules for the seam:

- The artifact is **immutable per `corpusVersion`**. The runtime pins a version.
- The runtime treats every field as **untrusted data** (it is generated text)
  and re-applies length bounds and escaping before it reaches the model,
  exactly like perception-tool results in v0.2.
- The artifact carries **provenance** per chunk so attribution can be known.
- No build-time tool, Python process, or network fetch is reachable from the
  runtime. The runtime only opens files / a local vector container.

This seam is what keeps the offline curation freedom (LLM rewriting, scraping)
from leaking into the read-only, deterministic application.

---

## 4. Offline build pipeline (bronze → silver → gold)

Orchestrated by a single `Taskfile` (cross-platform, Windows-friendly). Each
stage is idempotent and skips work whose output already exists, so a failed run
resumes for free. Layers are plain folders; a small SQLite/DuckDB file is the
manifest and lineage log.

```text
knowledge/
  domains.yaml                 # hand+AI authored topic list (input)
  sources.yaml                 # per-domain source list (input)
  Taskfile.yml                 # the whole pipeline as a build graph
  manifest.duckdb              # documents + transitions + provenance
  bronze/<domain>/<docId>.html.gz
  silver/<domain>/<docId>.md
  gold/<corpusVersion>/...     # the artifact from §3
  query-gaps.sqlite            # cache-miss log (written by runtime, read here)
```

### 4.1 Bronze — acquire (raw, untouched)

| Source kind                       | Off-the-shelf approach                                                                                                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Game wiki (MediaWiki)**         | MediaWiki **Action API** / `Special:Export` to pull _wikitext + metadata_ legitimately. Polite: descriptive User-Agent, `maxlag=5`, serial requests. Far better than mirroring HTML. |
| **General web pages / tutorials** | **Trafilatura** (Apache-2.0, Python CLI) — fetches and strips nav/footer/ads in one step. Add **Crawl4AI** only for JS-rendered pages.                                               |
| **Steam guides**                  | ⚠️ No official guides API for full bodies; guide text is author-owned copyright, not CC. Treat as **manual, permissioned, opt-in** input only. Do not auto-scrape.                   |
| **Reddit / forums**               | Manual or API-with-terms; same copyright caution. Low priority for MVP.                                                                                                              |

Bronze stores the raw bytes (gzipped HTML or raw wikitext) plus a manifest row:
`docId, domain, sourceUrl, fetchedAt, contentHash`. Nothing is cleaned
here — bronze is the audit trail and the re-runnable source of truth.

### 4.2 Silver — clean to markdown (format processing only)

| Input                  | Tool                                                                                                                                                                    |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Wikitext**           | **Pandoc** `-f mediawiki -t gfm` (understands wikitext natively). ⚠️ GPL/external binary — invoked as a CLI subprocess, never linked.                                   |
| **Scraped HTML**       | **Trafilatura** markdown output (one tool covers acquire + clean).                                                                                                      |
| **Pure-.NET fallback** | If a 100%-.NET silver step is ever wanted: a Readability/Trafilatura extraction then **ReverseMarkdown** (MIT NuGet). Not needed if Python is acceptable for the build. |

Silver is _only_ format normalization plus boilerplate removal. No semantics, no
re-voicing. Output is one `.md` per document, diffable in git. Determinism note:
rule-based extraction is reproducible; avoid OCR/VLM steps that are not.

### 4.3 Gold — curate, chunk, tag, re-voice

This is the only stage with an LLM in the loop. For each silver doc:

1. **Chunk** with `docling-core[chunking]` (heading/structure-aware) or
   `langchain-text-splitters` (simplest). Deterministic, rule-based.
2. **Tag** each chunk against `domains.yaml`: domain, topic tags, and an
   **epistemic category** drawn from the `big-idea.md` §6.8 taxonomy
   (`CommonKnowledge`, `ProfessionalKnowledge`, `RolePrivileged`, `CraftLore`,
   `MilitaryTraining`, …). This is the hook for future role-filtering.
3. **Re-voice** into "dwarven" phrasing with a local LLM via **Ollama**
   (looped by the Taskfile, idempotent skip-if-output-exists). The prompt turns
   "Plump helmets grow in 5 days underground" into something a dwarf would say
   without citing exact wiki numbers. Keep the original chunk as provenance.
4. **Embed** each gold chunk with the _same_ embedder used at query time
   (`nomic-embed-text` via Ollama, or in-process ONNX `bge-small`). Critical:
   one model for both ingest and query.
5. **Write** chunks + metadata to `chunks.parquet`/`chunks.duckdb`, and upsert
   vectors into the gold artifact's vector collection.

`domains.yaml` is the "guiding table" from the original idea: it is curated by
hand (with AI help), is the source of valid tags/categories, and later drives
deterministic role-filtering (which categories a given dwarf role may retrieve).

### 4.4 LLM batch step

No agent framework. The Taskfile loops silver/gold files and POSTs each prompt
to a local **Ollama** endpoint; per-file idempotency gives free resume. Use a
cloud **batch API** only if local model quality for the re-voicing is
insufficient and the offline constraint is consciously relaxed for that step.

---

## 5. Online query slice (the `remember` tool)

This lives inside the existing modular monolith and follows the v0.2 perception
tool contract exactly. It is a new application-owned, typed, read-only tool.

```text
IDwarfAgent turn
  -> remember tool call (typed args: { query, optional topic hint })
    -> Knowledge module (application-owned)
       1. embed query (same model as build)        [Microsoft.Extensions.AI]
       2. hybrid search gold collection             [Qdrant.Client, dense+sparse]
       3. (post-MVP) filter by dwarf role/epistemic category
       4. bound + normalize results, attach provenance
    -> typed KnowledgeResult { items[], schemaVersion, warnings[] }
       OR typed "miss" observation
```

### 5.1 Module placement (modular-monolith boundaries)

- **Domain**: `KnowledgeItem`, `KnowledgeQuery`, `EpistemicCategory`,
  `CorpusVersion` — pure concepts, no infrastructure.
- **Knowledge adapter module** (mirrors the DwarfFortress adapter pattern):
  owns the vector store client and the gold artifact reader. Provider DTOs
  (Qdrant types) stay inside it. Maps to application-owned `KnowledgeResult`.
- **Application/Chat**: the `remember` tool registration and the turn-level
  authorization (which dwarf may ask, role filter), execution budget, and
  failure mapping — reusing the v0.2 execution-policy machinery.
- **Embedding** is an `IEmbeddingGenerator` port; fake in tests, real (Ollama
  or ONNX) in dev. Same boundary style as `IChatProvider`.

### 5.2 Vector store

**Qdrant**, single Docker container, official current `Qdrant.Client` .NET SDK,
native hybrid (dense + sparse) search. Alternative if Postgres is ever already
present: **pgvector** (hybrid is hand-written SQL). Avoid LanceDB (no .NET) and
Milvus (abandoned .NET client). For tests, an in-memory fake knowledge adapter
returns fixture chunks — no container needed in CI.

### 5.3 Safety and contract conformance

The `remember` tool obeys the same rules as `look_around` / `inspect_stocks`:

- Typed args validated before any work; query is data, never executable.
- Read-only; no write path exists anywhere in the chain.
- Per-call and per-turn output budgets; results bounded and length-capped.
- Stable failure categories (`unavailable`, `invalid_arguments`, `not_found`,
  `timed_out`, `invalid_data`, `result_too_large`, `budget_exhausted`).
- A "no relevant knowledge" outcome is a typed observation so the dwarf can
  express not-knowing — and is recorded as a cache miss (§6).
- Telemetry is content-free: log `corpusVersion`, result count, max score band,
  duration, outcome — never query text, chunk text, or source URLs as labels.

---

## 6. Cache-miss / retrieval-gap logging

The signal the maintainer wants — "what knowledge should I add next" — is just
an append-mostly log written by the runtime and analyzed in batch.

- Runtime appends a row when a `remember` call returns nothing useful (below a
  relevance threshold or empty): `{ timestamp, corpusVersion, normalizedQuery
or queryCategory, topScore, resultCount }`.
- Store: a **SQLite `query_gaps` table** (upsert with a count for dedup) or an
  append-only **JSONL** file. Both are .NET-trivial.
- Analyze in batch with **DuckDB** ("most frequent unanswered queries this
  period") to prioritize the next `sources.yaml` additions.
- Observability rules apply: keep fields low-cardinality; avoid storing raw
  sensitive query text beyond what the maintainer needs for curation.

This closes the loop: misses feed the next bronze acquisition cycle.

---

## 7. Recommended minimal stack

| Concern                | Pick                                                           | Notes                                                       |
| ---------------------- | -------------------------------------------------------------- | ----------------------------------------------------------- |
| Build orchestration    | **Taskfile** (or Make)                                         | Medallion = build graph; incremental + idempotent.          |
| Bronze acquire         | **MediaWiki API/Export** + **Trafilatura**                     | Polite, legitimate; Steam guides manual/permissioned only.  |
| Silver → markdown      | **Pandoc** (wikitext) + **Trafilatura** (HTML)                 | ReverseMarkdown if a pure-.NET step is ever wanted.         |
| Gold chunk             | **docling-core[chunking]** or **langchain-text-splitters**     | Deterministic, lightweight.                                 |
| Gold re-voice / curate | **Ollama** local LLM, looped by Taskfile                       | Idempotent; cloud batch only if quality forces it.          |
| Embeddings             | **Ollama `nomic-embed-text`** or in-process **ONNX bge-small** | Same model for build and query. `IEmbeddingGenerator` port. |
| Layer storage          | folders (bronze/silver) + **Parquet/DuckDB** (gold)            | Files are files; no MinIO/object store.                     |
| Manifest / provenance  | **DuckDB or SQLite** (`documents` + `transitions`)             | One file = full lineage.                                    |
| Vector store + query   | **Qdrant** container + **Qdrant.Client**                       | Native hybrid; in-memory fake in CI.                        |
| .NET runtime seam      | **Microsoft.Extensions.AI** (+ `VectorData`)                   | Kernel Memory as blueprint only, not dependency.            |
| Cache-miss log         | **SQLite `query_gaps`** / JSONL, analyzed in **DuckDB**        | Drives the next acquisition cycle.                          |

**Hard no for this scale:** Airflow, Dagster, Prefect, Kestra, Mage, Argo,
Luigi, MinIO, lakeFS, OpenLineage, LM Studio in-pipeline, LangChain/LlamaIndex
as full frameworks, and depending on Kernel Memory. Bookmark Dagster/Prefect
only as a future escape hatch if this ever stops being a solo batch job.

---

## 8. MVP slice vs later

**MVP (smallest useful loop):**

1. `domains.yaml` + `sources.yaml` for one or two domains (e.g. farming, moods).
2. Bronze (wiki API) → Silver (Pandoc) → Gold (chunk + embed) for that slice;
   re-voicing can start as a single simple prompt.
3. Qdrant container + a `remember` tool returning curated chunks with
   provenance, behind the v0.2 agent-turn and execution-policy machinery.
4. Cache-miss logging from day one (it is nearly free and is the whole point).

**Deferred (explicitly post-MVP):**

- Role/epistemic filtering (the `domains.yaml` category hook exists from the
  start, but enforcement comes later, aligned with `big-idea.md` §6.8).
- Leakage prevention (a bookkeeper's stock records must not reach a farmer).
- Steam/Reddit corpora (copyright + acquisition complexity).
- Confidence scoring / reviewed-vs-unreviewed knowledge marking.

---

## 9. Open questions to resolve before this becomes a backlog item

1. **Where does the `remember` tool sit relative to v0.2?** It is a fifth
   perception tool in shape but reads a built corpus, not live DFHack. Likely a
   v0.3 concern; confirm and create `docs/specs/fortress-souls-v0.3.spec.md`
   plus an ADR before implementation.
2. **Embedding model + dimensions pinned** — choose one and record it; changing
   it later forces a full re-embed and a new `corpusVersion`.
3. **Artifact distribution** — is the gold artifact committed (git-lfs),
   built locally by each user, or shipped as a release asset?

These need a human decision and an ADR; they change distribution, safety, and
scope, so they are stop-and-ask items per `AGENTS.md`.
