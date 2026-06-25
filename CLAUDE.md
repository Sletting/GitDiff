# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Branch Review** (`PrDiffViewer`) is a local developer tool: a **hosted Blazor WebAssembly** app (.NET 10) that diffs two branches of a local Git repo and renders them in the style of Azure DevOps' Pull Request → Files view (file tree, side-by-side/inline diffs, change bars, word-level highlights, syntax highlighting, expandable hidden context). The browser can't read the filesystem, so all Git work happens server-side via **LibGit2Sharp** and is delivered to the WASM client over `/api`.

## Commands

```bash
# Run (dev): builds & serves the WASM client + API from one host
dotnet run --project src/PrDiffViewer.Server          # http://localhost:5124  (https://localhost:7063)

# Build / restore the whole solution
dotnet build PrDiffViewer.slnx

# Produce a deployable build (copies the client's wwwroot + _framework into the host output)
dotnet publish src/PrDiffViewer.Server -c Release

# Format (no custom ruleset / .editorconfig is checked in — this applies default conventions)
dotnet format PrDiffViewer.slnx
```

- **No test project exists** in this solution — there is no `dotnet test` target to run.
- **Linux/ICU:** every csproj sets `InvariantGlobalization=true`, but on a box without `libicu` the CLI host itself also needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` set in the environment, or `dotnet` aborts with an ICU error. (This sandbox has **no .NET SDK installed**; install one before building.)

## Architecture

### One process, three projects

This is a **single Kestrel host**, not a client + a separate server. `PrDiffViewer.Server` references `PrDiffViewer.Client` and serves its build output; there is no client launch profile.

| Project | Role |
|---|---|
| `PrDiffViewer.Server` | ASP.NET Core host. Serves the WASM payload **and** the `/api` REST endpoints. All LibGit2Sharp logic lives here. |
| `PrDiffViewer.Client` | Blazor WASM UI (runs in the browser). Calls `/api`, renders the diff. |
| `PrDiffViewer.Shared` | The wire contract: DTOs (`DiffModels.cs`) **and** two diff algorithms (`SideBySide`, `WordDiff`) used by *both* sides. |

### Server request pipeline (`Server/Program.cs`)

Middleware order is **load-bearing**: `UseBlazorFrameworkFiles()` → `UseStaticFiles()` → `MapGitApi()` (the `/api` group) → `MapFallbackToFile("index.html")` **last**. The SPA catch-all must stay last so it never shadows API routes or static assets. There is **no CORS** config — client and API share one origin, by design; don't add `AddCors`/`UseCors`.

- **Minimal API only** — no MVC controllers, no Swagger. Endpoints are three `MapGet` lambdas in `Server/Api/GitEndpoints.cs` (`/branches`, `/diff`, `/lines`), each taking `GitDiffService` as a trailing injected parameter.
- `GitDiffService` is a **singleton** but holds no state: every method does `using var repo = Open(...)` and disposes it, because LibGit2Sharp `Repository` objects are not thread-safe.
- **Error contract:** throw `GitServiceException` for expected, user-facing failures → HTTP 400 with `ApiError { Message }` shown verbatim. Any other exception → HTTP 500; the raw message is attached to `ApiError.Detail` **only in Development** (`includeErrorDetail` is wired from `IsDevelopment()`). Wrap every endpoint body in `Guard(...)` to get this shaping.

### The diff domain model

`DiffMode` mirrors Azure DevOps PR semantics and is the central concept:
- **`MergeBase`** (three-dot, **default**) — diffs the source tip against the *merge base* of source and target: what the source branch introduced. The query param is the literal string `mergeBase`.
- **`Direct`** (two-dot, query string `direct`) — diffs target tip against source tip.
- Mode only changes the **diff base** (`oldCommit`); `newCommit` is always the source tip. The **commit list is always the three-dot set** (in source, not in target) regardless of mode.

Pipeline on the server: `GitDiffService.GetDiff` runs `repo.Diff.Compare<Patch>` (rename detection on; `context` clamped 0–100, default 3) → `UnifiedDiffParser` turns each file's unified-diff text into structured `DiffHunk`/`DiffLine` → `AnnotateWordDiffs` runs `SideBySide.Build` per hunk and calls `WordDiff.Annotate` on each modified delete/add pair. **Word-level highlight segments are computed server-side and ship pre-baked in the JSON** — the client never word-diffs.

### Client rendering pipeline & state ownership

State is **layered, not centralized** — flows strictly parent→child by parameter and child→parent by `EventCallback`. No DI store, no cascading values.

- `Pages/Home.razor` — the **single data orchestrator**. Owns `_branches`, `_diff`, and the setup↔PR toggle. The only place (besides `DiffCard`) that injects `PrApiClient`.
- `PrView.razor` — owns the active tab (Overview/Files/Commits; defaults to Files).
- `FilesView.razor` — owns file-review UI: the built tree, selected path, the **Side-by-side/Inline toggle**, the splitter width.
- `DiffCard.razor` — owns **per-file** render state (`_blocks`, collapse). Keyed by file path so state survives re-renders.

Diff-arrives-to-rows: `DiffSummary` → `FileTree.Build` (left panel) + per-file `DiffBlocks.Build` → ordered `LinesBlock` (real lines) / `GapBlock` (collapsed context) → `SideBySideDiffView` (re-runs `SideBySide.Build` to lay out the grid) or `InlineDiffView` → `CodeContent` per line, which runs `SyntaxHighlighter.Tokenize` + wraps changed `WordSegment`s in `w-add`/`w-del` spans.

**Lazy context expansion** is a second data path that bypasses `Home`: grey `HunkBand` buttons → `DiffCard.Expand*` → `PrApiClient.GetLinesAsync` → `DiffBlocks.ToContextLines` → splice into `_blocks`.

`HttpClient.BaseAddress` is `HostEnvironment.BaseAddress` (from `<base href="/">` in `index.html`); `PrApiClient` issues **relative** URLs (`api/diff`, …) so the client always hits its own host. Don't hardcode an API URL.

## Invariants — change one side, you break the other

- **All wire DTOs live in `Shared/Models/DiffModels.cs`.** Changing a property changes the client's contract. Add transport types there, never in Server.
- `ChangeKind`/`DiffLineKind` cross the wire as **JSON integers** (System.Text.Json default) — **reordering enum members is a breaking wire change**. `DiffMode` is the exception: it's sent by name in the query string (`mergeBase`/`direct`), mapped manually in both `GitEndpoints` and `PrApiClient`.
- DTOs must stay **parameterless-constructible** (plain mutable classes) for round-tripping — don't convert to positional records / `required`-init without checking the `ReadFromJsonAsync` path.
- `SideBySide.Build` is the **single source of truth for delete/add alignment**, run identically on server (to pick word-diff pairs) and client (to lay out the grid). Don't re-implement pairing.
- `DiffLine.Content` carries **no** leading `+`/`-`/space marker (that meaning is in `DiffLineKind`). `OldLineNumber`/`NewLineNumber` are nullable for add-only/delete-only lines. **All line numbers are 1-based.**
- `DiffLine.Segments`: `null` means "render the whole line" (context/unpaired/too-dissimilar/over-length); non-empty means "highlight the `Changed` runs". `CodeContent` relies on this exact distinction.
- `WordDiff.Annotate` **mutates** its two `DiffLine` args and is meaningful only on a Deleted+Added pair. Keep heavy diffing on the server — don't move it into WASM render code.
- Client API calls go through `PrApiClient`, which returns `Result<T>` and never throws into the UI — branch on `IsSuccess`/`Error`. Route new endpoints through its `GetAsync<T>` helper to keep the `{message,detail}` parsing.
- `FileTree.CardId(path)` is the only DOM-id scheme linking a tree leaf to its diff card (`'/'`→`'-'`, `'.'`→`'_'`, `file-` prefix). The lone JS interop, `window.prDiff.scrollToId` in `wwwroot/js/app.js`, depends on it.
- `DiffBlock` is a sealed hierarchy pattern-matched in **both** `SideBySideDiffView` and `InlineDiffView` — a new block type needs a branch in both. `GapBlock.Count == null` means an open-ended tail gap to EOF.
- Icons are inline-SVG `MarkupString`s in `Diff/Icons.cs` (no `<img>`/external assets) for the WASM/CSP context.
- Large files (>1000 changed lines) start collapsed; their blocks build lazily on first expand.
- Everything assumes `InvariantGlobalization`: timestamps use `ToString("o")`, parsing uses `CultureInfo.InvariantCulture`.

### Known quirk

`FileDiff.Similarity` is always `0` even though rename detection is enabled — the field exists on the DTO but is never populated.

## Design reference

`docs/azure-devops-pr-design-spec.md` is the implementation spec the UI targets (layout, color tokens, typography, diff-rendering rules from `azure-devops-ui` + the Monaco `vs` light theme). The CSS variables in `wwwroot/css/app.css` mirror those tokens; `SyntaxHighlighter` is a deliberately best-effort, language-agnostic approximation of that theme.
