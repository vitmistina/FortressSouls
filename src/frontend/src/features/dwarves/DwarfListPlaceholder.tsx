export function DwarfListPlaceholder() {
  return (
    <article className="panel placeholder-card" aria-labelledby="dwarf-list-heading">
      <span className="placeholder-card__tag">Reserved for B-011</span>
      <h2 id="dwarf-list-heading">Dwarf list</h2>
      <p>
        The browser will own dwarf selection here once the backend list API is wired in.
      </p>
      <p className="placeholder-card__note">Expected source: <code>GET /api/dwarves</code>.</p>
    </article>
  );
}
