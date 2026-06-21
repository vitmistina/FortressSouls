import { useCallback, useEffect, useState } from "react";
import { fetchRuntimeStatus, type RuntimeStatusResult } from "../../api/status";

type RuntimeStatusLoader = (signal?: AbortSignal) => Promise<RuntimeStatusResult>;

export interface RuntimeStatusPanelProps {
  loadRuntimeStatus?: RuntimeStatusLoader;
}

type RuntimeStatusPanelState =
  | { kind: "loading" }
  | { kind: "ready"; result: RuntimeStatusResult }
  | { kind: "error" };

function deriveChipVariant(result: RuntimeStatusResult): "ready" | "degraded" | "not-configured" {
  const { provider, adapter } = result;

  if (!provider.isConfigured || !adapter.isConfigured) {
    return "not-configured";
  }

  const hasError =
    (provider.lastOutcome === "error" || provider.lastOutcome === "timeout") ||
    (adapter.lastOutcome === "error" || adapter.lastOutcome === "timeout");

  return hasError ? "degraded" : "ready";
}

export function RuntimeStatusPanel({ loadRuntimeStatus = fetchRuntimeStatus }: RuntimeStatusPanelProps) {
  const [state, setState] = useState<RuntimeStatusPanelState>({ kind: "loading" });
  const [retryKey, setRetryKey] = useState(0);

  const handleRetry = useCallback(() => {
    setState({ kind: "loading" });
    setRetryKey((k) => k + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    loadRuntimeStatus(controller.signal)
      .then((result) => {
        if (active) {
          setState({ kind: "ready", result });
        }
      })
      .catch(() => {
        if (active) {
          setState({ kind: "error" });
        }
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [loadRuntimeStatus, retryKey]);

  if (state.kind === "loading") {
    return (
      <section className="panel panel--runtime-status" aria-labelledby="runtime-status-heading" aria-busy="true">
        <div className="panel__header">
          <span className="status-chip status-chip--loading">Loading</span>
          <h2 id="runtime-status-heading">Runtime status</h2>
        </div>
        <p className="panel__copy">Checking runtime status...</p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section className="panel panel--runtime-status" aria-labelledby="runtime-status-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--error">Unavailable</span>
          <h2 id="runtime-status-heading">Runtime status</h2>
        </div>
        <p className="panel__copy panel__copy--error" role="alert">
          Runtime status is unavailable right now.
        </p>
        <button type="button" onClick={handleRetry}>
          Retry
        </button>
      </section>
    );
  }

  const { result } = state;
  const variant = deriveChipVariant(result);
  const { provider, adapter } = result;

  const chipClass =
    variant === "ready"
      ? "status-chip--ready"
      : variant === "degraded"
        ? "status-chip--error"
        : "status-chip--pending";

  const chipLabel =
    variant === "ready" ? "Ready" : variant === "degraded" ? "Degraded" : "Not configured";

  const isNotConfigured = variant === "not-configured";

  return (
    <section className="panel panel--runtime-status" aria-labelledby="runtime-status-heading">
      <div className="panel__header">
        <span className={`status-chip ${chipClass}`}>{chipLabel}</span>
        <h2 id="runtime-status-heading">Runtime status</h2>
      </div>

      <dl className="runtime-status-grid">
        <div>
          <dt>Provider</dt>
          <dd>{provider.providerType}</dd>
        </div>
        <div>
          <dt>Model</dt>
          <dd>{provider.model}</dd>
        </div>
        {provider.lastErrorCategory ? (
          <div>
            <dt>Provider error</dt>
            <dd>{provider.lastErrorCategory}</dd>
          </div>
        ) : null}
        <div>
          <dt>Adapter</dt>
          <dd>{adapter.adapterType}</dd>
        </div>
        {adapter.lastErrorCategory ? (
          <div>
            <dt>Adapter error</dt>
            <dd>{adapter.lastErrorCategory}</dd>
          </div>
        ) : null}
      </dl>

      {isNotConfigured ? (
        <p className="panel__copy">
          <a href="/docs/setup" aria-label="View setup guide">
            View setup guide
          </a>
        </p>
      ) : null}
    </section>
  );
}
