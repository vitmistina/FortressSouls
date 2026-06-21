import { useEffect, useRef, useState } from "react";
import { ChatPanel } from "../chat/ChatPanel";
import type {
  ChatPromptPreviewResult,
  CreateChatSessionResult,
  SendChatMessageResult,
} from "../../api/chat";
import {
  DwarfApiError,
  fetchDwarfList,
  fetchDwarfSnapshot,
  type DwarfListItem,
  type DwarfListResult,
  type DwarfSnapshotResponse,
  type DwarfSnapshotResult,
} from "../../api/dwarves";

type DwarfListLoader = (signal?: AbortSignal) => Promise<DwarfListResult>;
type DwarfSnapshotLoader = (dwarfId: string, signal?: AbortSignal) => Promise<DwarfSnapshotResult>;

interface DwarfListStateLoading {
  kind: "loading";
}

interface DwarfListStateError {
  kind: "error";
  message: string;
  correlationId?: string;
}

interface DwarfListStateReady {
  kind: "ready";
  result: DwarfListResult;
}

type DwarfListState = DwarfListStateLoading | DwarfListStateError | DwarfListStateReady;

interface SnapshotStateIdle {
  kind: "idle";
}

interface SnapshotStateLoading {
  kind: "loading";
}

interface SnapshotStateError {
  kind: "error";
  message: string;
  correlationId?: string;
}

interface SnapshotStateDegraded {
  kind: "degraded";
  message: string;
  correlationId?: string;
}

interface SnapshotStateReady {
  kind: "ready";
  result: DwarfSnapshotResult;
}

type SnapshotState =
  | SnapshotStateIdle
  | SnapshotStateLoading
  | SnapshotStateError
  | SnapshotStateDegraded
  | SnapshotStateReady;

export interface DwarfSelectionPanelsProps {
  loadDwarfList?: DwarfListLoader;
  loadDwarfSnapshot?: DwarfSnapshotLoader;
  showDevelopmentPreview?: boolean;
  createChatSession?: (dwarfId: string, signal?: AbortSignal) => Promise<CreateChatSessionResult>;
  sendChatMessage?: (
    sessionId: string,
    message: string,
    signal?: AbortSignal,
  ) => Promise<SendChatMessageResult>;
  loadChatPromptPreview?: (sessionId: string, signal?: AbortSignal) => Promise<ChatPromptPreviewResult>;
}

const snapshotPreviewCharacterLimit = 8000;

export function DwarfSelectionPanels({
  loadDwarfList = fetchDwarfList,
  loadDwarfSnapshot = fetchDwarfSnapshot,
  showDevelopmentPreview = import.meta.env.DEV,
  createChatSession,
  sendChatMessage,
  loadChatPromptPreview,
}: DwarfSelectionPanelsProps) {
  const allowDevelopmentPreview = import.meta.env.DEV && showDevelopmentPreview;
  const [listState, setListState] = useState<DwarfListState>({ kind: "loading" });
  const [listRetryKey, setListRetryKey] = useState(0);
  const [selectedDwarfId, setSelectedDwarfId] = useState<string | null>(null);
  const [snapshotState, setSnapshotState] = useState<SnapshotState>({ kind: "idle" });
  const snapshotControllerRef = useRef<AbortController | null>(null);
  const snapshotRequestIdRef = useRef(0);
  const selectedDwarfRef = useRef<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    loadDwarfList(controller.signal)
      .then((result) => {
        if (!active) {
          return;
        }

        setListState({ kind: "ready", result });

        setSelectedDwarfId((currentSelection) => {
          if (currentSelection === null) {
            return currentSelection;
          }

          const stillExists = result.list.items.some((item) => item.id === currentSelection);
          if (stillExists) {
            return currentSelection;
          }

          selectedDwarfRef.current = null;
          setSnapshotState({
            kind: "degraded",
            message: "The previous dwarf selection is no longer available. Choose another dwarf.",
          });
          return null;
        });
      })
      .catch((error: unknown) => {
        if (!active) {
          return;
        }

        selectedDwarfRef.current = null;
        setSelectedDwarfId(null);
        setSnapshotState({ kind: "idle" });

        const apiError = error instanceof DwarfApiError ? error : undefined;
        setListState({
          kind: "error",
          message: "Dwarf roster is unavailable right now.",
          correlationId: apiError?.correlationId,
        });
      });

    return () => {
      active = false;
      controller.abort();
      snapshotControllerRef.current?.abort();
      snapshotRequestIdRef.current += 1;
    };
  }, [loadDwarfList, listRetryKey]);

  const handleRetryList = () => {
    selectedDwarfRef.current = null;
    setSelectedDwarfId(null);
    setSnapshotState({ kind: "idle" });
    setListState({ kind: "loading" });
    setListRetryKey((value) => value + 1);
  };

  const handleChooseAnotherDwarf = () => {
    selectedDwarfRef.current = null;
    setSelectedDwarfId(null);
    setSnapshotState({ kind: "idle" });
  };

  const handleSelectDwarf = (dwarfId: string) => {
    if (listState.kind !== "ready") {
      return;
    }

    const selectedStillExists = listState.result.list.items.some((item) => item.id === dwarfId);
    if (!selectedStillExists) {
      selectedDwarfRef.current = null;
      setSelectedDwarfId(null);
      setSnapshotState({
        kind: "degraded",
        message: "The selected dwarf is stale or invalid. Choose another dwarf.",
      });
      return;
    }

    snapshotControllerRef.current?.abort();
    const controller = new AbortController();
    snapshotControllerRef.current = controller;
    const requestId = snapshotRequestIdRef.current + 1;
    snapshotRequestIdRef.current = requestId;
    selectedDwarfRef.current = dwarfId;
    setSelectedDwarfId(dwarfId);
    setSnapshotState({ kind: "loading" });

    loadDwarfSnapshot(dwarfId, controller.signal)
      .then((result) => {
        if (
          controller.signal.aborted ||
          requestId !== snapshotRequestIdRef.current ||
          selectedDwarfRef.current !== dwarfId
        ) {
          return;
        }

        if (result.snapshot.dwarfId !== dwarfId) {
          selectedDwarfRef.current = null;
          setSelectedDwarfId(null);
          setSnapshotState({
            kind: "degraded",
            message: "Snapshot data did not match the selected dwarf. Choose a dwarf again.",
            correlationId: result.correlationId,
          });
          return;
        }

        setSnapshotState({ kind: "ready", result });
      })
      .catch((error: unknown) => {
        if (
          controller.signal.aborted ||
          requestId !== snapshotRequestIdRef.current ||
          selectedDwarfRef.current !== dwarfId
        ) {
          return;
        }

        if (
          error instanceof DwarfApiError &&
          (error.errorCode === "dwarf_not_found" || error.errorCode === "invalid_dwarf_id")
        ) {
          selectedDwarfRef.current = null;
          setSelectedDwarfId(null);
          setSnapshotState({
            kind: "degraded",
            message: "The selected dwarf is stale or invalid. Choose another dwarf.",
            correlationId: error.correlationId,
          });
          return;
        }

        const apiError = error instanceof DwarfApiError ? error : undefined;
        const unavailable =
          apiError?.errorCode === "dwarf_source_unavailable" || apiError?.errorCode === "request_cancelled";

        setSnapshotState({
          kind: "error",
          message: unavailable
            ? "Dwarf snapshot is unavailable right now."
            : "Unable to load dwarf snapshot right now.",
          correlationId: apiError?.correlationId,
        });
      });
  };

  const selectedDwarf =
    listState.kind === "ready"
      ? listState.result.list.items.find((item) => item.id === selectedDwarfId) ?? null
      : null;

  return (
    <>
      <DwarfListPanel
        listState={listState}
        selectedDwarfId={selectedDwarfId}
        onSelectDwarf={handleSelectDwarf}
        onRetry={handleRetryList}
      />
      <SelectedDwarfPanel
        listState={listState}
        selectedDwarf={selectedDwarf}
        snapshotState={snapshotState}
        showDevelopmentPreview={allowDevelopmentPreview}
        onChooseAnotherDwarf={handleChooseAnotherDwarf}
      />
      <ChatPanel
        key={selectedDwarfId ?? "chat-none-selected"}
        selectedDwarfId={selectedDwarfId}
        selectedDwarfName={selectedDwarf?.displayName ?? null}
        showDevelopmentPreview={allowDevelopmentPreview}
        createSession={createChatSession}
        sendMessage={sendChatMessage}
        loadPromptPreview={loadChatPromptPreview}
      />
    </>
  );
}

interface DwarfListPanelProps {
  listState: DwarfListState;
  selectedDwarfId: string | null;
  onSelectDwarf: (dwarfId: string) => void;
  onRetry: () => void;
}

function DwarfListPanel({ listState, selectedDwarfId, onSelectDwarf, onRetry }: DwarfListPanelProps) {
  if (listState.kind === "loading") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="dwarf-list-heading" aria-busy="true">
        <div className="panel__header">
          <span className="status-chip status-chip--loading">Loading</span>
          <h2 id="dwarf-list-heading">Dwarf list</h2>
        </div>
        <p className="panel__copy">Loading dwarves...</p>
      </article>
    );
  }

  if (listState.kind === "error") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="dwarf-list-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--error">Unavailable</span>
          <h2 id="dwarf-list-heading">Dwarf list</h2>
        </div>
        <p className="panel__copy panel__copy--error" role="alert">
          {listState.message}
        </p>
        {listState.correlationId ? (
          <p className="panel__copy">
            Correlation ID: <code>{listState.correlationId}</code>
          </p>
        ) : null}
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      </article>
    );
  }

  if (listState.result.list.items.length === 0) {
    return (
      <article className="panel dwarf-panel" aria-labelledby="dwarf-list-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--pending">Empty</span>
          <h2 id="dwarf-list-heading">Dwarf list</h2>
        </div>
        <p className="panel__copy">No dwarves are currently available.</p>
      </article>
    );
  }

  return (
    <article className="panel dwarf-panel" aria-labelledby="dwarf-list-heading">
      <div className="panel__header">
        <span className="status-chip status-chip--ready">Ready</span>
        <h2 id="dwarf-list-heading">Dwarf list</h2>
      </div>
      <p className="panel__copy">
        Source: <code>{listState.result.list.source.adapter}</code>
      </p>
      <ul className="dwarf-list" aria-label="Available dwarves">
        {listState.result.list.items.map((item) => (
          <li key={item.id}>
            <button
              type="button"
              className="dwarf-list__button"
              aria-pressed={selectedDwarfId === item.id}
              onClick={() => onSelectDwarf(item.id)}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  onSelectDwarf(item.id);
                }
              }}
            >
              <span className="dwarf-list__name">{item.displayName}</span>
              <span className="dwarf-list__meta">{item.profession}</span>
              <span className="dwarf-list__meta">
                Job: {item.currentJob ?? "No current job"} · Stress: {item.stressLevel}
              </span>
              {selectedDwarfId === item.id ? <span className="dwarf-list__selected">Selected</span> : null}
            </button>
          </li>
        ))}
      </ul>
    </article>
  );
}

interface SelectedDwarfPanelProps {
  listState: DwarfListState;
  selectedDwarf: DwarfListItem | null;
  snapshotState: SnapshotState;
  showDevelopmentPreview: boolean;
  onChooseAnotherDwarf: () => void;
}

function SelectedDwarfPanel({
  listState,
  selectedDwarf,
  snapshotState,
  showDevelopmentPreview,
  onChooseAnotherDwarf,
}: SelectedDwarfPanelProps) {
  if (listState.kind === "loading") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--loading">Waiting</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">Waiting for dwarf list...</p>
      </article>
    );
  }

  if (listState.kind === "error") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--error">Unavailable</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">Snapshot view is unavailable while the dwarf list cannot be loaded.</p>
      </article>
    );
  }

  if (listState.result.list.items.length === 0) {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--pending">Empty</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">No dwarf can be selected yet.</p>
      </article>
    );
  }

  if (selectedDwarf === null) {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--pending">No selection</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">Select a dwarf from the list to load a snapshot.</p>
        {snapshotState.kind === "degraded" ? (
          <p className="panel__copy panel__copy--error" role="status">
            {snapshotState.message}
            {snapshotState.correlationId ? (
              <>
                {" "}
                Correlation ID: <code>{snapshotState.correlationId}</code>
              </>
            ) : null}
          </p>
        ) : null}
      </article>
    );
  }

  if (snapshotState.kind === "loading") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading" aria-busy="true">
        <div className="panel__header">
          <span className="status-chip status-chip--loading">Loading</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">
          Loading snapshot for <strong>{selectedDwarf.displayName}</strong>...
        </p>
      </article>
    );
  }

  if (snapshotState.kind === "error") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--error">Unavailable</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy panel__copy--error" role="alert">
          {snapshotState.message}
        </p>
        {snapshotState.correlationId ? (
          <p className="panel__copy">
            Correlation ID: <code>{snapshotState.correlationId}</code>
          </p>
        ) : null}
        <button type="button" onClick={onChooseAnotherDwarf}>
          Choose another dwarf
        </button>
      </article>
    );
  }

  if (snapshotState.kind !== "ready") {
    return (
      <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
        <div className="panel__header">
          <span className="status-chip status-chip--pending">Pending</span>
          <h2 id="selected-dwarf-heading">Selected dwarf</h2>
        </div>
        <p className="panel__copy">Select a dwarf from the list to load a snapshot.</p>
      </article>
    );
  }

  const { snapshot, correlationId } = snapshotState.result;

  return (
    <article className="panel dwarf-panel" aria-labelledby="selected-dwarf-heading">
      <div className="panel__header">
        <span className="status-chip status-chip--ready">Ready</span>
        <h2 id="selected-dwarf-heading">Selected dwarf</h2>
      </div>
      <p className="dwarf-selected-name">
        <strong>{snapshot.identity.displayName}</strong> · {snapshot.identity.profession}
      </p>
      <dl className="dwarf-snapshot-grid">
        <div>
          <dt>Current job</dt>
          <dd>{snapshot.work.currentJob ?? "No current job"}</dd>
        </div>
        <div>
          <dt>Health</dt>
          <dd>{snapshot.health.summary}</dd>
        </div>
        <div>
          <dt>Top skill</dt>
          <dd>{snapshot.skills[0] ? `${snapshot.skills[0].name} (${snapshot.skills[0].description})` : "None"}</dd>
        </div>
        <div>
          <dt>Snapshot tick</dt>
          <dd>{snapshot.gameTick}</dd>
        </div>
      </dl>
      {correlationId ? (
        <p className="panel__copy">
          Correlation ID: <code>{correlationId}</code>
        </p>
      ) : null}
      {showDevelopmentPreview ? <SnapshotDebugPanel snapshot={snapshot} /> : null}
    </article>
  );
}

interface SnapshotDebugPanelProps {
  snapshot: DwarfSnapshotResponse;
}

function SnapshotDebugPanel({ snapshot }: SnapshotDebugPanelProps) {
  const fullJson = JSON.stringify(snapshot, null, 2);
  const preview =
    fullJson.length > snapshotPreviewCharacterLimit
      ? `${fullJson.slice(0, snapshotPreviewCharacterLimit)}\n…(preview truncated)`
      : fullJson;

  return (
    <details className="snapshot-preview">
      <summary>Development snapshot preview (contract JSON)</summary>
      <pre>{preview}</pre>
    </details>
  );
}
