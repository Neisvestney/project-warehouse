# Backlog

Work that is understood, measured and deliberately not done yet. Every entry names the reason it is
blocked and the concrete event that unblocks it — an entry without a trigger belongs in an issue, not
here. When the trigger fires, do the work and delete the entry.

## Enable `build.chunkImportMap`

**What it buys.** A chunk embeds the file names of the chunks it imports, and those names carry
content hashes, so editing one shared module rewrites every chunk that references it — see the
cascade note in [frontend.md](frontend.md#build-output-and-caching). `build.chunkImportMap` makes
chunks import stable placeholder names and inlines a `<script type="importmap">` into `index.html`
that redirects each placeholder to the real hashed file. The importer's bytes stop depending on its
dependencies' hashes, and the cascade disappears.

Measured on this repo with Vite 8.2.2, changing one CSS line in `components/PageGenericHeader.tsx`:

| | current | with `chunkImportMap` |
|---|---|---|
| chunks invalidated | 16 of 180 | 2 of 180 |
| modern bytes re-downloaded | 668 KB | 1 KB |
| initial payload | 1859 KB | 1859 KB |

The legacy build is covered. `@vitejs/plugin-legacy` emits its own
`<script type="systemjs-importmap">`, and it guards the modern path with an inline `data:` module
that throws when `import.meta.resolve` is missing — browsers without it never set
`__vite_is_modern_browser` and fall through to the SystemJS entry.

**Why it is off.** [vitejs/vite#23225](https://github.com/vitejs/vite/issues/23225), open and absent
from the 8.2.2 changelog. `vite:build-import-analysis` substitutes `__VITE_PRELOAD__` during
`generateBundle`, after rolldown has computed content hashes, so a graph change that only alters a
chunk's `__vite__mapDeps` list changes the file's bytes while leaving its hashed name intact. The
entry chunk here references `__vite__mapDeps` in twelve places.

That lands harder on this app than on most. 123 of 134 precache entries carry `revision: null` —
Workbox keys them by URL alone and never re-fetches a file whose name did not change. With an import
map in play, a stale `mapDeps` entry names a placeholder that is absent from the current map, so the
dynamic import of a route fails rather than merely preloading the wrong thing.

Forcing Workbox to compute a `revision` for every precache entry would neutralise the bug for
service-worker clients, but it does nothing for the first visit, for HTTP caches, or for a CDN in
front of the app, all of which still trust the file name. Not worth carrying that asymmetry.

**Trigger.** [vitejs/vite#23278](https://github.com/vitejs/vite/pull/23278) merged and released. Then
set `build.chunkImportMap: true` in `vite.config.ts` and confirm on a fresh build that every import
specifier in the modern chunks resolves through `importmap.json`, that `index.html` carries both the
`importmap` and `systemjs-importmap` tags, and that the `modulepreload` set is unchanged.

## Drop the `"use no memo"` opt-outs on MobX components

**What it buys.** React Compiler memoizes all of `src` except the `observer` components in
`RolesSettingsPage` and `WarehouseEditPage`, which opt out with a `"use no memo"` directive — see
[frontend.md](frontend.md#react-compiler). Removing the directives puts the warehouse floor-plan
editor and the roles matrix, the two heaviest interactive screens in the app, under the same
memoization as everything else.

**Why it is off.** MobX relies on interior mutability: the store reference stays the same while its
fields change, so the compiler never records an observable read as a dependency of the JSX cache. The
component keeps its subscription and still re-renders, and still returns the element built on the
first render — the subtree freezes permanently. `react-hooks/incompatible-library` does not detect the
pattern, so nothing but the directive stands between the compiler and a silently dead canvas.

[mobxjs/mobx#3874](https://github.com/mobxjs/mobx/issues/3874) tracks this upstream. Open since May
2024, labelled `✋ on hold`, no assignee — the bindings are not being adapted, so the directives are
the permanent arrangement rather than a stopgap.

**Trigger.** Either mobx-react-lite ships a compiler-compatible subscription API and #3874 closes, or
`rolesStore` / `warehouseEditStore` move to `useSyncExternalStore` with immutable snapshots, which the
compiler reads correctly. Then delete the directives and confirm with a Babel probe that those files
emit `_c(n)` cache slots, and by hand that the canvas still repaints on every store mutation —
the failure mode is a frozen subtree, and no test in the repo covers it.

## Drop empty `MarketplaceSyncScanJob` runs from traces

**What it buys.** `SyncScanCron: "0 * * * * ?"` wakes the job once a minute, and in the vast majority
of runs it finds no account due for a sync. That is roughly fifteen hundred traces a day, each one a
single span over a single `SELECT`. In the file archive they eat rotation budget; in the dashboard
they bury the trace list, so every incident starts with scrolling past noise.

**Why it is off.** By design the filtering belongs in the collector's `filter/noise` (see
[observability-specification.md](observability-specification.md#сэмплинг)), and there is nothing
there to filter on: `OpenTelemetry.Instrumentation.Quartz` records the job name, the trigger and the
duration — no attribute says "did nothing". Duration is not a usable proxy either, since an empty
pass and a pass with one fast account differ by milliseconds.

**Trigger.** The job setting an attribute of its own on the current span — say
`app.sync.accounts_scanned` — right after it selects candidates. The collector rule is then a single
line (`attributes["app.sync.accounts_scanned"] == 0`) and ships by restarting one container. Adding
the attribute ahead of this task buys nothing on its own.
