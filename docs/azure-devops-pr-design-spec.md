# Azure DevOps PR "Files" Diff View — Implementation Spec (Light/Default Theme)

Build target: a pixel-faithful Blazor/HTML/CSS rebuild of the Azure DevOps Pull Request **Files** tab. All colors flow through CSS custom properties so a later dark-theme swap only changes values. Px values flagged *(approx)* are derived from the `azure-devops-ui` package + Monaco/VS Code source, not lifted from a live `dev.azure.com` stylesheet — tune them against a screenshot before locking.

---

## Layout map

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│ GLOBAL NAV  org > project  ▸ ▸ ▸                      🔍 search        ⚙  👤 account    │  ~44px  (stub)
├──┬───────────────────────────────────────────────────────────────────────────────────┤
│  │ PR HEADER                                                                            │
│H │  ┌Title  "Fix login redirect"  #1234 ──────────────────  [ Approve ▾ ]  avatars ◯◯◯ │  ~64px
│U │  └ source-branch  →  target-branch       ● Active                                    │
│B ├───────────────────────────────────────────────────────────────────────────────────┤
│  │ TAB BAR    Overview │  Files (12)  │ Updates │ Commits   [Conflicts]                 │  ~44px
│R │                       ▔▔▔▔▔▔▔▔ ← 2px #0078D4 underline on active                     │
│A ├──────────────┬────────────────────────────────────────────────────────────────────┤
│I │ FILE PANEL   ║│ SECONDARY TOOLBAR  [Side-by-side|Inline]  [changes ▾]   view opts    │  ~40px
│L │              ║│────────────────────────────────────────────────────────────────────│
│  │ ▾ src/       ║│ ┌ DIFF CARD ───────────────────────────────────────────────────┐   │
│  │   ▸ auth/    ║│ │ ▾  src/auth/Login.cs        [edit]            …    ⌄          │   │  card hdr ~40px sticky
│  │   + new.cs   ║│ ├──────────────────────────────────────────────────────────────┤   │
│  │   M Login.cs ║│ │‖ 12│ 12│ context line                                          │   │  diff body
│  │   - old.cs   ║│ │‖ 13│   │- removed line                  (red row)             │   │  20px rows
│  │ ▾ tests/     ║│ │‖   │ 13│+ added line                    (green row)           │   │
│  │   M api.cs   ║│ │  ··· 8 lines ···   [⌃ expand up] [⌄ expand down]  (hunk band)  │   │
│  │              ║│ └──────────────────────────────────────────────────────────────┘   │
│  │ ☑ reviewed   ║│ ┌ DIFF CARD (next file) ... ┐                                       │
│  │  (~300px)    ▲splitter (1px line / ~5px hit-area, draggable)                         │
└──┴──────────────╨────────────────────────────────────────────────────────────────────┘
```

### Region-by-region

**Global nav (stub).** Top app bar `~44px` high: org > project breadcrumb, search box, account/settings. Plus a narrow left vertical hub rail (`HUBRAIL` above, ~48px wide). Outside the PR feature; render as a flat strip + icon rail. Driven by `--nav-header-*` tokens in real ADO; not load-bearing for this view.

**PR header band.** Height `~64px` *(approx)*. Contains: PR **title** as page H1 (`20px`) + `#<id>`; a branch indicator `<source> → <target>` (two branch pills with an arrow glyph); a **status pill** (Active / Draft / Completed / Abandoned); author + reviewer avatars on the right, each reviewer avatar carrying a small vote-state badge. Right edge: primary action button (`Approve ▾` for reviewers / `Complete ▾` for author) as a split button in accent blue.

**Tab bar (Pivot).** Height `~44px`. Order: **Overview | Files | Updates | Commits** (`Conflicts` only when conflicts exist). Tabs may carry count badges (Files = file count, Overview = comment count). Inactive labels use `--ado-text-secondary`; active label uses `--ado-text-primary` with a **2px `#0078D4` bottom underline**. Background white.

**Secondary toolbar.** Height `~40px`, sits at the top of the right pane, sticky above the diff cards. Holds the **`Side-by-side | Inline`** layout toggle (segmented control), the multi-select **`changes`/updates dropdown** (scopes the diff to selected pushes; each entry labeled with that push's final commit message), and view options. Background `--ado-secondary-bg`.

**Left file panel.** Default width `~300px` *(approx; resizable ~250–320px)*, collapsible. A vertical **splitter** (1px visible line, ~5px drag hit-area) separates it from the diff area; the panel width must be a CSS variable the splitter JS rewrites. Rows are a file tree (folders + files); selecting the **tree root** shows the all-files summary. Each row: change-type glyph/label, file name, optional reviewed checkbox, hover `View` affordance. Tree row height `~32px` *(approx)*, indent `16px`/level.

**Right diff area.** Vertically scrollable column of stacked **diff cards**, one per changed file.

**Per-file diff card header.** Height `~40px`, `position: sticky; top: 0` within the scrolling right pane (or `top: 40px` if the secondary toolbar is also sticky). Contains: collapse chevron (`▾`/`▸`), file path/name, change-type badge (`[edit]`/`[add]`/`[delete]`/`[rename, edit]`), layout/view controls, overflow `…` menu. Background `--ado-secondary-bg`; bottom hairline `--ado-border`.

**Diff body.** The Monaco-based diff. Each line is a 20px-tall row: `[change-bar margin][gutter(s)][code]`. New/deleted files render a **single content pane** (one side) rather than a two-pane diff. Files >0.5 MB are not diffed in summary; single files >5 MB are truncated.

---

## Color & token reference

```css
:root {
  /* ---------- Fonts ---------- */
  --ado-ui-font: "Segoe UI", "-apple-system", BlinkMacSystemFont, Roboto,
                 "Helvetica Neue", Helvetica, Ubuntu, Arial, sans-serif,
                 "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol";
  --ado-code-font: "Cascadia Mono", Menlo, Consolas, "Courier New", monospace;

  /* ---------- Font sizes ---------- */
  --ado-font-size-base:      14px;   /* default UI/body text (body-m) */
  --ado-font-size-secondary: 12px;   /* captions, metadata, tooltips (body-s) */
  --ado-font-size-small:     11px;   /* small UI text */
  --ado-font-size-title:     20px;   /* PR title (page H1) */
  --ado-code-size:           13px;   /* diff code (.monospaced-m) */
  --ado-gutter-font-size:    13px;   /* line numbers (same monospace) */

  /* ---------- Font weights ---------- */
  --ado-weight-normal:   400;
  --ado-weight-semibold: 600;        /* headers, selected file path */
  --ado-weight-bold:     700;

  /* ---------- Line height & row heights ---------- */
  --ado-line-height:      20px;      /* code line-height == diff row height */
  --ado-diff-row-height:  20px;      /* one diff code line */
  --ado-tree-row-height:  32px;      /* file-tree row (approx) */

  /* ---------- Chrome dimensions (approx — verify vs screenshot) ---------- */
  --ado-header-height:               64px;  /* PR header band (approx) */
  --ado-tabbar-height:               44px;  /* Pivot/tab bar */
  --ado-secondary-toolbar-height:    40px;  /* toolbar above diff */
  --ado-diffcard-header-height:      40px;  /* sticky per-file header */
  --ado-tab-active-underline-thickness: 2px;
  --ado-filetree-width:              300px; /* resizable; rewritten by splitter JS */
  --ado-splitter-width:              1px;   /* visible line; ~5px hit-area */

  /* ---------- Tree / gutter / spacing ---------- */
  --ado-tree-indent:           16px;   /* per nesting level */
  --ado-tree-item-pad-y:       2px;
  --ado-gutter-width:          48px;   /* each line-number column; min ~32px */
  --ado-code-cell-padding-left: 8px;   /* gutter → first code char */
  --ado-gutter-pad-x:          8px;
  --ado-radius:                4px;    /* $radius-medium */
  --ado-radius-small:          2px;    /* tree expand button */
  --ado-spacing-4:  4px;
  --ado-spacing-8:  8px;
  --ado-spacing-12: 12px;
  --ado-spacing-16: 16px;
  --ado-spacing-20: 20px;
  --ado-spacing-24: 24px;

  /* ---------- Icon sizes ---------- */
  --ado-icon-small:  12px;
  --ado-icon-medium: 16px;            /* file/folder icons in tree */
  --ado-icon-large:  24px;

  /* ====================================================================
     PALETTE — RGB triplets fed through rgb()/rgba(), mirroring ADO so a
     theme swap only flips these values. (Light-theme values shown.)
     ==================================================================== */
  --palette-primary:      0,120,212;  /* accent blue #0078D4 (constant across themes) */
  --palette-neutral-0:    255,255,255;
  --palette-neutral-2:    250,249,248;
  --palette-neutral-4:    244,244,244;
  --palette-neutral-6:    239,239,239;
  --palette-neutral-8:    234,234,234;
  --palette-neutral-10:   225,223,221;
  --palette-neutral-20:   200,198,196;
  --palette-neutral-30:   96,94,92;
  --palette-neutral-60:   161,159,157;
  --palette-neutral-70:   121,119,117;
  --palette-neutral-80:   50,49,48;
  --palette-neutral-100:  32,31,30;

  /* ---------- Backgrounds ---------- */
  --ado-page-bg:        rgb(var(--palette-neutral-0));   /* #FFFFFF */
  --ado-panel-bg:       rgb(var(--palette-neutral-0));   /* card/diff container */
  --ado-secondary-bg:   rgb(var(--palette-neutral-2));   /* #FAF9F8 toolbar/header strip */
  --ado-neutral-4-bg:   rgb(var(--palette-neutral-4));   /* #F4F4F4 dividers/inactive */
  --ado-neutral-6-bg:   rgb(var(--palette-neutral-6));   /* #EFEFEF collapsed rows */
  --ado-hover-bg:       rgb(var(--palette-neutral-4));   /* #F4F4F4 list/tree row hover */
  --ado-row-select-bg:  rgb(var(--palette-neutral-6));   /* #EFEFEF selected row */

  /* ---------- Borders ---------- */
  --ado-border:        #E1DFDD;                          /* solid divider (neutral-10) */
  --ado-border-subtle: rgba(0,0,0,0.08);                 /* hairline (--border-subtle-color) */

  /* ---------- Text ---------- */
  --ado-text-primary:   rgba(0,0,0,0.90);  /* ≈ #201F1E — alpha-on-black, NOT solid */
  --ado-text-secondary: rgba(0,0,0,0.55);  /* ≈ #605E5C — metadata, inactive tabs */
  --ado-text-disabled:  rgba(0,0,0,0.38);  /* ≈ #A19F9D */

  /* ---------- Accent / links ---------- */
  --ado-accent:        rgb(var(--palette-primary));      /* #0078D4 */
  --ado-accent-hover:  #106EBE;                          /* themeDarkAlt */
  --ado-link:          rgb(var(--palette-primary));      /* #0078D4, underline on hover only */
  --ado-tab-active-underline-color: rgb(var(--palette-primary));

  /* ---------- Status / error / warning ---------- */
  --ado-status-success: #107C10;   /* success green */
  --ado-error-text:     #DA0A00;   /* status-error (core.css 218,10,0) */
  --ado-accent3:        #D67F3C;   /* attention/suggestion (core.css 214,127,60) */

  /* ====================================================================
     DIFF COLORS — proprietary VC renderer, NOT in published CSS.
     GitHub-family stand-ins; FLAGGED low/medium. Verify in DevTools on
     .line-insert / .line-delete / .char-insert / .char-delete.
     ==================================================================== */
  /* full-line backgrounds (translucent intent so syntax tokens show through) */
  --ado-diff-add-line-bg:    #E6FFED;   /* FLAG medium — added row tint */
  --ado-diff-remove-line-bg: #FFEEF0;   /* FLAG medium — removed row tint */
  --ado-diff-context-bg:     #FFFFFF;   /* unchanged/context + modified base */

  /* word-level (intra-line) darker shade over the row tint */
  --ado-diff-add-word-bg:    #ACF2BD;   /* FLAG low — changed tokens, added */
  --ado-diff-remove-word-bg: #FDB8C0;   /* FLAG low — changed tokens, removed */

  /* +/- marker glyph color (inline view) */
  --ado-diff-marker-add:    #22863A;    /* FLAG low */
  --ado-diff-marker-remove: #B31D28;    /* FLAG low */

  /* far-left vertical change-bar stripe */
  --ado-change-bar-add:    #2DA44E;     /* FLAG medium */
  --ado-change-bar-remove: #CF222E;     /* FLAG medium */
  --ado-change-bar-width:  3px;

  /* side-by-side empty-counterpart fill (Monaco diffEditor.diagonalFill) */
  --ado-diff-diagonal-fill: rgba(34,34,34,0.20);  /* #22222233 — medium */
  --ado-diff-empty-bg:      #F4F4F4;              /* flat neutral-4 alt fill */

  /* line-number gutter */
  --ado-diff-gutter-bg:     #FAFBFC;   /* slightly off-white vs code (FLAG low) */
  --ado-diff-gutter-text:   rgba(0,0,0,0.55);  /* muted grey ≈ #605E5C */
  --ado-diff-gutter-border: #E1E4E8;   /* gutter↔code + pane↔pane separator */

  /* hunk-expand separator band */
  --ado-hunk-band-bg:     #F3F6FB;     /* FLAG low — grey collapsed-lines band */
  --ado-hunk-band-fg:     #57606A;     /* muted band text/icons */
  --ado-hunk-band-border: #E1E4E8;     /* band top/bottom hairline */

  /* ---------- File-tree change-type badges ---------- */
  --ado-status-add:    #107C10;   /* Add — green (medium) */
  --ado-status-edit:   #0078D4;   /* Edit — blue (FLAG low; sometimes neutral) */
  --ado-status-delete: #DA0A00;   /* Delete — red (medium) */
  --ado-status-rename: #605E5C;   /* Rename/Move — neutral (FLAG low) */
  --ado-change-edit-amber: #E8A317; /* alt amber pencil/dot for edit (FLAG low) */

  /* ---------- Icon colors ---------- */
  --icon-folder-color: #DCB67A;   /* folder glyph (confirmed token) */
  --icon-file-color:   #605E5C;   /* generic file glyph (neutral/secondary) */

  /* ====================================================================
     SYNTAX HIGHLIGHTING — Monaco 'vs' light theme (sits ON TOP of row tint)
     ==================================================================== */
  --ado-syntax-keyword:  #0000FF;
  --ado-syntax-string:   #A31515;
  --ado-syntax-comment:  #008000;
  --ado-syntax-number:   #098658;
  --ado-syntax-type:     #267F99;
  --ado-syntax-function: #795E26;
  --ado-syntax-text:     #000000;
}
```

---

## Typography & spacing

**Font stacks**
- **UI / chrome:** `var(--ado-ui-font)` — Segoe UI primary (verbatim from `azure-devops-ui@2.275.0` `_platformCommon.scss`).
- **Code / diff:** `var(--ado-code-font)` — `"Cascadia Mono", Menlo, Consolas, "Courier New", monospace`.

**Sizes**
- PR title: `20px`.
- UI body / labels: `14px` (`body-m`, line-height 20px). Secondary/caption: `12px` (`body-s`, line-height 16px). Small: `11px`.
- Diff code: `13px` with `line-height: 20px`. Line numbers: `13px`, same monospace, `font-variant-numeric: tabular-nums`.
- Tab labels: `14px`. Section/file-path headers: semibold (`600`).

**Weights:** normal `400`, semibold `600`, bold `700`.

**Line / row heights**
- Diff code row: **exactly `20px`** (`line-height == row height`). Set this rigidly for vertical alignment between panes.
- File-tree row: `32px` *(approx)*; tree item vertical padding `2px`.
- Card header / toolbar / tab bar rows: `40px` / `40px` / `44px`.

**Gutter**
- Each line-number column: `48px` *(approx; min ~32px)*, `text-align: right`, `padding: 0 8px`, `user-select: none`, color `--ado-diff-gutter-text`.
- Inline view shows two adjacent number gutters (`[old#][new#]`), ~96px combined.

**Spacing (4px grid):** `4, 8, 12, 16, 20, 24, 32, 40`. All paddings/margins are multiples of 4.
- Diff code cell: `8px` left padding from gutter to first code char; `white-space: pre`.
- List/tree cell padding: `0 8px`.
- Tree expand button: `6px` padding, `2px` radius, `4px` margin-right, hover bg `--ado-hover-bg`.

**Border radius:** `4px` default (buttons, chips), `2px` for tree expand button.

**Reference diff-line recipe**
```css
.diff-line {
  display: flex;
  height: var(--ado-diff-row-height);            /* 20px */
  font: var(--ado-code-size)/var(--ado-line-height) var(--ado-code-font);
}
.diff-line .gutter {
  width: var(--ado-gutter-width); text-align: right;
  padding: 0 var(--ado-gutter-pad-x);
  color: var(--ado-diff-gutter-text);
  user-select: none; font-variant-numeric: tabular-nums;
}
.diff-line .code { padding-left: var(--ado-code-cell-padding-left); white-space: pre; }
```

---

## Diff rendering rules

The diff is a Monaco diff editor with ADO custom decorations (production class `modified-in-monaco-diff-editor`). Rebuild it as a grid of rows: each row = `[change-bar margin][gutter(s)][code]`. **Syntax highlighting is ON** inside the diff using the Monaco `vs` light theme; token colors sit *on top of* the translucent row tint.

### Common to both layouts
- **Two-level shading (signature detail):** the whole changed row gets the **light** tint (`--ado-diff-add-line-bg` / `--ado-diff-remove-line-bg`); only the changed characters get the **darker** word-level tint (`--ado-diff-add-word-bg` / `--ado-diff-remove-word-bg`) layered on top. Keep both translucent in intent so syntax tokens stay readable.
- **Far-left change-bar:** a thin `3px` solid vertical stripe in the decoration margin on every changed line — green (`--ado-change-bar-add`) for added, red (`--ado-change-bar-remove`) for removed. Independent of the row background tint.
- **Context/unchanged lines:** white background (`--ado-diff-context-bg`), both gutters numbered.
- **Modified line model:** Monaco has no "modified" state — an edited line is a **delete-left + insert-right on the same row**, aligned, with word-level highlights marking the actual changed spans. (This is why side-by-side pairs a red left row with a green right row instead of one "modified" color.)

### Side-by-side
- Two synchronized panes: **original (left)** and **modified (right)**, each with its **own** line-number gutter and a vertical `--ado-diff-gutter-border` between gutter and code; the two panes are separated by the same hairline.
- Removed lines (red tint) appear only on the **left**; added lines (green tint) only on the **right**; context lines appear in both at the same vertical position.
- **Empty counterpart:** where one side has no corresponding line (pure add or pure delete), the opposite cell is filled with a **diagonal-hatch shaded band** (`--ado-diff-diagonal-fill`, Monaco's `diffEditor.diagonalFill` ≈ `#22222233`) covering gutter + content, so the panes stay line-for-line aligned. A flat `--ado-diff-empty-bg` (#F4F4F4) is an acceptable alternative fill.
- No `+`/`-` glyph prefix in side-by-side; the side (left=removed, right=added) plus the change-bar conveys polarity.

### Inline (unified)
- Single code column with **two adjacent numeric gutters**: old line# (left), new line# (right).
  - Removed line → old# shown, new# blank.
  - Added line → new# shown, old# blank.
  - Context line → both numbers shown.
- Each changed line is prefixed with a **`+`/`-` marker glyph** (`--ado-diff-marker-add` / `--ado-diff-marker-remove`) — an ADO custom decoration Monaco lacks natively.
- Full-row green/red tint as above.

### Hunk-expand control
- Collapsed unchanged regions between hunks are hidden behind a **grey separator band** (`--ado-hunk-band-bg`) with hairline top/bottom borders (`--ado-hunk-band-border`).
- Band shows an **ellipsis / hidden-line count** in muted grey (`--ado-hunk-band-fg`) plus expand affordances: **expand-up** (`⌃`) and **expand-down** (`⌄`) chevrons, often an expand-all. Clicking inserts the hidden context rows.

### Word-level intra-line behavior
- Run a token/word diff on each modified line; wrap only the differing spans in a `char-insert`/`char-delete` element and paint it with the darker word-level background. Unchanged spans on a changed line keep the lighter full-row tint.

### Comment affordances
- Hovering a line reveals a comment (speech-bubble) button in/near the gutter. Multi-line selection shows a comment button spanning the selected lines.
- The **suggest-change light-bulb appears only on the modified (right) side**, never on the original (left) side of a side-by-side diff.

---

## File tree & status badges

**Tree rows**
- Row height `~32px` *(approx)*; item vertical padding `2px`.
- **Indent `16px` per nesting level** (left padding × depth).
- Expand/collapse chevron: `▸` collapsed / `▾` expanded (MDL2 `ChevronRightSmall` / `ChevronDownSmall`). Button: `6px` padding, `2px` radius, `4px` margin-right, hover bg `--ado-hover-bg`.
- **Hover:** row bg `--ado-hover-bg` (#F4F4F4). **Selected:** bg `--ado-row-select-bg` (#EFEFEF) + a **2px solid `#0078D4` left border** on focus/selection.
- Optional reviewed checkbox at row start/end; hover `View` affordance.

**Icons (no proprietary fonts — approximate with unicode or inline SVG)**
- **Folder** — MDL2 `FabricFolder`; approximate `📁` or a tabbed-folder SVG. Color `--icon-folder-color` (#DCB67A).
- **File** — MDL2 `Page`/`TextDocument`; approximate `📄` or an SVG rectangle with a folded top-right corner. Color `--icon-file-color` (#605E5C). Icon size `16px` (medium).

**Change-type badges** — small `12–16px` colored glyph/letter to the right of (or over) the filename; ADO additionally appends a lowercase text label.

| Type | Glyph | Text label | Color token |
|------|-------|-----------|-------------|
| **Add** | `+` (green) | `add` | `--ado-status-add` #107C10 |
| **Edit** | pencil / dot | `edit` | `--ado-status-edit` #0078D4 *(FLAG: sometimes neutral/amber `--ado-change-edit-amber`)* |
| **Delete** | `−` / `×` (red) | `delete` | `--ado-status-delete` #DA0A00 |
| **Rename** | `→` arrow | `rename, edit` | `--ado-status-rename` #605E5C *(FLAG low)* |

> Git treats a file with >50% change as a rename (default threshold, not configurable). New/deleted files open a single content pane, not a two-pane diff.

---

## Component inventory

Blazor components to build (top-down):

- **`PrPage`** — outer CSS grid: rows `[global-nav][pr-header][tab-bar][body]`; body row a 2-col grid `[filetree | splitter | diff]` with file-tree width bound to `--ado-filetree-width`.
- **`GlobalNavBar`** *(stub)* — top app strip (breadcrumb, search, account) + `HubRail` left icon rail.
- **`PrHeader`** — title + `#id`, `BranchIndicator` (source → target pills), `StatusPill`, author/reviewer `AvatarStack` with vote badges, primary split action button.
- **`StatusPill`** — Active / Draft / Completed / Abandoned colored pill.
- **`TabBar`** — Pivot: Overview / Files / Updates / Commits / (Conflicts); active 2px underline; count badges.
- **`FilesBody`** — hosts the file panel + splitter + diff area; owns layout-toggle state and selected-file state.
- **`FileTreePanel`** — resizable left panel; renders the tree; tree-root selection → summary view.
- **`FileTreeRow`** — chevron + indent, folder/file icon, name, `ChangeTypeBadge`, reviewed checkbox, hover View.
- **`ChangeTypeBadge`** — add/edit/delete/rename glyph + label + color.
- **`Splitter`** — 1px line / ~5px hit-area; drag rewrites `--ado-filetree-width`.
- **`SecondaryToolbar`** — `Side-by-side | Inline` `LayoutToggle`, `ChangesDropdown` (multi-select pushes), view options. Sticky.
- **`LayoutToggle`** — segmented Side-by-side / Inline control.
- **`DiffCard`** — per-file container; sticky `DiffCardHeader` + `DiffBody`; collapsible.
- **`DiffCardHeader`** — collapse chevron, file path, `ChangeTypeBadge`, layout/view controls, overflow `…` menu. `position: sticky`.
- **`DiffBody`** — switches between `SideBySideDiff` and `InlineDiff`; or `SingleContentPane` for add/delete files.
- **`SideBySideDiff`** — two synced `DiffPane`s (original/modified), aligned rows, diagonal-fill placeholders.
- **`InlineDiff`** — single column, dual gutter, `+`/`-` markers.
- **`DiffLine`** — `[ChangeBar][Gutter(s)][CodeCell]`; applies row tint + word-level spans + syntax tokens.
- **`ChangeBar`** — 3px green/red left stripe.
- **`Gutter`** — right-aligned tabular-nums line number, non-selectable.
- **`CodeCell`** — `white-space: pre`; renders syntax-highlighted tokens with word-level highlight spans.
- **`HunkExpandBand`** — grey collapsed-lines band: ellipsis/count + expand-up/down chevrons.
- **`LineCommentAffordance`** — hover comment bubble; multi-line selection comment; suggestion light-bulb (right side only).

---

## Open questions / low-confidence items

Verify these against a live `dev.azure.com` PR with DevTools (inspect computed styles on `.line-insert` / `.line-delete` / `.char-insert` / `.char-delete` / the margin decoration / `.monaco-diff-editor`), or by pulling `node_modules/azure-devops-ui/Core/core.css` + Header/Tabs/List SCSS:

1. **Diff add/remove backgrounds** (`--ado-diff-add-line-bg` #E6FFED, `--ado-diff-remove-line-bg` #FFEEF0) — *medium*. Proprietary VC renderer; GitHub-family stand-ins. Expect a low-alpha green/red (~0.10–0.20 over white) in reality.
2. **Word-level (intra-line) shades** (`--ado-diff-add-word-bg`, `--ado-diff-remove-word-bg`) — *low*. Expect same hue at higher alpha (~0.35–0.40).
3. **`+`/`-` marker colors** and **gutter-marker tints** — *low*.
4. **Change-bar colors** (#2DA44E / #CF222E) and exact width (assumed 3px) — *medium*.
5. **Diagonal-fill** value — *medium*; Monaco default `#22222233` used; ADO may flatten to `#F4F4F4`.
6. **Hunk band** colors (#F3F6FB / #57606A / #E1E4E8) — *low*; ADO may use neutral-4/6 instead of the blue-grey shown.
7. **Gutter bg** (#FAFBFC) vs neutral-2 (#FAF9F8) — *low*; pick one consistent off-white.
8. **Status badge colors for Edit and Rename** — *low*; Edit may be neutral or amber rather than blue; Rename may be blue rather than neutral.
9. **All chrome px dimensions** (header 64px, tab bar 44px, file tree 300px, splitter 1px, card header 40px, toolbar 40px, tree row 32px, gutter 48px) — *low/medium*. Derived from `azure-devops-ui`, not a fetched ADO stylesheet. Tune against a screenshot.
10. **Status PILL colors** (Draft/Conflicts/Auto-complete/Review-required/Policy) — from a community extension mirroring ADO conventions, *medium*; not pixel-exact ADO values.
11. **Borders** — `--border-subtle-color` renders as `rgba(0,0,0,0.08)`; the solid divider equivalent is somewhere in #E1DFDD–#E6E6E6 (neutral-8/10). Pick one and verify.
12. **Syntax token palette** — Monaco `vs` light defaults, *medium*; ADO may diverge per language grammar.