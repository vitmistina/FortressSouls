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
      <header className="top-bar">
        <h1>Fortress Souls</h1>
        <p>Read-only dwarf companion</p>
      </header>

      <main className="content-grid">
        <DwarfSelectionPanels
          loadDwarfList={loadDwarfList}
          loadDwarfSnapshot={loadDwarfSnapshot}
          showDevelopmentPreview={showDevelopmentPreview}
          createChatSession={createChatSession}
          sendChatMessage={sendChatMessage}
          loadChatPromptPreview={loadChatPromptPreview}
          auxiliaryContent={
            <section className="auxiliary-grid auxiliary-grid--sidebar" aria-label="Runtime details">
              <HealthStatusPanel loadHealth={loadHealth} />
              <RuntimeStatusPanel loadRuntimeStatus={loadRuntimeStatus} />
            </section>
          }
        />
      </main>
    </div>
  );
}
