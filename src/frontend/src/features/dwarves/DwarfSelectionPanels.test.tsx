import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DwarfApiError } from "../../api/dwarves";
import type { DwarfSnapshotResponse } from "../../api/dwarves";
import { DwarfSelectionPanels } from "./DwarfSelectionPanels";

function createListResult() {
  return {
    list: {
      items: [
        {
          id: "4101",
          displayName: "Iden Torrentshade",
          profession: "Miner",
          currentJob: "Dig",
          stressLevel: "Low",
        },
        {
          id: "4102",
          displayName: "Domas Inkgranite",
          profession: "Bookkeeper",
          currentJob: "UpdateStockpileRecords",
          stressLevel: "Moderate",
        },
      ],
      source: {
        adapter: "Fake",
        snapshotTick: 123456,
        schemaVersion: "dwarf-list.v0.1",
      },
    },
  };
}

describe("DwarfSelectionPanels", () => {
  it("shows loading while dwarf list request is in flight", () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={() => new Promise<never>(() => undefined)}
        showDevelopmentPreview={false}
      />,
    );

    expect(screen.getByText("Loading dwarves...")).toBeInTheDocument();
    expect(screen.getByText("Waiting for dwarf list...")).toBeInTheDocument();
  });

  it("shows empty state when backend returns no dwarves", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => ({
          list: {
            items: [],
            source: {
              adapter: "Fake",
              snapshotTick: 123456,
              schemaVersion: "dwarf-list.v0.1",
            },
          },
        })}
        showDevelopmentPreview={false}
      />,
    );

    expect(await screen.findByText("No dwarves are currently available.")).toBeInTheDocument();
    expect(screen.getByText("No dwarf can be selected yet.")).toBeInTheDocument();
  });

  it("lets the user select a dwarf and loads matching snapshot", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async (dwarfId) => ({
          snapshot: {
            schemaVersion: "dwarf-snapshot.v0.1",
            dwarfId,
            extractedAt: "2026-06-18T00:00:00Z",
            gameTick: 123456,
            identity: {
              displayName: dwarfId === "4101" ? "Iden Torrentshade" : "Domas Inkgranite",
              profession: dwarfId === "4101" ? "Miner" : "Bookkeeper",
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
          correlationId: "trace-123",
        })}
        showDevelopmentPreview={false}
      />,
    );

    const selectButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    expect(selectButton).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(selectButton);

    expect(await screen.findByText("No known injuries.")).toBeInTheDocument();
    expect(selectButton).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText("trace-123")).toBeInTheDocument();
  });

  it("shows degraded no-selection state for stale selected dwarf id", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async () => {
          throw new DwarfApiError("stale", 404, "dwarf_not_found", "trace-stale");
        }}
        showDevelopmentPreview={false}
      />,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Iden Torrentshade/i }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      /The selected dwarf is stale or invalid\. Choose another dwarf\./,
    );
    expect(screen.getByText("trace-stale")).toBeInTheDocument();
  });

  it("clears selection when snapshot identity does not match the selected dwarf", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async () => ({
          snapshot: {
            schemaVersion: "dwarf-snapshot.v0.1",
            dwarfId: "4102",
            extractedAt: "2026-06-18T00:00:00Z",
            gameTick: 123456,
            identity: {
              displayName: "Domas Inkgranite",
              profession: "Bookkeeper",
            },
            work: {
              currentJob: "UpdateStockpileRecords",
              labors: ["Bookkeeping"],
            },
            skills: [{ name: "Record Keeping", level: 8, description: "Skilled" }],
            personality: { traits: [], values: [] },
            needs: [],
            relationships: [],
            health: { summary: "No known injuries." },
            debug: { adapter: "Fake", rawAvailable: false },
          },
          correlationId: "trace-mismatch",
        })}
        showDevelopmentPreview={false}
      />,
    );

    const firstDwarfButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    const secondDwarfButton = screen.getByRole("button", { name: /Domas Inkgranite/i });

    fireEvent.click(firstDwarfButton);

    expect(await screen.findByRole("status")).toHaveTextContent(
      /Snapshot data did not match the selected dwarf\. Choose a dwarf again\./,
    );
    expect(screen.getByText("trace-mismatch")).toBeInTheDocument();
    expect(firstDwarfButton).toHaveAttribute("aria-pressed", "false");
    expect(secondDwarfButton).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByText("Select a dwarf from the list to load a snapshot.")).toBeInTheDocument();
  });

  it("supports keyboard selection and deterministic reselect refresh", async () => {
    let requestCount = 0;
    const snapshotLoader = vi.fn(async () => {
      requestCount += 1;
      return {
        snapshot: {
          schemaVersion: "dwarf-snapshot.v0.1",
          dwarfId: "4101",
          extractedAt: "2026-06-18T00:00:00Z",
          gameTick: 123456 + requestCount,
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
          health: { summary: requestCount === 1 ? "Minor bruises." : "No known injuries." },
          debug: { adapter: "Fake", rawAvailable: false },
        },
      };
    });

    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={snapshotLoader}
        showDevelopmentPreview={false}
      />,
    );

    const firstDwarfButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    fireEvent.keyDown(document.body, { key: "Tab", code: "Tab" });
    firstDwarfButton.focus();
    expect(firstDwarfButton).toHaveFocus();

    fireEvent.keyDown(firstDwarfButton, { key: "Enter", code: "Enter" });
    expect(await screen.findByText("Minor bruises.")).toBeInTheDocument();
    expect(firstDwarfButton).toHaveAttribute("aria-pressed", "true");

    fireEvent.keyDown(firstDwarfButton, { key: " ", code: "Space" });
    expect(await screen.findByText("No known injuries.")).toBeInTheDocument();
    await waitFor(() => expect(snapshotLoader).toHaveBeenCalledTimes(2));
  });

  it("ignores stale snapshot completion during rapid same-dwarf reselection", async () => {
    type SnapshotResult = { snapshot: DwarfSnapshotResponse; correlationId?: string };
    let firstResolve: ((value: SnapshotResult) => void) | undefined;
    let requestCount = 0;
    const snapshotLoader = vi.fn(async (dwarfId: string) => {
      requestCount += 1;
      if (requestCount === 1) {
        return await new Promise<SnapshotResult>(
          (resolve) => {
            firstResolve = resolve;
          },
        );
      }

      return {
        snapshot: {
          schemaVersion: "dwarf-snapshot.v0.1",
          dwarfId,
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
          health: { summary: "Second result" },
          debug: { adapter: "Fake", rawAvailable: false },
        },
      };
    });

    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={snapshotLoader}
        showDevelopmentPreview={false}
      />,
    );

    const selectButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    fireEvent.click(selectButton);
    fireEvent.click(selectButton);

    expect(await screen.findByText("Second result")).toBeInTheDocument();

    firstResolve?.({
      snapshot: {
        schemaVersion: "dwarf-snapshot.v0.1",
        dwarfId: "4101",
        extractedAt: "2026-06-18T00:00:00Z",
        gameTick: 123455,
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
        health: { summary: "First stale result" },
        debug: { adapter: "Fake", rawAvailable: false },
      },
    });

    await waitFor(() => {
      expect(screen.getByText("Second result")).toBeInTheDocument();
      expect(screen.queryByText("First stale result")).not.toBeInTheDocument();
    });
  });

  it("renders safe unavailable state when list cannot be loaded", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => {
          throw new DwarfApiError("boom", 503, "dwarf_source_unavailable", "trace-list-error");
        }}
        showDevelopmentPreview={false}
      />,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Dwarf roster is unavailable right now.");
    expect(screen.getByText("trace-list-error")).toBeInTheDocument();
  });

  it("retry button in list error re-triggers dwarf list load", async () => {
    const loadDwarfList = vi.fn()
      .mockRejectedValueOnce(new DwarfApiError("boom", 503, "dwarf_source_unavailable"))
      .mockResolvedValue(createListResult());

    render(
      <DwarfSelectionPanels
        loadDwarfList={loadDwarfList}
        showDevelopmentPreview={false}
      />,
    );

    await screen.findByRole("alert");

    const retryButton = screen.getByRole("button", { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    fireEvent.click(retryButton);

    expect(await screen.findByText("Iden Torrentshade")).toBeInTheDocument();
    expect(loadDwarfList).toHaveBeenCalledTimes(2);
  });

  it("retry button in list error is keyboard accessible", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => {
          throw new DwarfApiError("boom", 503, "dwarf_source_unavailable");
        }}
        showDevelopmentPreview={false}
      />,
    );

    await screen.findByRole("alert");

    const retryButton = screen.getByRole("button", { name: /retry/i });
    retryButton.focus();
    expect(retryButton).toHaveFocus();
  });

  it("renders selected panel snapshot-unavailable state without clearing selection", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async () => {
          throw new DwarfApiError("unavailable", 503, "dwarf_source_unavailable", "trace-snapshot-error");
        }}
        showDevelopmentPreview={false}
      />,
    );

    const selectButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    fireEvent.click(selectButton);

    expect(await screen.findByRole("alert")).toHaveTextContent("Dwarf snapshot is unavailable right now.");
    expect(screen.getByText("trace-snapshot-error")).toBeInTheDocument();
    expect(selectButton).toHaveAttribute("aria-pressed", "true");
  });

  it("choose another dwarf button in snapshot error clears selection and returns to idle", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async () => {
          throw new DwarfApiError("unavailable", 503, "dwarf_source_unavailable");
        }}
        showDevelopmentPreview={false}
      />,
    );

    const selectButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    fireEvent.click(selectButton);

    await screen.findByRole("alert");

    const chooseButton = screen.getByRole("button", { name: /choose another dwarf/i });
    expect(chooseButton).toBeInTheDocument();

    fireEvent.click(chooseButton);

    expect(screen.getByText("Select a dwarf from the list to load a snapshot.")).toBeInTheDocument();
    expect(selectButton).toHaveAttribute("aria-pressed", "false");
  });

  it("choose another dwarf button is keyboard accessible", async () => {
    render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={async () => {
          throw new DwarfApiError("unavailable", 503, "dwarf_source_unavailable");
        }}
        showDevelopmentPreview={false}
      />,
    );

    const selectButton = await screen.findByRole("button", { name: /Iden Torrentshade/i });
    fireEvent.click(selectButton);

    await screen.findByRole("alert");

    const chooseButton = screen.getByRole("button", { name: /choose another dwarf/i });
    chooseButton.focus();
    expect(chooseButton).toHaveFocus();
  });

  it("shows development snapshot preview only when enabled", async () => {
    const snapshotLoader = async (dwarfId: string) => ({
      snapshot: {
        schemaVersion: "dwarf-snapshot.v0.1",
        dwarfId,
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
    });

    const { rerender } = render(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={snapshotLoader}
        showDevelopmentPreview={false}
      />,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Iden Torrentshade/i }));
    await screen.findByText("No known injuries.");
    expect(screen.queryByText("Development snapshot preview (contract JSON)")).not.toBeInTheDocument();

    rerender(
      <DwarfSelectionPanels
        loadDwarfList={async () => createListResult()}
        loadDwarfSnapshot={snapshotLoader}
        showDevelopmentPreview
      />,
    );

    fireEvent.click(await screen.findByRole("button", { name: /Iden Torrentshade/i }));
    expect(await screen.findByText("Development snapshot preview (contract JSON)")).toBeInTheDocument();
  });
});
