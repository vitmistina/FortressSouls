export function SelectedDwarfPlaceholder() {
  return (
    <article className="panel placeholder-card" aria-labelledby="selected-dwarf-heading">
      <span className="placeholder-card__tag">Reserved for B-011</span>
      <h2 id="selected-dwarf-heading">Selected dwarf</h2>
      <p>The validated snapshot for the selected dwarf will appear here.</p>
      <p className="placeholder-card__note">
        Browser selection, not the game UI cursor, will drive this panel.
      </p>
    </article>
  );
}
