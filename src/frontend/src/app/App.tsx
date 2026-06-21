import { HealthStatusPanel, type HealthStatusPanelProps } from "../features/diagnostics/HealthStatusPanel";
import { RuntimeStatusPanel, type RuntimeStatusPanelProps } from "../features/diagnostics/RuntimeStatusPanel";
import { DwarfSelectionPanels, type DwarfSelectionPanelsProps } from "../features/dwarves/DwarfSelectionPanels";

export interface AppProps {
  loadHealth?: HealthStatusPanelProps["loadHealth"];
  loadRuntimeStatus?: RuntimeStatusPanelProps["loadRuntimeStatus"];
  loadDwarfList?: DwarfSelectionPanelsProps["loadDwarfList"];
  loadDwarfSnapshot?: DwarfSelectionPanelsProps["loadDwarfSnapshot"];
  showDevelopmentPreview?: DwarfSelectionPanelsProps["showDevelopmentPreview"];
  createChatSession?: DwarfSelectionPanelsProps["createChatSession"];
  sendChatMessage?: DwarfSelectionPanelsProps["sendChatMessage"];
  loadChatPromptPreview?: DwarfSelectionPanelsProps["loadChatPromptPreview"];
}

export function App({
  loadHealth,
  loadRuntimeStatus,
  loadDwarfList,
  loadDwarfSnapshot,
  showDevelopmentPreview,
  createChatSession,
  sendChatMessage,
  loadChatPromptPreview,
}: AppProps) {
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
        <RuntimeStatusPanel loadRuntimeStatus={loadRuntimeStatus} />

        <section className="panel panel--workflow" aria-labelledby="workflow-heading">
          <div className="panel__header">
            <span className="status-chip status-chip--pending">Waiting</span>
            <h2 id="workflow-heading">Browser workflow</h2>
          </div>
          <div className="workflow-grid">
            <DwarfSelectionPanels
              loadDwarfList={loadDwarfList}
              loadDwarfSnapshot={loadDwarfSnapshot}
              showDevelopmentPreview={showDevelopmentPreview}
              createChatSession={createChatSession}
              sendChatMessage={sendChatMessage}
              loadChatPromptPreview={loadChatPromptPreview}
            />
          </div>
        </section>
      </main>
    </div>
  );
}
