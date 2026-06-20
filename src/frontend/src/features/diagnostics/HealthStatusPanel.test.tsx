import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
});
