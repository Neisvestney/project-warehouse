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
