export function ChatPlaceholder() {
  return (
    <article className="panel placeholder-card" aria-labelledby="chat-heading">
      <span className="placeholder-card__tag">Reserved for B-014/B-015</span>
      <h2 id="chat-heading">Chat</h2>
      <p>The in-memory conversation will appear here once the fake provider backend lands.</p>
      <p className="placeholder-card__note">
        This shell intentionally leaves chat behavior out for now.
      </p>
    </article>
  );
}
