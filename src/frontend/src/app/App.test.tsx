import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("App", () => {
  it("renders the shell and workflow placeholders", async () => {
    render(
      <App
        loadHealth={async () => ({
          health: {
            status: "ok",
            version: "0.1.0",
            adapter: "NotConfigured",
            provider: "NotConfigured",
            observability: "ConsoleFallback",
          },
          correlationId: "trace-123_abc",
        })}
        loadRuntimeStatus={async () => ({
          provider: {
            providerType: "Fake",
            model: "fake-dwarf",
            isConfigured: true,
            isReady: true,
            lastOutcome: "not_started",
            lastErrorCategory: null,
            lastDurationMs: null,
            lastUpdatedAtUtc: null,
          },
          adapter: {
            adapterType: "Fake",
            isConfigured: true,
            isReady: true,
            lastOutcome: "not_started",
            lastErrorCategory: null,
            lastDurationMs: null,
            lastUpdatedAtUtc: null,
          },
        })}
        loadDwarfList={async () => ({
          list: {
            items: [
              {
                id: "4101",
                displayName: "Iden Torrentshade",
                profession: "Miner",
                currentJob: "Dig",
                stressLevel: "Low",
              },
            ],
            source: {
              adapter: "Fake",
              snapshotTick: 123456,
              schemaVersion: "dwarf-list.v0.1",
            },
          },
        })}
        loadDwarfSnapshot={async () => ({
          snapshot: {
            schemaVersion: "dwarf-snapshot.v0.1",
            dwarfId: "4101",
            extractedAt: "2026-06-18T00:00:00Z",
            gameTick: 123456,
            identity: {
              displayName: "Iden Torrentshade",
              profession: "Miner",
            },
            work: {
              currentJob: "Dig",
              labors: ["Mining"],
            },
            skills: [{ name: "Mining", level: 8, description: "Skilled" }],
            personality: { traits: [], values: [] },
            needs: [],
            relationships: [],
            health: { summary: "No known injuries." },
            debug: { adapter: "Fake", rawAvailable: false },
          },
        })}
        showDevelopmentPreview={false}
      />,
    );

    expect(screen.getByRole("heading", { name: "Read-only dwarf companion" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Backend health" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Runtime status" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Dwarf list" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Selected dwarf" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Chat" })).toBeInTheDocument();
    expect(await screen.findByText("trace-123_abc")).toBeInTheDocument();
  });
});
