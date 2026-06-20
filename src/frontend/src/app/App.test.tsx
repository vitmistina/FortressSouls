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
      />,
    );

    expect(screen.getByRole("heading", { name: "Read-only dwarf companion" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Backend health" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Dwarf list" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Selected dwarf" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Chat" })).toBeInTheDocument();
    expect(await screen.findByText("trace-123_abc")).toBeInTheDocument();
  });
});
