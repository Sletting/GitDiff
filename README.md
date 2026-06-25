# Branch Review

A local web app that shows the diff between two branches of a Git repository, presented in the
style of the **Azure DevOps Pull Request → Files** review experience: a file tree, side-by-side and
inline diffs with line numbers, change bars, word-level highlights, syntax highlighting, and
expandable hidden context.

It is a **hosted Blazor WebAssembly** application:

| Project | Role |
|---|---|
| `PrDiffViewer.Client` | Blazor WebAssembly UI (runs in the browser) — the ADO-style PR view |
| `PrDiffViewer.Server` | ASP.NET Core host — serves the WASM app and exposes the Git REST API |
| `PrDiffViewer.Shared` | DTOs and the diff model shared by client and server |

The server reads the repository with **LibGit2Sharp**, computes the diff (merge-base / PR semantics by
default), and the client renders it. The browser can't touch the filesystem, so all Git work happens
on the server and is delivered to the WASM client over `/api`.

---

## Prerequisites

- **.NET SDK 10.0+** (`dotnet --version`).
- On Linux without ICU installed, run with invariant globalization (see below). On Windows/macOS this
  is not needed.

## Run

```bash
dotnet run --project src/PrDiffViewer.Server
```

Then open the URL it prints (e.g. `http://localhost:5xxx`).

> **Linux without libicu:** if `dotnet` fails with a libicu/ICU error, the projects are already set to
> `InvariantGlobalization`, but the CLI host itself also needs it:
> ```bash
> DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet run --project src/PrDiffViewer.Server
> ```

## Use

1. Enter the **path to a local Git repository** (any folder inside the repo works) and click
   **Load branches**.
2. Pick a **source** branch (the one with the changes) and a **target** branch (what it would merge
   into).
3. Choose the diff type:
   - **Changes since merge base (like a pull request)** — three-dot diff, what the source introduced
     since it diverged from the target. *(default, matches ADO PRs)*
   - **Direct branch-to-branch differences** — two-dot diff between the two tips.
4. Click **Compare changes**.

In the result view:
- **Files** tab — file tree on the left; click a file to jump to its diff. Toggle **Side by side /
  Inline**. Click the **⌃ / ⌄ / ⇕ Expand all** controls in a grey band to reveal hidden context.
- **Commits** tab — commits the source has that the target doesn't.
- **Overview** tab — a summary of the comparison.

---

## REST API

| Endpoint | Description |
|---|---|
| `GET /api/branches?repo=PATH` | List branches in the repository |
| `GET /api/diff?repo=PATH&source=BR&target=BR&mode=mergeBase\|direct&context=3` | Full diff between branches |
| `GET /api/lines?repo=PATH&commit=REF&path=FILE&start=N&count=M` | A slice of a file (for expanding context) |

Errors come back as `{ "message": "...", "detail": "..." }` with a 4xx/5xx status.

## Design reference

`docs/azure-devops-pr-design-spec.md` is the implementation spec the UI targets (layout, color
tokens, typography, and diff-rendering rules derived from the open-source `azure-devops-ui` palette
and the Monaco light theme). The CSS variables in `wwwroot/css/app.css` mirror those tokens.

## Notes & limitations

- This is a **local developer tool**: it reads whatever local repository path you point it at. Don't
  expose the server to untrusted networks.
- Rename detection is enabled (Git's default 50% similarity); on very small files Git's heuristic can
  pair files in surprising ways — this reflects Git itself, not a bug in the diff rendering.
- Very large diffs render without virtualization; extremely large files may be slow.
- Syntax highlighting is a lightweight, language-agnostic approximation of the Monaco `vs` light theme.
