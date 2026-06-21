import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { ProviderStatusResponse, AdapterStatusResponse } from "../../api/status";
import { RuntimeStatusPanel } from "./RuntimeStatusPanel";

function makeProvider(overrides?: Partial<ProviderStatusResponse>): ProviderStatusResponse {
  return {
    providerType: "Fake",
    model: "fake-dwarf",
    isConfigured: true,
    isReady: true,
    lastOutcome: "not_started",
    lastErrorCategory: null,
    lastDurationMs: null,
    lastUpdatedAtUtc: null,
    ...overrides,
  };
}

function makeAdapter(overrides?: Partial<AdapterStatusResponse>): AdapterStatusResponse {
  return {
    adapterType: "Fake",
    isConfigured: true,
    isReady: true,
    lastOutcome: "not_started",
    lastErrorCategory: null,
    lastDurationMs: null,
    lastUpdatedAtUtc: null,
    ...overrides,
  };
}

describe("RuntimeStatusPanel", () => {
  it("shows loading while status is being fetched", () => {
    render(<RuntimeStatusPanel loadRuntimeStatus={() => new Promise<never>(() => undefined)} />);

    expect(screen.getByText("Checking runtime status...")).toBeInTheDocument();
  });

  it("shows ready state when both provider and adapter are configured and ready", async () => {
    render(
      <RuntimeStatusPanel
        loadRuntimeStatus={async () => ({
          provider: makeProvider(),
          adapter: makeAdapter(),
        })}
      />,
    );

    expect(await screen.findByText("Ready")).toBeInTheDocument();
    expect(screen.getByText("Fake")).toBeInTheDocument();
    expect(screen.getByText("fake-dwarf")).toBeInTheDocument();
  });

  it("shows degraded state when provider has a last error", async () => {
    render(
      <RuntimeStatusPanel
        loadRuntimeStatus={async () => ({
          provider: makeProvider({
            isReady: true,
            lastOutcome: "error",
            lastErrorCategory: "invalid_response",
          }),
          adapter: makeAdapter(),
        })}
      />,
    );

    expect(await screen.findByText("Degraded")).toBeInTheDocument();
    expect(screen.getByText("invalid_response")).toBeInTheDocument();
  });

  it("shows not-configured state when provider is not configured", async () => {
    render(
      <RuntimeStatusPanel
        loadRuntimeStatus={async () => ({
          provider: makeProvider({
            isConfigured: false,
            isReady: false,
            lastOutcome: "error",
            lastErrorCategory: "missing_api_key",
          }),
          adapter: makeAdapter(),
        })}
      />,
    );

    expect(await screen.findByText("Not configured")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /setup guide/i })).toBeInTheDocument();
  });

  it("shows not-configured state when adapter is not configured", async () => {
    render(
      <RuntimeStatusPanel
        loadRuntimeStatus={async () => ({
          provider: makeProvider(),
          adapter: makeAdapter({
            isConfigured: false,
            isReady: false,
            lastOutcome: "error",
            lastErrorCategory: "adapter_disabled",
          }),
        })}
      />,
    );

    expect(await screen.findByText("Not configured")).toBeInTheDocument();
  });

  it("shows degraded state when adapter has a last error", async () => {
    render(
      <RuntimeStatusPanel
        loadRuntimeStatus={async () => ({
          provider: makeProvider(),
          adapter: makeAdapter({
            isReady: true,
            lastOutcome: "error",
            lastErrorCategory: "dfhack_error",
          }),
        })}
      />,
    );

    expect(await screen.findByText("Degraded")).toBeInTheDocument();
    expect(screen.getByText("dfhack_error")).toBeInTheDocument();
  });

  it("shows error state and allows retry when status fetch fails", async () => {
    const loadRuntimeStatus = vi.fn()
      .mockRejectedValueOnce(new Error("network failure"))
      .mockResolvedValue({
        provider: makeProvider(),
        adapter: makeAdapter(),
      });

    render(<RuntimeStatusPanel loadRuntimeStatus={loadRuntimeStatus} />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Runtime status is unavailable right now.",
    );

    const retryButton = screen.getByRole("button", { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    fireEvent.click(retryButton);

    expect(await screen.findByText("Ready")).toBeInTheDocument();
    expect(loadRuntimeStatus).toHaveBeenCalledTimes(2);
  });

  it("retry button is keyboard accessible", async () => {
    const loadRuntimeStatus = vi.fn()
      .mockRejectedValueOnce(new Error("network failure"))
      .mockResolvedValue({
        provider: makeProvider(),
        adapter: makeAdapter(),
      });

    render(<RuntimeStatusPanel loadRuntimeStatus={loadRuntimeStatus} />);

    await screen.findByRole("alert");

    const retryButton = screen.getByRole("button", { name: /retry/i });
    retryButton.focus();
    expect(retryButton).toHaveFocus();

    fireEvent.keyDown(retryButton, { key: "Enter", code: "Enter" });
    fireEvent.click(retryButton);

    expect(await screen.findByText("Ready")).toBeInTheDocument();
  });
});
