import { expect, test } from "@playwright/test";

test("loads list, selects dwarf, chats, and shows safe fake diagnostics", async ({ page }) => {
  await page.goto("/");

  const dwarfListPanel = page.locator("article", {
    has: page.getByRole("heading", { name: "Dwarf list" }),
  });
  await expect(dwarfListPanel).toBeVisible();

  const selectedDwarfButton = page.getByRole("button", { name: /Iden Torrentshade/i });
  await selectedDwarfButton.click();
  await expect(selectedDwarfButton).toHaveAttribute("aria-pressed", "true");

  const selectedDwarfPanel = page.locator("article", {
    has: page.getByRole("heading", { name: "Selected dwarf" }),
  });
  await expect(selectedDwarfPanel.locator(".dwarf-selected-name strong")).toHaveText("Iden Torrentshade");
  await expect(selectedDwarfPanel.getByText("Current job")).toBeVisible();
  await expect(selectedDwarfPanel.locator("dd").first()).not.toHaveText("No current job");

  const chatPanel = page.locator("article", {
    has: page.getByRole("heading", { name: "Chat" }),
  });
  await expect(chatPanel.getByText("Chat target:")).toContainText("Iden Torrentshade");

  const message = "What do you see around you right now?";
  await chatPanel.getByLabel("Message").fill(message);
  await chatPanel.getByRole("button", { name: "Send" }).click();

  const conversation = page.getByRole("list", { name: "Conversation" });
  await expect(conversation.getByText(message)).toBeVisible();
  await expect(conversation.getByText(/I can see/i)).toBeVisible();
  await expect(conversation.getByText("Current scene")).toBeVisible();
  await expect(conversation.getByText("Scene", { exact: true })).toBeVisible();
  await expect(conversation.getByText("Success")).toBeVisible();
  await expect(conversation.getByText("look_around")).not.toBeVisible();

  const diagnostics = conversation.getByText(
    /Provider:\s*Fake.*Model:\s*fake-dwarf.*Duration:\s*\d+ms.*Prompt:\s*prompt-[0-9a-f]{12}/,
  );
  await expect(diagnostics).toBeVisible();
  await expect(diagnostics).not.toContainText(message);
  await expect(diagnostics).not.toContainText("Authorization");
  await expect(conversation).not.toContainText("\"cells\"");
  await expect(conversation).not.toContainText("radius");
});

test("renders an unavailable current scene without claiming success", async ({ page }) => {
  await page.route("**/api/chat/sessions/*/messages", async (route) => {
    const sessionId = route.request().url().match(/\/sessions\/([^/]+)\/messages/)?.[1];
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        sessionId,
        dwarfId: "4101",
        assistantMessage: { role: "assistant", text: "I cannot see the fortress right now." },
        diagnostics: { provider: "Fake", model: "fake-dwarf", durationMs: 10, promptId: "prompt-unavailable" },
        toolReceipts: [],
        observationReceipts: [{ capability: "current_scene", outcome: "unavailable" }],
      }),
    });
  });

  await page.goto("/");
  await page.getByRole("button", { name: /Iden Torrentshade/i }).click();

  const chatPanel = page.locator("article", {
    has: page.getByRole("heading", { name: "Chat" }),
  });
  await chatPanel.getByLabel("Message").fill("What is around us?");
  await chatPanel.getByRole("button", { name: "Send" }).click();

  const conversation = page.getByRole("list", { name: "Conversation" });
  await expect(conversation.getByText("I cannot see the fortress right now.")).toBeVisible();
  await expect(conversation.getByText("Current scene", { exact: true })).toBeVisible();
  await expect(conversation.getByText("Unavailable", { exact: true })).toBeVisible();
  await expect(conversation.getByText("Success")).not.toBeVisible();
});

test("does not render a scene receipt when the chat turn fails", async ({ page }) => {
  await page.route("**/api/chat/sessions/*/messages", async (route) => {
    await route.fulfill({
      status: 503,
      contentType: "application/json",
      headers: { "X-Correlation-ID": "trace-provider-failure" },
      body: JSON.stringify({ errorCode: "chat_provider_unavailable" }),
    });
  });

  await page.goto("/");
  await page.getByRole("button", { name: /Iden Torrentshade/i }).click();

  const chatPanel = page.locator("article", {
    has: page.getByRole("heading", { name: "Chat" }),
  });
  await chatPanel.getByLabel("Message").fill("What is around us?");
  await chatPanel.getByRole("button", { name: "Send" }).click();

  await expect(page.getByRole("alert")).toContainText("The chat provider is unavailable right now.");
  const conversation = page.getByRole("list", { name: "Conversation" });
  await expect(conversation.getByText("What is around us?")).not.toBeVisible();
  await expect(conversation.getByText("Current scene")).not.toBeVisible();
  await expect(conversation.getByText("Success")).not.toBeVisible();
});
