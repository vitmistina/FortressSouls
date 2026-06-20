import { HealthStatusPanel, type HealthStatusPanelProps } from "../features/diagnostics/HealthStatusPanel";
import { ChatPlaceholder } from "../features/chat/ChatPlaceholder";
import { DwarfListPlaceholder } from "../features/dwarves/DwarfListPlaceholder";
import { SelectedDwarfPlaceholder } from "../features/dwarves/SelectedDwarfPlaceholder";

export interface AppProps {
  loadHealth?: HealthStatusPanelProps["loadHealth"];
}

export function App({ loadHealth }: AppProps) {
  return (
    <div className="app-shell">
      <header className="hero">
        <div className="hero__eyebrow">Fortress Souls v0.1</div>
        <h1>Read-only dwarf companion</h1>
        <p>
          Select a dwarf in the browser, inspect the validated snapshot, and chat during the
          current session without mutating the fortress.
        </p>
      </header>

      <main className="content-grid">
        <HealthStatusPanel loadHealth={loadHealth} />

        <section className="panel panel--workflow" aria-labelledby="workflow-heading">
          <div className="panel__header">
            <span className="status-chip status-chip--pending">Waiting</span>
            <h2 id="workflow-heading">Browser workflow</h2>
          </div>
          <div className="workflow-grid">
            <DwarfListPlaceholder />
            <SelectedDwarfPlaceholder />
            <ChatPlaceholder />
          </div>
        </section>
      </main>
    </div>
  );
}
