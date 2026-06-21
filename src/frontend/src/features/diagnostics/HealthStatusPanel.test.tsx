import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HealthStatusPanel } from "./HealthStatusPanel";

describe("HealthStatusPanel", () => {
  it("shows loading while the request is in flight", () => {
    const loadHealth = () => new Promise<never>(() => undefined);

    render(<HealthStatusPanel loadHealth={loadHealth} />);

    expect(screen.getByText("Checking backend health...")).toBeInTheDocument();
  });

  it("renders a successful health response", async () => {
    render(
      <HealthStatusPanel
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
      />,
    );

    expect(await screen.findByText("ConsoleFallback")).toBeInTheDocument();
    expect(screen.getByText("trace-123_abc")).toBeInTheDocument();
  });

  it("renders a safe error state", async () => {
    render(<HealthStatusPanel loadHealth={async () => { throw new Error("boom"); }} />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Backend health is unavailable right now.",
    );
  });

  it("retry button re-triggers health load from error state", async () => {
    const loadHealth = vi.fn()
      .mockRejectedValueOnce(new Error("boom"))
      .mockResolvedValue({
        health: {
          status: "ok",
          version: "0.1.0",
          adapter: "Fake",
          provider: "Fake",
          observability: "ConsoleFallback",
        },
      });

    render(<HealthStatusPanel loadHealth={loadHealth} />);

    await screen.findByRole("alert");

    const retryButton = screen.getByRole("button", { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    fireEvent.click(retryButton);

    expect(await screen.findByText("ConsoleFallback")).toBeInTheDocument();
    expect(loadHealth).toHaveBeenCalledTimes(2);
  });

  it("retry button is keyboard accessible", async () => {
    const loadHealth = vi.fn()
      .mockRejectedValueOnce(new Error("boom"))
      .mockResolvedValue({
        health: {
          status: "ok",
          version: "0.1.0",
          adapter: "Fake",
          provider: "Fake",
          observability: "ConsoleFallback",
        },
      });

    render(<HealthStatusPanel loadHealth={loadHealth} />);

    await screen.findByRole("alert");

    const retryButton = screen.getByRole("button", { name: /retry/i });
    retryButton.focus();
    expect(retryButton).toHaveFocus();
  });
});
