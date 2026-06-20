import { useEffect, useState } from "react";
import { fetchHealth, type HealthResult } from "../../api/health";

type HealthLoader = (signal?: AbortSignal) => Promise<HealthResult>;

export interface HealthStatusPanelProps {
  loadHealth?: HealthLoader;
}

type HealthPanelState =
  | { kind: "loading" }
  | { kind: "ready"; result: HealthResult }
  | { kind: "error" };

export function HealthStatusPanel({ loadHealth = fetchHealth }: HealthStatusPanelProps) {
  const [state, setState] = useState<HealthPanelState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    loadHealth(controller.signal)
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
  }, [loadHealth]);

  if (state.kind === "loading") {
    return (
      <section className="panel panel--health" aria-labelledby="health-heading" aria-busy="true">
        <div className="panel__header">
          <span className="status-chip status-chip--loading">Loading</span>
          <h2 id="health-heading">Backend health</h2>
        </div>
        <p className="panel__copy">Checking backend health...</p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section className="panel panel--health" aria-labelledby="health-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--error">Unavailable</span>
          <h2 id="health-heading">Backend health</h2>
        </div>
        <p className="panel__copy panel__copy--error" role="alert">
          Backend health is unavailable right now.
        </p>
      </section>
    );
  }

  const { health, correlationId } = state.result;

  return (
    <section className="panel panel--health" aria-labelledby="health-heading">
      <div className="panel__header">
        <span className="status-chip status-chip--ready">Ready</span>
        <h2 id="health-heading">Backend health</h2>
      </div>
      <dl className="health-grid">
        <div>
          <dt>Status</dt>
          <dd>{health.status}</dd>
        </div>
        <div>
          <dt>Version</dt>
          <dd>{health.version}</dd>
        </div>
        <div>
          <dt>Adapter</dt>
          <dd>{health.adapter}</dd>
        </div>
        <div>
          <dt>Provider</dt>
          <dd>{health.provider}</dd>
        </div>
        <div>
          <dt>Observability</dt>
          <dd>{health.observability}</dd>
        </div>
        {correlationId ? (
          <div>
            <dt>Correlation ID</dt>
            <dd>
              <code>{correlationId}</code>
            </dd>
          </div>
        ) : null}
      </dl>
    </section>
  );
}
