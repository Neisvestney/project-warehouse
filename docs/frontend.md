# Frontend Architecture

Shell-level architecture: stack, layering, routing, providers, cross-cutting conventions and the API client.
Only what the code cannot say — anything derivable by opening a file is deliberately absent.

Companion documents:
- [frontend-components.md](frontend-components.md) — reusable components and feature modules
- [frontend-state.md](frontend-state.md) — URL state, form hooks, MobX↔RHF bridge
- [frontend-realtime.md](frontend-realtime.md) — SSE stream, watches, presence, edit locks

## Tech Stack

| Tool | Version | Role |
|------|---------|------|
| React | 19 | UI framework |
| React Compiler | 1 | Automatic memoization (Babel pass, see below) |
| TypeScript | 7 (native Go compiler) | Type safety |
| Vite | 8 | Build tool + dev server |
| MUI (Material UI) | v9 | Component library |
| Emotion | 11 | CSS-in-JS (MUI peer) |
| React Router | v7 | Client-side routing |
| TanStack React Query | v5 | Server state management |
| MobX + mobx-react-lite | 6 | Local complex edit state (RolesSettingsPage, WarehouseEditPage) |
| @dnd-kit/core + sortable | — | Drag-and-drop (roles matrix, node tree, file lists) |
| @hey-api/openapi-ts | 0.97+ | OpenAPI → TypeScript codegen |
| notistack | 3 | Toast notifications |
| zxing-wasm | 3 | Barcode/QR decoding (WASM) |
| bwip-js | — | Barcode/DataMatrix rendering on `/print` |
| vite-plugin-pwa | 1 | PWA + service worker |
| Konva | — | Warehouse floor-plan canvas |
| sass-embedded | — | Sass support |
| use-double-tap | 1 | Double-tap gesture (camera focus on mobile) |

Build target is `chrome >= 49`. That is why a few modern CSS features (`aspect-ratio`) and browser APIs are
hand-rolled or feature-detected rather than used directly.

### React Compiler

React Compiler memoizes components automatically, in dev and in build alike. Manual `useMemo` / `useCallback`
are no longer the way to avoid re-renders — write the plain version and let the pass do it. Existing manual
memoization is kept as written (`react-hooks/preserve-manual-memoization`), so removing it is safe but not
urgent.

`eslint-plugin-react-hooks` carries the compiler's own rules, and its `recommended` set is what the project
lints against. **A lint error there means the compiler cannot safely optimize that component** — it is a
correctness signal, not style, and `eslint-disable` on those rules is the wrong fix.

The Babel peers (`@babel/core`, `@babel/plugin-transform-runtime`, `@babel/runtime`) are pinned to 7.x: npm
otherwise resolves them to 8.x and the install fails against the copy `@vitejs/plugin-legacy` already pulls in.

**MobX `observer` components opt out with a `"use no memo"` directive.** MobX relies on interior mutability:
the store reference is stable while its fields change, so the compiler does not see an observable read as a
dependency of the JSX cache. The component still subscribes and still re-renders, but returns the element
built during the first render — a permanently frozen subtree. The `react-hooks/incompatible-library` lint rule
does not detect this pattern, so the directive is the boundary, and it is required in every `observer` body:

```tsx
export default observer(function WarehouseCanvas() {
  "use no memo";
  ...
});
```

The line is drawn at reading observable fields, not at touching a store: a component that only passes a store
instance through context reads nothing observable and stays compiled.

The same interior-mutability trap applies to react-hook-form's `watch()` — use `useWatch({control, name})` in
components instead. That one the linter does catch.

### TypeScript tooling note

Two TypeScript packages are installed under aliases: `typescript` → `@typescript/typescript6` (because
`typescript-eslint`'s programmatic API declares a `typescript` peer range of `<6.1.0`), and the real TS 7
native compiler → `typescript-7`, used only for type-checking.

**Always type-check with `npm run typecheck`; never trust bare `tsc` / `npx tsc`.** Both aliased packages
declare a `tsc` bin, so `node_modules/.bin/tsc` resolves to whichever npm linked last — an install-order
artifact that can silently flip versions. `npx --package typescript-7 tsc` does not work either: npx resolves
`--package` by the package's own internal name, not the local alias key.

`strict` is `true` in `tsconfig.app.json` / `tsconfig.node.json`.

## Directory layering

`src/` is layered by *who is allowed to depend on whom*, not by file type. The directory listing itself is in
the repository; the rule is not.

- **`api/`** — generated from the backend OpenAPI schema (`npm run generate-api`). Never hand-edited, always
  committed. Everything else may import it; it imports nothing of ours.
- **`utils/`** — pure helpers with no React dependency (the one exception is `appEntityUtils.tsx`, which
  carries icons). Importable from anywhere, imports nothing above itself.
- **`services/`** — module-level singletons that must work *outside* the React tree (`apiClient`,
  `modalService`). Configured once from `main.tsx`.
- **`features/<domain>/`** — domain metadata and domain-specific presentational pieces that more than one page
  or component needs. This is where a domain's single source of truth lives (e.g. `catalog/catalogItemTypes.ts`);
  adding a backend enum value should mean editing exactly one file here.
- **`components/`** — reusable UI. Flat files for app-wide primitives, one subfolder per domain
  (`catalog/`, `files/`, `orders/`, `receipts/`, …) when a cluster of components belongs together. A component
  here must not import from `pages/`.
- **`pages/`** — route targets. A page's own subroutes live in a nested `pages/` folder beside it; a page's
  private components live beside the page file. **The operations tree must not import from the settings tree**
  and vice versa — shared pieces move down into `components/` or `features/` instead (see the label maps in
  `components/orders/marketplace/marketplaceOrderUtils`, deliberately duplicated away from
  `MarketplacesSettingsPage/marketplaceUtils`).
- **`layouts/`** — shells that host an `<Outlet />` or a `children` slot and own nothing domain-specific.
- **`contexts/`** — providers. The context object and its consumer hook live in a `*Context.ts` file separate
  from the `*Provider.tsx` component, so the provider file exports only components (react-refresh rule). The
  same split applies to `CatalogItemDrawerHost` / `CatalogItemDrawerContext`.
- **`hooks/`** — hooks used by more than one page. A hook used by exactly one page stays with that page.

## Routing

`BrowserRouter` in `main.tsx`. Pages are lazy-loaded via `React.lazy` + `Suspense`. Access control is handled
by `ProtectedRoute` / `ProtectedRoutes`; unauthenticated users are redirected to `/login`.

Each layout owns a `Suspense` boundary around its own `<Outlet />`, and `App.tsx` keeps an outer one for the
routes that have no layout. React picks the nearest boundary, so a suspending page chunk never reaches past
its layout: the app bar stays on screen, and — the reason the inner boundary in `MainLayout` matters —
`RealtimeProvider`'s effects are not torn down, which would drop and re-open the stream on every cold chunk.
All of them render `RouteFallback`.

The route configs are the source of truth for paths and permissions: `App.tsx` for top-level routes, and
`storageConfig.tsx` / `operationsConfig.tsx` / `settingsConfig.tsx` for the three `SidebarPage` modules
(`/storage/*`, `/operations/*`, `/settings/*`). `@/components/MainNav/mainNavConfig.tsx` joins them into the
top-level nav: the app bar links on desktop, and the expandable section tree of `MainNavDrawer` on mobile.

> **Convention:** subroutes carry no `requiredPermission` of their own — `SidebarPage` only gates the section
> route. Sub-pages that need a stronger right (`integrations.edit`, `integrations.map`, `integrations.sync`) hide their actions with
> `useHasPermission`, and the server enforces it regardless.

Two layouts nest inside each other. `MainLayout` is the shell every authenticated page shares — realtime
stream, service-worker update watcher, URL-synced state. `MainAppBarLayout` sits inside it and adds the visual
chrome: app bar and the page `Container`. `/scanner` and `/print` are children of `MainLayout` directly, so
they keep the stream and the shared providers but render full-bleed, with no app bar and no breadcrumbs.

## Cross-cutting conventions

### Date-only values

The API's `DateOnly` fields travel as `yyyy-MM-dd` strings. Never feed those to `new Date(...)` — the built-in
parser reads a bare date as **UTC midnight**, so anyone west of UTC sees the previous day. Use `parseDateOnly`
(or `formatDateOnly`) from `@/utils/dateOnly`, which builds the date in local time.

The mirror case is server-side day cutting: an endpoint that turns a `DateTime` into a day (statistics, the
calendar, the stock forecast) decides the zone itself. The client's part is one request interceptor in
`setupApiClient` that sends `X-Time-Zone: <IANA id>`, recomputed per request so a session left open across a
DST transition starts sending the new value on its own; the zone actually applied comes back on the response
as `timeZoneId`, and it is not always the one that was sent — a warehouse with its own zone wins. See
[stock-forecast-specification.md](stock-forecast-specification.md#резолвер-пояса).

### Inline error branches

A page fetching one entity by id renders `<NotFound />` for a 404 and `<QueryError error={…} />` for anything
else, and sets both `suppressGlobalError` and `suppressGlobalNotFound` on that query so the global modal does
not appear alongside the inline state. Error screens are shown **only on the initial load** — `isRefetchError`
is ignored, so a transient network blip does not replace visible data with an error screen.

```tsx
if (query.isError)
  return isNotFoundError(query.error) ? <NotFound /> : <QueryError error={query.error} />;
```

### `stripEphemeralSearchParams()`

Called in `main.tsx` before `mountApp()` (same slot as the existing `clear_server` cleanup). Deletes every
param listed in `EPHEMERAL_PARAMS` (`utils/ephemeralSearchParams.ts`) from the current URL via
`history.replaceState`, so they never survive a cold entry (F5, bookmark, pasted link) but are untouched by
in-app SPA navigation. Running before React mounts means the drawer doesn't flash open for a frame, and the
history entry is replaced rather than pushed.

### Номер отправления

`formatPostingNumber(postingNumber)` from `@/utils/postingNumberUtils` returns a `ReactNode` where the last 4
digits of the first segment are bold and slightly larger — that's the part warehouse staff actually reads off a
label. `0132298262-0184-1` → `013229**8262**-0184-1`, `43468002-0359-1` → `4346**8002**-0359-1`, `1234567890` →
`123456**7890**`. Strings that don't start with at least 4 digits are returned unchanged; `null`/`undefined`/`""`
give `null`.

Used everywhere a posting number is rendered: the `postingNumber` extra column in `OrdersFbsPage`, the
**Отправление** row in `OrderMetaSection`, and the failure list in `SkippedOrdersList` (there it sits inside the
existing `<b>`, so the whole number stays bold and the 4 digits only gain the size bump).

### Invalidating by operation

Generated query keys are one object — `[{_id, baseUrl, path?, query?}]` — so a filter built from a subset of
those fields partially matches every variant of an operation, including paginated ones whose `query` differs
per page. `byOperation(id, match?)` from `@/utils/queryKeys` builds it:

```typescript
void queryClient.invalidateQueries({
  queryKey: byOperation("marketplacesGetSyncRuns", {path: {id: accountId}}),
});
```

### Downloading a generated file

`utils/downloadUtils.ts` has the two ways of handing a generated file to the browser:

- `saveBlob(blob, fileName)` — `createObjectURL` plus an anchor with `download`. The save mechanism, and the
  only thing that works on native: the WebView cannot render a PDF inline, so the file goes to the system app
  ([native-client.md](native-client.md)).
- `reserveBlobTab()` — for a document the user just looks at and prints. It returns `{show, close}`: `show`
  puts the blob into the tab, `close` drops it when the file never arrived. Call it **synchronously from the
  click**, before the request — a `window.open` after an `await` has no user gesture behind it and gets blocked.
  The reserved tab is `about:blank` on this origin, so it gets a spinner page written into it while the request
  runs instead of sitting empty.
  It falls back to `saveBlob` on `Capacitor.isNativePlatform()` and when the popup is blocked anyway, so call
  sites need no platform branch of their own. The object URL is never revoked: the tab reads it for as long as
  it stays open, and closing the tab frees the blob.
- `withTimestamp(fileName)` — `labels.pdf` → `labels-2026-08-28_14-05-09.pdf`. Wrap the name before handing it
  over so repeated saves land as separate files instead of `labels (1).pdf`.

Binary endpoints are called through the generated SDK function directly with `parseAs: "blob"` (the
generator's response types for binary responses are unreliable — same reason as `useFileBlobUrl`). That makes
the **error** body a Blob too, so `resolveErrorMessage` cannot read it: unwrap it with
`parseProblemFromBlob(error)` from `utils/blobErrorUtils.ts` first.

### Telemetry

OpenTelemetry traces and logs, shipped to `/api/telemetry/*` under the existing JWT. The design and the
server side live in [observability-specification.md](observability-specification.md); what matters at a call
site is which module to import.

| Module | Depends on | Use it for |
|--------|-----------|------------|
| `hooks/useOperationMutation.ts` | `@opentelemetry/api` | A mutation that *is* a warehouse operation — the common case |
| `services/withOperationSpan.ts` | `@opentelemetry/api` | An operation that sends more than one request |
| `services/telemetryLogs.ts` | `@opentelemetry/api-logs` | Emitting a log record; installing the global error capture |
| `services/telemetry.ts` | the whole Web SDK | Nothing — it is loaded once from `main.tsx` and imported nowhere else |
| `services/currentPage.ts` | nothing | Nothing — `TelemetryRouteLogger` feeds it, the three modules above read it |
| `services/currentUser.ts` | nothing | Nothing — reads `user.*` off the access token for the three modules above |

The first two are in the initial bundle and are safe to import from any component. The third is ~70 KB gzip
and is pulled in by a dynamic `import()` after the first render, so **never import it statically** — that
undoes the split and puts the SDK back in the critical path.

Both light modules work before the SDK has loaded, which is the point of the split: `trace.getTracer()`
without a registered provider returns a no-op and runs the callback untouched, and log records wait in a
100-entry ring buffer until the exporter is attached.

`fetch` is instrumented automatically, so the generated `@hey-api` client and the SSE wrapper need no
wrapping — a request made anywhere already carries `traceparent` to the backend. What is not automatic is the
**operation** a request belongs to:

```tsx
const mutation = useOperationMutation(
  "order.self_assign",
  {...ordersBatchSelfAssignMutation(), meta: {suppressGlobalError: true}, onSuccess},
  (variables) => ({"order.count": variables.body?.orderIds.length ?? 0}),
);
```

An operation that sends several requests calls the helper directly and spreads `op` into each of them —
including into the variables of an existing mutation, which are themselves request options:

```tsx
await withOperationSpan("receipt.post", {"document.id": id}, async (op) => {
  await uploadScan({body: form, ...op});
  await postReceiptMutation.mutateAsync({path: {id}, ...op});
});
```

Rules that are not obvious from the code:

- **`op` is what puts a request inside the operation.** A call that does not get it still works, but its span
  lands in a trace of its own. Parallel calls sharing one `op` are fine — the context travels as a value, so
  there is nothing to race over.
- **`mutateAsync`, never `mutate`.** `mutate()` returns nothing and does not wait, so the span would end
  before anything was sent. The hook exists precisely because it wraps `mutationFn` instead.
- **Never swap the client's `fetch` for the duration** (`client.setConfig({fetch})`). That is ambient state:
  with `staleTime: 0` and refetch-on-focus, any background query that starts inside the window would attach
  itself to the operation.
- **One span per user action, not per business process.** Handing an order to assembly and finishing it are
  two actions minutes apart on different screens; tie them together with a shared attribute
  (`assembly_task.id`), not with one long-lived span.

Errors are handled by the helper — it marks the span `ERROR`, records the exception and rethrows, so the
calling code knows nothing about telemetry beyond the operation name. Operation spans are `SpanKind.CLIENT`.

Every span and every log record also carries `app.page`, the screen the user was on, and `user.id` /
`user.name`, taken from the access token — the same names the server writes, so one filter finds both sides
of a trace. Both are stamped centrally, from `services/currentPage.ts` and `services/currentUser.ts`, and
need nothing at the call site.

### `pluralUtils`

Russian pluralization of counters. Forms are picked by `Intl.PluralRules("ru-RU")`, not by hand-rolled
`% 10` / `% 100` arithmetic.

```ts
plural(n, forms)        // → one form: "задания"
pluralCount(n, forms)   // → "2 задания" (the number via toLocaleString("ru-RU"))
```

`PluralForms` is `{one, few, many}`: `one` — 1, 21, 31…; `few` — 2-4, 22-24…; `many` — 0, 5-20, 25-30…

CLDR puts fractional values in the `"other"` category, which `PluralForms` does not have — `plural()` folds it
into `few`, because «1,5 задания» is the correct Russian. Do not replace this with `forms[category]`
indexing: that silently returns `undefined`.

`NOUNS` is the shared dictionary of nouns **in the nominative case** (`task`, `item`, `position`, `itemType`).
Add a word here rather than to a component when a counter is reused.

The forms are arbitrary strings, so in phrases that require verb or adjective agreement, decline the whole
phrase:

```ts
pluralCount(n, {
  one: "позиция будет удалена",
  few: "позиции будут удалены",
  many: "позиций будут удалены",
});
```

A word in an oblique case (e.g. after «для» — «для 2 **заданий**») does **not** go into `NOUNS`, which holds
nominative forms only. Declare such forms as a constant next to the call site (see `TASKS_GENITIVE` in
`BatchAssemblyDialog.tsx`). Abbreviations — `шт.`, `поз.`, `комп.`, `м`, `мин` — are left alone; they do not
decline.

#### Pluralization inside error templates

Error texts are strings in `errorCodeMessages` / `errorCodeArgMessages`, where there is nowhere to call
`pluralCount`, so the forms are declared inside the placeholder itself and expanded by `interpolateArgs`:

```ts
"{count}"                              // → "3"           — the value as-is
"{count:заказа|заказов|заказов}"       // → "3 заказов"   — pluralCount with one|few|many
```

The grammatical case lives in the template, not in the dictionary: «для 1 **заказа**» and «1 **заказ**» are
different forms of one word, and `NOUNS` (nominative) cannot supply oblique ones. The forms after the colon are
arbitrary strings, so a whole phrase can be made to agree.

`hasAllArgs` reads the key name from the same regex, so the detailed variant of a message is still enabled only
when the server sent every argument. A malformed directive (fewer than three forms) degrades to the bare value
rather than crashing the render. Abbreviations (`симв.`) still need no pluralization — only whole words decline.

## Pages

Only pages whose behaviour cannot be read off the file are described here. Everything else is a
list/create/detail page built from the standard parts: `PageGenericHeader` + `SearchInput` + `FiltersBar` +
`DataTableContainer`, with state in URL params via the hooks in [frontend-state.md](frontend-state.md).

### User pages

`UsersPage` / `UserViewPage` / `UserCreatePage` / `UserEditPage` (under `/settings/employees`) and
`MyProfilePage` (`/profile`) are the standard list + detail + form triple over the same entity; they differ only
in which id they read and which permission gates them (`users.view`, `users.create`, `users.edit_profile`, and
`users.manage_roles_and_permissions` for the roles/permissions block of the edit form). Two details are not
obvious from the files: `UserEditPage` refetches on window focus with `keepDirtyValues: true`, so a background
refresh never overwrites unsaved edits, and both password dialogs (`ChangePasswordDialog` — self-service in
`MyProfilePage`, admin reset in `UserViewPage`) disable backdrop-click dismissal while the mutation is pending.

`HomePage` renders its navigation cards from `AppEntity[]` returned by `/api/home`, resolved through
`resolveEntity` from `appEntityUtils` — adding a card is a backend change, not a frontend one.

### `PrintPage`

Print-ready label sheet generator at `/print`. Reads `?item=TYPE:VALUE|LABEL` query params (repeatable, batch)
and renders a grid of barcode/datamatrix labels. Supported types: `DataMatrix`, `EAN13`, `Code128`, `QR`. Uses
`bwip-js` for canvas rendering.

Query param format: `TYPE:VALUE` or `TYPE:VALUE|LABEL` — pipe separates value from an optional human-readable
label shown above the barcode. The value may contain colons (e.g. URLs).

Items are loaded from the URL once into local state on mount; the list is **not** reactive to subsequent URL
changes. This allows removing individual labels before printing without navigating away. Each label card has a
floating **×** `IconButton` that removes it, hidden via `@media print`.

Print layout is controlled by `PrintSettings` (also hidden on print):
- **Preset selector** — built-in presets (A4 4×7, A4 2×5, A5 2×4, Термо 58мм) plus user-saved custom presets in
  `localStorage` under `print-page-presets`; the last selected preset is restored from `print-page-last-preset`.
- **Manual fields** — label width/height (mm), columns, gap, page padding, label padding. All use `NumField` —
  the input can be cleared while focused and only snaps to the minimum on blur.

`@page { margin: 0 }` is injected globally via `GlobalStyles` so browser default print margins are removed and
`pagePaddingMm` (CSS `padding` on the page container) is the sole source of page margins. `labelPaddingMm` adds
inner padding to each `BarcodeLabel` in both preview and print. For 1D barcodes the bwip-js bar height is
calculated from the **unpadded** label height so the rendered canvas resolution stays fixed as padding changes —
only the CSS `maxHeight` constraint shrinks.

To open the print page programmatically use `openPrintPage(items)` from `@/utils/printUtils`.

Example URL: `/print?item=DataMatrix:ABC123|Товар А&item=EAN13:5901234123457&item=Code128:HELLO&item=QR:test`

#### Barcode payload format

Barcodes printed for app entities carry an entity tag so a scanner can tell what was scanned. Built with
`formatEntityBarcode(entity, id)` from `@/utils/barcodeUtils` and read back with `parseEntityBarcode(raw)` →
`{entity, id} | null`.

Format: `pw:<entityCode>:<guid>` — `storagePlaceNode` → `spn`, `catalogItem` → `ci`.

Parsing is strict: an untagged bare GUID is **not** accepted.

### `ReceiptPage`

Detail page for a single receipt (`/operations/receipts/:id`). Shows receipt metadata with an inline edit form
(PATCH) and status action buttons. Body section is `ReceiptItemsSection` — one collapsible card per item
showing planned/received counts and a placements table.

**Status transitions rendered as action buttons based on `receipt.status`:**
- `draft` → **Запланировать** + **Редактировать состав** (opens `ReceiptItemsEditorDrawer`) + **Удалить**
- `planned` → **Начать приёмку** + **Редактировать состав** + **Вернуть** + **Отменить**
- `processing` → **Завершить** + **Вернуть** + **Отменить** (Вернуть/Отменить disabled if any placements exist)
- `finished` → **Вернуть в обработку**
- `canceled` → read-only, no actions

`receivedCount` is editable only in `processing` (PATCH `.../received-count`), and placements can only be
deleted there.

### `StocktakePage`

Detail page (`/operations/stocktakes/:id`) whose **body swaps with the status**, because the three phases are
genuinely different screens:

- `planned` / `draft` → `StocktakeNodesSection` — the scope. Cells are added via the shared `SelectNodeModal`
  and removed through a `ConfirmDialog` that warns when counted lines would be discarded. Every change PUTs the
  full id list. A cell already in another stocktake's scope is accepted here — the overlap is only rejected
  against a running count, by `POST /start` and by scope edits on an already-started document. Both 422s
  surface through the snackbar.
- `inProgress` → `StocktakeCountingSection` — one `StocktakeNodeAccordion` per cell plus **Показать расхождения**.
- `finished` / `canceled` → `StocktakeResultSection` — read-only, rendered from `appliedDelta` and never from
  live stock.

Action buttons by status: `draft` and `planned` both get **Начать** (`POST /start`) / **Отменить** / **Удалить**;
`draft` additionally gets **Запланировать** (`POST /schedule`) when the type is `scheduled` and a planned date is
set, and `planned` gets **Вернуть в черновик** (`POST /to-draft`). **Начать** and **Запланировать** are disabled
until at least one node is in scope; `inProgress` → **Завершить** (opens `StocktakeDifferencesDialog`) /
**В черновик** / **Отменить**; terminal → none. All mutations return the full `StocktakeDto` and are written
into the cache with `setQueryData`.

**`StocktakeNodeAccordion` (the counting editor).** Cell stock is fetched lazily (`enabled: expanded`) from
`GET /api/stocktakes/{id}/nodes/{nodeId}/stock` — the stocktake-owned endpoint, so counting needs no warehouse
permissions.

Displayed rows are **derived, never stored in an effect**: `useMemo` merges live stock with the already-saved
lines, then applies three pieces of local state — `edits` (per-row overrides), `added` (surpluses), `removed`
(keys). A refetch therefore refreshes the baseline without discarding what the operator typed, and `dirty` is
simply "any of the three is non-empty". `buildDraftRows` (in `stocktakeDraft.ts`) implements the merge: every
live position defaults to *counted = expected* so only discrepancies need touching, and saved lines with no live
counterpart are appended — those are surpluses entered earlier.

Standard rows use `ClampedIntegerField` with an explicit **`min={0}`** — the component defaults to `min = 1`,
which would make a zero count impossible to enter. Unit rows are a «Найден» checkbox (unchecked ⇒
`countedQuantity = 0`). Only rows with `expected === 0` can be deleted; pre-populated rows are set to zero
instead, so "искали — нет" stays an explicit finding.

Each accordion saves independently (`PUT .../nodes/{nodeId}/items`), so two operators can count different cells
without clobbering each other.

**`StocktakeDifferencesDialog`** is the only path to `POST /finish`. It renders `GET /{id}/differences`: totals,
a per-cell table with a «Что будет сделано» column, `missingFromDocument` rows highlighted and labelled
«нет в документе — будет списано», and a `problems` block that disables the finish button. This is deliberate —
the cell-is-authoritative rule is destructive by omission and must never be applied blind.

### `StockMovementsPage`

Pivot table of stock movements at `/storage/stock-movements`. Filter state lives in URL params via
`useStockMovementsFilters` (`?items=` comma-separated catalog item ids, `?from=`, `?to=`, `?warehouse=`,
`?place=`, `?node=`, `?user=`, `?actions=`, `?transfers=`); ids are resolved back into DTOs by
`useCatalogItemsByIds`.

**Catalog item selector** — `CatalogItemsSelect` restricted to `STOCK_MOVEMENT_ITEM_TYPES` (`standard` + `unit`;
groups, variations and bundles never hold stock). The selected items are what the pivot columns are made of, so
the clear icon is disabled.

**«По тегу» button** — opens `AddItemsByTagDialog`: pick one or more tags (`CatalogTagsFilter`), the dialog
queries `GET /api/catalog/for-select` with `tagIds` + `types` + `take=200`, previews the match count and appends
the found ids to the current selection (duplicates skipped). It's a one-shot action — the tag itself is not
persisted in the URL. A warning is shown when the result hits the 200-item cap.

The applied time zone sits in the tooltip of an `InfoOutlinedIcon` beside the page title («Сутки считаются по
часовому поясу Europe/Moscow»). It comes from `timeZoneId` on the pivot response and is not necessarily the
one the client sent: a warehouse with its own zone outranks the caller's, so the figures would otherwise
shift for a viewer elsewhere with nothing on screen to explain it. The icon stays rendered while the pivot
reloads — a caption that came and went with every request moved the whole page under the cursor.

### `StockForecastPage`

«Прогноз остатков» at `/storage/forecast`, requires `statistics.view` or `statistics.view_assigned` (the
endpoint additionally judges warehouse access itself). A paginated table of how long the stock of one
warehouse lasts; the warehouse is mandatory and lives in `?warehouse=`, the rest of the state in
`?search=`, `?types=`, `?tags=`, `?archived=`, `?warnings=`, `?sortBy=`, `?sortOrder=`, `?page=`,
`?pageSize=` through `useSyncedWithQueryState` / `useDebouncedSyncedWithQueryState` / `usePaginatedParams`.
The table itself is `ForecastBasePage`; `WarehouseForecastPage`
(`/storage/warehouses/:id/forecast`, reached from the «Прогноз остатков» button of `WarehouseViewPage`) is the
second wrapper around it and drops the warehouse filter, the way `WarehouseInventoryPage` does for «Остатки».
Columns, sorting, the settings dialogs and `StockForecastChip` are described in
[frontend-components.md](frontend-components.md#forecastbasepage).

### `CatalogPage`

Paginated, searchable catalog item list requiring `catalog.view` or `receipts.process_assigned`. Clicking a row
opens `CatalogItemDrawer`; the selected id lives in `?item=` (see the drawer-param convention in
[frontend-components.md](frontend-components.md#catalogitemdrawer)).

An empty type selection **disables the list query** — the server cannot express "no types match", so the page
renders the empty state locally.

### `ItemsBasePage` and the inventory-scope pages

`components/inventory/ItemsBasePage.tsx` is the whole inventory table: search, type/tag/archive filters,
pagination, drawers. It takes `warehouseId?`, `storagePlaceId?`, `nodeId?` as scope constraints, and the
warehouse filter `Select` is shown **only** when `warehouseId` is absent. The type filter is limited to
`PHYSICAL_CATALOG_ITEMS` (`standard` + `unit`).

The name cell is a `CatalogItemLink` that opens `CatalogItemDrawer` (it `stopPropagation()`s, so the row's own
click handler doesn't fire). Clicking a `unit` row opens `UnitItemsDrawer` — a bottom drawer paginating the
individual `UnitInventoryItem` instances of that catalog item inside the same scope; other types do nothing.
Both drawers use `useDrawerSearchParamsState` with the params `"catalogItem"` and `"unitCatalogItem"`.

Four pages are thin wrappers around it and differ only in which ids they forward and which names they fetch for
the breadcrumbs: `InventoryPage` (`/storage/inventory`, no ids — warehouse filter visible),
`WarehouseInventoryPage` (`/storage/warehouses/:id/inventory`, `warehouseId`), `StoragePlaceInventoryPage`
(`/storage/warehouses/:warehouseId/storage-places/:storagePlaceId/inventory`, + `storagePlaceId`) and
`NodeInventoryPage` (`.../nodes/:nodeId/inventory`, all three). All four require `warehouses.view` or
`warehouses.view_assigned`.

### `WarehouseViewPage` / `WarehouseEditPage`

`WarehouseViewPage` renders a pan/zoom Konva canvas of storage place rectangles (`WarehouseCanvas` from
`features/warehouse/`). Clicking a place opens `StoragePlaceDrawer` (`?storagePlace=`), a wide right drawer
holding a `StoragePlaceNodeTree`; selecting a leaf node offers a link to that node's inventory page (disabled
for nodes that have children — only leaves hold stock).

**"Этикетки"** fetches `GET /api/warehouses/{id}/print` and calls `openPrintPage` with every node as a
`DataMatrix` label (value = `formatEntityBarcode("storagePlaceNode", node.id)`, label = the full path joined by
` / `). `StoragePlaceDrawer` has its own button printing only that place's nodes in the same format.

**"Редактировать ячейки"** (inside the drawer) toggles tree edit mode: the tree switches from
`StoragePlaceNodeTree` to `SortableNodeTree`, each row gains add-child / rename / delete actions and a drag
handle, and reorder calls `PUT .../nodes/reorder`. All operations update the nodes query cache in place from the
returned flat list.

`WarehouseEditPage` (`/storage/warehouses/:id/edit`) combines its **own** Konva canvas component — distinct
from the read-only variant in `features/warehouse/` — with a metadata form, driven by a MobX
`WarehouseEditStore` provided through context. It passes `isDirty: true` to `useEditLock` unconditionally,
because the canvas holds unsaved layout from the moment it opens.

`WarehousesPage` itself has **no permission guard** — it is reachable by any authenticated user; the server
scopes what is listed.

### `StorageSettingsPage` (Хранилище)

`/settings/storage`, requires `system.view`. A shell with two tabs kept in `?tab=` via
`useSyncedWithQueryState`; each tab fetches only when it is shown, so the DB catalog query is not paid for by
someone looking at files.

**`FilesTab`** — read-only view of `GET /api/system/storage`: a row of counter cards (файлов, общий объём, кэш
превью, не привязано), a determinate `LinearProgress` for disk usage that turns warning above 75 % and error
above 90 %, a by-content-type table with an inline bar per row, and the ten largest files — their names open
`FileViewerModal`.

**`DatabaseTab`** — `GET /api/system/database`: counters (размер БД, из них таблицы, индексы, строк), then one
expandable row per `AppEntityType` listing the tables inside it. Labels and icons come from `entitiesTypes` in
`utils/appEntityUtils.tsx` rather than a second list — that `Record<AppEntityType, …>` is exhaustive, so a new
entity type fails `typecheck` until it is named. `unknown` is rendered as «Прочее». Row counts are planner
estimates and print «—» when a table has never been analysed; never present them as exact.

> **Design decisions — do not undo these without a reason.**
> - **No chart library, and none should be added for this page**: four numbers do not justify the dependency at
>   the `chrome >= 49` build target, and a table with inline bars reads better than a pie chart anyway.
> - **There is deliberately no «run the collector now» button** — collection is scheduled, and the orphan card
>   says when files go. Disk figures are cached server-side; the page shows «по состоянию на …» from
>   `diskStatsAt`.

> Not to be confused with `StoragePage` («Места хранения»), which is about warehouse storage places.

### `MarketplacesSettingsPage` / `MarketplaceAccountCreatePage`

The account list at `/settings/integrations` sorts on **Магазин** and **Синхронизация** only — the backend
`MarketplaceAccountSortBy` accepts nothing else. **Подключить магазин** requires `integrations.edit`.

The create form at `/settings/integrations/new` offers Ozon only for now, and **has no name field**:
`MarketplaceAccount.Name` comes from the marketplace's own seller info and is overwritten by every sync; until
the first run the server stores a `Ozon ••••1234` placeholder. An inline `Alert` says so, otherwise the missing
field reads as a bug.

`TestConnectionButton` probes the credentials before the record exists (the route id is ignored when the body
carries an `apiKey`). On submit the server enqueues the first sync itself when the account is active, so the
client does not call `/sync` after creating.

> **Note:** `input[type=number]` hands RHF a string. Numeric fields are coerced with `Number(...)` at submit —
> the API rejects `"30"` for an `int`.

### `AutoMapRulesPage`

Auto-mapping rules at `/settings/integrations/auto-map-rules`, reached from **Правила автосопоставления** in the
account-list header. The rules are global — one set for every shop — so the page is a sibling of the account
list, not a tab on an account. The route is declared **before** `:id` in `settingsConfig.tsx`, otherwise the
dynamic segment swallows it.

One unpaginated table ordered by **Приоритет** descending — the order the backend applies them in, highest first. The **Активно** switch
saves through the same `PUT` as the dialog, sending the row unchanged apart from `isEnabled`. A rule whose
target got archived carries an **Архив** chip — the backend skips it until it is repointed. Everything that
mutates requires `integrations.map`; without it the page is read-only.

`AutoMapRuleDialog` serves both create and edit (`rule === null` means create) and picks the target with the
shared `CatalogItemsSelect`, restricted to `standard | unit | bundle | variation`. An invalid regular expression
comes back as a 422 on `value` and lands on the field through `useRhfApiErrors`.

Collaboration follows `RolesSettingsPage`: the rules are one versioned object, so `useEditLock` claims them
under `marketplaceAutoMapRules` with an empty guid, `EditLockBanner` and `StaleDataBanner` sit above the table,
and `AppBreadcrumbs` carries `viewersOf` for the same key. An open dialog counts as `isDirty`, so a concurrent
save warns instead of silently reloading under the form.

**The claim waits for intent.** `enabled` is gated on a sticky `hasEditIntent` flag raised by opening either
dialog or flipping the **Активно** switch — most visitors only read the page, and claiming on arrival would
lock them all out of each other. Watching and the staleness warning run from the first render regardless; only
the `acquire` call is deferred.

### `MarketplaceAccountPage`

Account shell at `/settings/integrations/:id`. Header shows the sync status chip plus **Синхронизировать**
(a `Menu` picking scope: Всё / Склады / Карточки, requires `integrations.sync`), **Изменить** and **Удалить**
(both `integrations.edit`).

Four tabs — **Обзор**, **Склады**, **Карточки**, **История** — live on a single route with the active tab in
`?tab=` (see the tabbed-page convention in [frontend-state.md](frontend-state.md)). The Склады and Карточки
tabs are hidden unless the account's `capabilities` declare them; a `?tab=` pointing at a hidden tab falls back
to Обзор. Only the active tab is mounted, so background tabs hold no queries.

While `lastSyncStatus === "running"`, the account query and the run-history query poll at 3 s and stop on their
own afterwards. The realtime stream is the primary channel here — the page subscribes with `useEntityWatch` and
this polling is the fallback for when no subscription is confirmed (see
[frontend-realtime.md](frontend-realtime.md)).

Tabs:
- **Обзор** — connection details, seller details (юрлицо, ИНН, ОГРН, форма собственности), synced-data
  counters, `SyncErrorAlert` for `lastSyncError`, and a hard error alert when `credentialsUnreadable` (the Data
  Protection key ring was lost and the key must be re-entered).
- **Склады** — sortable table with an inline `WarehousesSelect` per row saving on change; unmapped rows carry a
  warning icon, the Seller API status renders as a `WarehouseStatusChip`. `?archived=` toggles archived
  warehouses.
- **Карточки** — image, название, артикул, цена, обновлена, SKU, позиция каталога, `CardMappingChip`. Filters
  in URL (`?search=`, `?mappingState=`, `?archived=`); **`mappingState` defaults to `unmapped`** because that is
  the working list. **Сопоставить автоматически** runs account-wide auto-mapping and reports «Сопоставлено N,
  требует ручного разбора M». Clicking a row opens `CardMappingDialog` (requires `integrations.map`); a mapped
  row's catalog cell is a `CatalogItemLink` opening `CatalogItemDrawer` (the tab is wrapped in
  `CatalogItemDrawerHost`, drawer state in `?catalogItem=`). The thumbnail is a `CardImage` — opens the
  full-size marketplace image in a new tab.
- **История** — run history; rows carrying an error expand into a `SyncErrorAlert`.
  `MarketplaceSyncStatus.canceled` is reserved and never produced by the current backend.

## Providers (in `App.tsx`)

```
ServiceWorkerContext.Provider
  └── ThemeProvider (MUI)
        └── SnackbarProvider (notistack)
              └── ModalProvider
                    ├── QueryErrorHandler    (self-closing — global query error handler)
                    ├── TelemetryRouteLogger (self-closing — logs router transitions)
                    ├── CssBaseline          (self-closing — global CSS reset)
                    ├── UpdatePrompt
                    └── AuthProvider
                          └── Suspense
                                └── ProtectedRoutes
                                      └── Routes (lazy pages)
                                            └── MainLayout
                                                  └── RealtimeProvider
                                                        ├── ServiceWorkerUpdateWatcher
                                                        └── SearchParamsProvider
                                                              └── Suspense
                                                                    ├── MainAppBarLayout
                                                                    │     └── app bar + Container + Suspense
                                                                    ├── /scanner
                                                                    └── /print
```

`RealtimeProvider` sits in `MainLayout` rather than next to `AuthProvider`: the stream is only useful for an
authenticated user, and `MainLayout` covers every authenticated route including `/scanner` and `/print`.

`AuthProvider` fetches `/me` with `suppressGlobalError` and clears the entire query cache on logout.

## Build output and caching

Chunk filenames carry a content hash and the service worker precaches them, so the build config is
tuned so that an ordinary change invalidates as few chunks as possible.

**Reproducible dependency resolution.** `ProjectWarehouse.Server/Dockerfile` copies
`package-lock.json` alongside `package.json` and installs with `npm ci`. A lock-less `npm install`
re-resolves every `^` range at image build time; a different rolldown or Vite build changes chunk
boundaries and with them every hash, which wipes the whole service worker cache. With the lock in
place two independent installs produce byte-identical chunk names — on Linux and Windows alike.

**Vendor chunks** (`vendorGroups` in `vite.config.ts`). Packages tagged `$initial` — statically
reachable from the entry — are split per package family: react, emotion, mui, mui-x, mui-icons,
query, mobx, dnd, capacitor, plus a catch-all `vendor`. A group also claims the dependencies of the
modules it captures, so `priority` has to place each package ahead of its dependents: react and
emotion before mui, mui before the icon packages. Bumping one dependency then invalidates only its
own chunk.

The `$initial` tag is what keeps the initial payload small. Packages reached only through
`React.lazy` routes stay inside the route chunk; giving them vendor groups of their own makes them
statically reachable from the entry and adds them to the `modulepreload` set — konva, bwip-js and
`@mui/x-scheduler` alone amount to ~1.2 MB of first-load traffic.

**Shared app chunks** (`sharedGroup`). Modules under `api`, `components`, `configuration`,
`contexts`, `features`, `hooks`, `layouts`, `plugins`, `services` and `utils` that are used by two
or more chunks (`minShareCount: 2`) each get a chunk of their own, named `app-<path>`. Otherwise a
shared component is inlined into every route chunk importing it, and editing one header rewrites
every page.

A hash change still cascades into importers, because a chunk embeds the filenames it imports —
touching a widely used component rewrites its own chunk plus the entry and the routes that reference
it. That cascade is the floor; the config removes the duplication on top of it.

## PWA

`vite-plugin-pwa` with `registerType: "prompt"` — the user is prompted before an SW update, not auto-updated.

Workbox caching strategy:
- All static assets → precached (`globPatterns: ["**/*"]`)
- `*-legacy-*.js` → excluded from the precache via `globIgnores`, served by a `CacheFirst` runtime
  route (`legacy-assets`, 60 entries / 30 days). `@vitejs/plugin-legacy` emits a full second copy of
  every chunk, and a browser only ever executes one of the two builds; precaching both would double
  the download for every client. Legacy targets (`chrome >= 49`) still support service workers, so
  they populate that runtime cache on first load and keep working offline.
- `/api/*` → `NetworkOnly` (never cached)

Manifest: name "Project Warehouse", theme `#1976d2`, standalone display.

### Update checks

The browser fetches `sw.js` when the worker is registered and on navigations within scope. Client-side
routing is not a navigation, so a tab that stays open — the normal state of a warehouse terminal — would
otherwise never learn that a release shipped. Three triggers cover that, all going through
`checkForServiceWorkerUpdate()` in `services/serviceWorkerUpdate.ts`, which shares one 60 s throttle
across callers. The throttle window opens only after a check that actually reached the network, so a
failure on a flapping connection does not cost the next trigger its turn; concurrent callers join the
check already in flight rather than starting a second one:

- **A realtime reconnect after an outage.** `ServiceWorkerUpdateWatcher`, mounted inside
  `RealtimeProvider`, subscribes to `onReconnectedAfterOutage` — see
  [frontend-realtime.md](frontend-realtime.md#outage-detection). The client is published into the server
  image, so a frontend release restarts the server and drops every stream; the reconnect is the first
  moment the new bundle is known to be served.
- **A 30-minute interval, plus becoming visible** (`usePeriodicUpdateCheck`, called in `App.tsx`) for tabs
  the reconnect trigger cannot reach — `/login`, `/scanner` and `/print` all live outside the realtime
  provider. The visibility half carries the weight: a hidden tab has its timers throttled to about one
  firing a minute, a frozen one runs none at all, and a machine returning from suspend collapses every
  missed period into one late firing, so the interval is least trustworthy exactly when a tab has been
  sitting idle the longest.
- **A React crash.** `ErrorBoundary` checks with `force: true`, bypassing the throttle, and skip-waits
  straight into the new worker if one installs.

A check that finds new bytes installs the worker and raises `needRefresh`, which is what `UpdatePrompt`
renders. Checks are cheap: an unchanged `sw.js` costs one small request and does nothing.

### `InstallPrompt` / `UpdatePrompt`

PWA lifecycle UI. `InstallPrompt` is driven by the `beforeinstallprompt` event (captured in
`utils/useInstallPrompt.ts`) and is surfaced on `HomePage` alongside the offline-ready indicator.
`UpdatePrompt` is mounted globally in `App.tsx` and calls `updateServiceWorker()` from `ServiceWorkerContext`
when a new SW version is waiting.

Every variant — both plaques and the modal dialog — is hidden under `@media print`, so an update that arrives
while the user is at `Ctrl+P` never lands on the sheet.

The plaques are positioned by `useFloatTop`, which returns a CSS `max(...)` expression built around the
`--app-bar-height` custom property. `MainAppBarLayout` sets that property on `documentElement` while it is
mounted and removes it on unmount, so a route without an app bar (`/login`, `/scanner`, `/print`) falls back
to the declared `0px` and the plaque sits at the top of the viewport. The property is the only channel
available: `UpdatePrompt` is mounted above the router in `App.tsx` and cannot see the layout tree. The height
itself comes from `MAIN_APP_BAR_HEIGHT`, exported by `MainAppBar` and also used for its own `Toolbar`.

The installing plaque floats over the page and carries nothing to click, so it fades out and lets clicks
through while the pointer is on it, via `useFadeOnHover`. The plaque shown after "Отложить" keeps its
pointer events: its button is the only way left to apply an update the user has already deferred.

The hook is JavaScript rather than a `:hover` rule because `pointer-events: none` removes the element from
hit testing, which unapplies the very rule that set it. It watches `pointermove` on the document to know when
the pointer has left the rect, restores on `pointerleave` when the pointer leaves the window entirely, and
falls back to a 2 s timer for a non-mouse pointer, which delivers an enter and nothing after it.

## Dev Proxy

Vite (`vite.config.ts`) proxies `/api/*`, `/openapi/*` and `/scalar/*` to the backend at
`https://localhost:7095`. This means frontend code can call `/api/auth/login` without CORS or hardcoded URLs.

## Path Alias

`@` → `./src`. Import as `import foo from "@/utils/qrTools"`.

## API Client

The TypeScript client is auto-generated from the backend's OpenAPI schema using `@hey-api/openapi-ts`.

### Regenerating

```bash
# Backend must be running first
npm run generate-api
```

Reads from `https://localhost:7095/openapi/v1.json` (the dev cert TLS check is bypassed for the CLI only).
Outputs to `src/api/`. Generated files are committed to git.

### Runtime setup

`setupApiClient()` is called once in `main.tsx` before `ReactDOM.createRoot`. It:
- Sets `baseUrl` to `/api` (the Vite proxy routes this to the backend)
- Installs a request interceptor that proactively refreshes the JWT access token when < 30 s of its lifetime
  remains
- Installs a response interceptor that on a 401 attempts a refresh and retries the request. If no `accessToken`
  is in `localStorage` the response is passed through without a refresh attempt (avoids spurious refreshes on
  unauthenticated requests). Stored tokens are cleared when the refresh token is also invalid.

Tokens live in `localStorage`, written only through `storeTokens()` / `clearTokens()` in
`services/apiClient.ts` — the expiry timestamp is stored alongside the tokens so the proactive refresh above
needs no JWT decode on every request. Call `storeTokens(tokenResponse)` after a successful login (the auth
context does this) and `clearTokens()` on logout. Each dispatches a window event — `auth:tokens` and
`auth:clear` — which is how code outside the React tree learns that authentication changed.

The proactive refresh itself is `getFreshAccessToken()`, exported from the same module: it returns a token
that is valid for at least the next 30 seconds, refreshing first if it is not. The request interceptor is one
caller; the telemetry exporters, which send their own requests, are another. Anything that builds an
`Authorization` header by hand goes through it rather than reading `localStorage` directly.

Because the bearer token is injected by the request interceptor, **anything the browser fetches by URL
attribute cannot be authorized** — that is why images go through `FileImage` rather than `<img src="/api/…">`.

### Using generated hooks

```typescript
import {useQuery} from "@tanstack/react-query";
import {getApiAuthMeOptions} from "@/api/@tanstack/react-query.gen";

const {data, error} = useQuery(getApiAuthMeOptions());
```

```typescript
import {useMutation} from "@tanstack/react-query";
import {postApiAuthLoginMutation} from "@/api/@tanstack/react-query.gen";

const login = useMutation({
  ...postApiAuthLoginMutation(),
  onSuccess: (data) => storeTokens(data),
});
```

`staleTime` is 0 app-wide: every active query refetches on window focus and on reconnect. Several conventions
in this codebase (the staleness banner, the absence of refetch-on-reconnect handling in the realtime provider)
depend on that.
