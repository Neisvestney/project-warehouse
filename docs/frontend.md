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
by `ProtectedRoute` / `ProtectedRoutes`; unauthenticated users are redirected to `/login`, which carries the
whole current URL — path, query and hash — in `location.state.from` and returns them there after the login.
Page state lives in the query string, so a deep link survives the round trip intact.

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

### Checking permissions

One predicate answers the question everywhere: `hasPermission(granted, required, mode?)` from
`@/utils/permissions`. `required` is a single `PermissionName`, a list of them, or nothing; `mode` is `"any"`
(default) or `"all"`. **An absent or empty requirement means the route or item is open** — `requiredPermission`
is a filter, and an empty filter removes nothing, which matters because the route configs are generated.

`useHasPermission(required, mode?)` is the same function reading the user out of `AuthContext`; the places that
already hold a permission list — `sectionVisibility.ts`, `mainNavConfig.tsx` — call the pure function with it.
Nothing re-implements the check inline.

`useHasWarehousePermission(all, assigned, warehouseId)` is the warehouse-scoped form, and it is what gates any
UI acting on a single warehouse-bound entity: the unscoped permission passes everywhere, the `_assigned` one
only when `warehouseId` is in the user's `assignedWarehouseIds` (from `/api/auth/me`, on `AuthContext`). It
mirrors the server's `WarehouseScopedRule` exactly, so a button it shows is a request the server accepts.
Pass the id straight off the loaded entity — `undefined` (nothing loaded yet) reads as "no access", so the
control stays hidden rather than flashing. The plain `useHasPermission(["x.edit", "x.edit_assigned"])` is for
places with no single warehouse in hand, such as a «создать» button above a list.

`useIsAssignedToWarehouse(warehouseId)` is the assignment half on its own, for operations the server binds to
the warehouse for *everyone* — assembly is the case: picking physical stock needs the assignment even from a
holder of the unscoped `orders.edit`, so `OrderPage` composes it as `hasAssemblePermission && isAssigned`.
Do not express that as an empty `all` list to `useHasWarehousePermission`: an empty requirement means "open to
all" in `hasPermission`, and the check would pass for everyone.

A component whose controls hit two endpoint families with different requirements takes **two props**, one per
family — `AssemblyTaskAccordionItem` splits `canEditTask` (reassign, delete — `orders.edit` family) from
`canTransitionStatus` (advance, roll back, undo a fulfillment — warehouse-bound for everyone). Collapsing them
into one flag with `||` shows buttons that are certain to answer 403, whichever way the caller leans.

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

### Таблицы на узких экранах

A dense `Table` — a chip column, an editable quantity field, an action icon — does not fit a phone: the row
either overflows the `Paper` or squeezes every column to one word. Such a table renders a card list instead
below `theme.breakpoints.down("sm")` (`useMediaQuery`), one outlined `Paper` per row: the identity of the row
(link plus type chip) on top, the numbers as `caption` label + value pairs underneath, the row action as an
icon in the top-right corner. The row's own click target (a drawer, a details view) moves onto the card, so
every interactive control inside it stops propagation, exactly as the cells did.

The same breakpoint turns an inline "add" form (`direction="row"` select + quantity + button) into a column,
otherwise the select collapses to a few characters. `ReceiptItemsSection` and the order page's
`OrderComponentsTable` / `OrderBoxesSection` are the worked examples.

### Inline error branches

A page fetching one entity by id renders `<NotFound />` for a 404 and `<QueryError error={…} />` for anything
else, and sets both `suppressGlobalError` and `suppressGlobalNotFound` on that query so the global modal does
not appear alongside the inline state. Error screens are shown **only on the initial load** — `isRefetchError`
is ignored, so a transient network blip does not replace visible data with an error screen.

```tsx
if (query.isError)
  return isNotFoundError(query.error) ? <NotFound /> : <QueryError error={query.error} />;
```

### Номер отправления

`formatPostingNumber(postingNumber)` from `@/utils/postingNumberUtils` returns a `ReactNode` where the last 4
digits of the first segment are bold and slightly larger — that's the part warehouse staff actually reads off a
label. `0132298262-0184-1` → `013229**8262**-0184-1`, `43468002-0359-1` → `4346**8002**-0359-1`, `1234567890` →
`123456**7890**`. Strings that don't start with at least 4 digits are returned unchanged; `null`/`undefined`/`""`
give `null`.

Used everywhere a posting number is rendered: the `postingNumber` extra column in `OrdersFbsPage`, the
**Отправление** row in `OrderMetaSection`, and the failure list in `SkippedOrdersList` (there it sits inside the
existing `<b>`, so the whole number stays bold and the 4 digits only gain the size bump).

### Resolving catalog ids

`hooks/useCatalogItemsByIds(ids)` turns a set of catalog item ids into `CatalogItemSelectDto`s and returns
`{items: Map<string, CatalogItemSelectDto>, isLoading, isError}`. Use it wherever a component holds ids and
has to show items — a selection restored from a URL, the members of a variation, the value of a single-value
picker. It posts `POST /api/catalog/for-select/by-ids`, so a hundred ids cost one request rather than a
hundred; the ids are deduplicated and sorted before they become the query key, so the same set in a different
order is a cache hit, and anything past the server's 500-id cap is split into further requests.

An id the server does not know is simply missing from `items` — that is not an error, and callers render
their own placeholder for it (`StockMovementsPage` shows «…» until the name arrives).

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
- `processing` → **Авто приёмка** + **Завершить** + **Вернуть** + **Отменить** (Вернуть/Отменить disabled if any placements exist)
- `finished` → **Вернуть в обработку**
- `canceled` → read-only, no actions

`receivedCount` is editable only in `processing` (PATCH `.../received-count`), and placements can only be
deleted there.

**Авто приёмка** opens `AutoAcceptDialog` — the confirmation for `POST /api/receipts/{id}/auto-accept`. It
takes the warehouse default node from `useDefaultStorageNodeQuery` and previews the plan built by
`buildAutoAcceptPlan(items)` in `receiptUtils.ts`, which mirrors the server rule: how many Standard items are
touched, how many of them get a received count, and how many pieces land in the cell. Two groups the server
skips get their own warnings — serialised items that still need a cell, and items already placed beyond their
target. Confirm is disabled when the warehouse has no default cell or nothing needs doing, and a failed
default-node request reads as a failed request rather than as "no cell assigned". The mutation's pending state
is lifted to the page so the status buttons cannot fire while the receipt is being rewritten.

`ReceivedCountInput` holds its edit as a `draft` that is `null` while the field is untouched, falling back to
`item.receivedCount`. That is what lets an outside write — auto-accept — reach an already-mounted input; keying
the component by the count would do the same but yanks focus out of the field on Enter-to-save.

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

### `OrdersAssemblyPage` — диалоги фулфилмента

Сборщик фиксирует источник позиции в двух диалогах — `AddFulfillmentDialog` (одна позиция задания) и
`BatchAssemblyDialog` (массовая сборка). Оба собраны из общих кусков в папке страницы:

| Файл | Что держит |
|------|------------|
| `ComponentRow.tsx` | строка компонента — маркер, имя, `× N`, чип типа, след выбора, зона управления, рельса вложенности; плюс `UnfilledCounter` |
| `FulfillmentControls.tsx` | зоны управления: `NodeControl` (ячейка, остаток), `UnitPicker` (экземпляр), `VariantPicker` (вариант) |
| `FulfillmentTree.tsx` | рекурсивное дерево состава Bundle: слот на каждый компонент, отчёт наверх `(entries, status)` |
| `compositionPicks.ts` | `CompositionPicks` — плоская карта выборов дерева по путям слотов, и контекст, через который слоты её читают и пишут |
| `VariationChain.tsx` + `variationOptions.ts` | цепочка вариаций и её лист (`chainLeaf`) — то, по чему реально двигаются остатки |
| `fulfillmentStatus.ts` | `SlotStatus` (`filled`/`total`), из которого растут все «N из M», и `isShort` — вердикт «остатка не хватает» |
| `stockGuard.ts` | `IgnoreStockContext` + `useIsShort`: тот же вердикт, но его снимает окружающий диалог |
| `todoRegistry.ts` | реестр незаполненных строк: счётчик в футере прокручивает к самой верхней |

**Строка компонента одна для всех типов.** Товар, экземпляр, вариация и вложенный комплект различаются только
зоной управления под именем; тип подписан чипом, а не отдельной вёрсткой. Форма строки зависит от состояния, не
от типа: заполненная схлопывается в хвост своей же строки (путь ячейки, остаток, «Изменить»), незаполненная
разворачивает зону ввода. Незаполненность помечается красным кантом при обычном фоне — заливка остаётся за
настоящими ошибками. Вариация с листовым вариантом остаётся одной строкой: выбор уходит в след
(`→ Кожа → Синий`), ячейка листа — в хвост; вариант, оказавшийся комплектом или вариацией, открывает рельсу с
подписью узла.

**Выбор состава живёт выше дерева.** `BundleTree` — контролируемый: он не хранит выбранное, а получает
`picks` и `onPicksChange` от диалога. Ключ выбора — путь слота вниз по дереву (`componentId`, а на каждой
вариации ещё и id выбранного варианта), значение — только то, что выбрал человек: ячейка, экземпляр, вариант.
`entries` и `status` остаются производными и по-прежнему текут наверх через `onChange`, потому что считаются из
каталога и остатков. Отсюда два следствия. Дерево можно свернуть и развернуть — слоты поднимутся из `picks` и
отчитаются теми же `entries`. Смена варианта забывает ветку под ним целиком (`withBranchReplaced` сносит все
пути с префиксом старого): выбор, сделанный для «Кожа → Синий», не может утечь в «Ткань → Синий».

**Прогресс.** Каждый слот отдаёт наверх `SlotStatus`, комплект суммирует статусы своих компонентов. Отсюда чип
`1 из 3` на неготовой ветке, счётчик «состав не заполнен: N» у кнопки и блокировка отправки (см.
[orders-specification.md § Полнота комплекта](orders-specification.md#полнота-комплекта)).

**Остаток ячейки.** `useNodeItemCount` даёт количество позиции в выбранной ячейке, `needTimes` умножает
потребность на число собираемых копий состава — комплектов за один заход в одиночном диалоге, заданий группы в
массовой сборке. Известная нехватка держит слот незаполненным — сервер всё равно откажет — и подписывается чипом
`остаток N — на M не хватит`. Вердикт слот берёт из `useIsShort`, а не напрямую из `isShort`: значение
`IgnoreStockContext` его снимает, и слот считается заполненным при любом остатке. Контекст поднимает только
массовая сборка (dev-переключатель ниже); везде ещё он `false`, то есть ограничение действует.

**Частичная сборка — норма.** Диалог не требует закрыть позицию за один заход: количество (или «Комплектов
сейчас») по умолчанию равно остатку, рядом кнопка «Все N», а кнопка отправки называет, сколько уйдёт прямо
сейчас — «Добавить 2 из 4». Прогресс в шапке показывает собранное сплошной заливкой, а добавляемое — буфером;
буфер отражает намерение, а не готовность формы, иначе полоса скачет, пока грузятся дерево и остаток.

**Массовая сборка.** Группы (`catalogItemId + warehouseId`) — строки таблицы: позиция, итог, источник, статус.
Статусный чип называет, что мешает («нужна ячейка», «нужен вариант», «задать состав»), футер считает неготовые
группы. Строка раскрывается по клику в любое место, кроме зоны выбора ячейки, и показывает панель
состава, который задаётся один раз и копируется на каждое задание. Копий ровно столько, сколько штук в
«Итого»: фулфилмент-комплект засчитывается как одна штука, поэтому заданию, которому нужно два комплекта, уходит
два одинаковых состава. Ячейку простой группы выбирают прямо в строке; всё остальное,
включая ячейку вариации, разрешившейся в товар, — в панели состава, а строка только показывает, где это стоит.
Состав группы живёт в её `GroupState.picks`, поэтому свернуть строку и вернуться к ней безопасно; смена варианта
в `VariationChain` группы, наоборот, обнуляет `picks` вместе с ячейкой и статусом — лист цепочки задаёт другой
комплект.
`failedItems` из ответа раскладываются по строкам своих групп, диалог остаётся открытым: группы после
инвалидации сжимаются до недобранного, так что повтор отправит только то, что не прошло.

**Ошибки ответа.** Нехватка остатков приходит отдельным списком `insufficientInventoryErrors` — свёрнутым по паре
«товар + ячейка» (см.
[orders-specification.md § Ошибки в ответе `batch-fulfill`](orders-specification.md#ошибки-в-ответе-batch-fulfill)) —
и рисуется одним алертом «Не хватило остатков» под таблицей, через общий `resolveErrorMessage`. По строкам групп
раскладываются `failedItems` с любым кодом, кроме `insufficientInventory`: он уже посчитан в сводке, и построчная
копия только дублировала бы её.

Итог отправки называет снекбар, считая фулфилменты запроса, а не позиции таблицы (комплект уходит копиями, см.
выше). Считается сохранённое, а не отправленное: без ошибок — `success` «Собрано N»; с ошибками при
`allowPartialSuccess` — `warning` «Собрано M из N, с ошибкой — K»; с ошибками без него — `error` «Партия откачена
целиком», потому что в базе не осталось ничего. При полном успехе диалог закрывается, и снекбар — единственное,
что о нём сообщает.

**Флаги отправки.** Чекбокс «Сохранять успешные позиции при ошибках» — это `allowPartialSuccess` запроса, по
умолчанию снятый: подпись под ним называет действующий режим («любая ошибка откатит всю партию целиком» либо «что
удалось собрать — останется собранным»). Рядом, только под `import.meta.env.DEV`, переключатель «dev: не
блокировать сборку при нехватке остатков» — он поднимает `IgnoreStockContext` над содержимым диалога, так что
слоты с нехваткой перестают держать кнопку. Чип остатка при этом остаётся красным: он показывает склад как есть,
а не то, что разрешено отправить. Сервер отказ по остаткам всё равно вернёт — переключатель нужен, чтобы дойти до
этого ответа на тестовых данных.

Ниже `sm` таблица разбирается на карточки, диалог идёт `fullScreen`, а состав группы занимает отдельный экран со
стрелкой назад — внутри строки на телефоне он не живёт.

### `StockMovementsPage`

Pivot table of stock movements at `/storage/stock-movements`. Filter state lives in URL params via
`useStockMovementsFilters` (`?items=` comma-separated catalog item ids, `?from=`, `?to=`, `?warehouse=`,
`?place=`, `?node=`, `?user=`, plus the display params `?preset=` and `?full=1`); ids are
resolved back into DTOs by `hooks/useCatalogItemsByIds`. `preset` and `full` stay out of the `filter` object, so
switching a preset or expanding the table does not change the pivot query key.

**Columns are groups.** Each catalog item spans one sub-column per metric of the active preset, then two
fixed ones: «Итого движение» (the raw net) and «Остаток». The first group is «Итого» — the sum of the
selected positions, computed server-side by summing the same columns, so it always adds up to what is on
screen. Metric labels are set vertically (`writing-mode: vertical-rl` plus a 180° rotation), which is what
fixes `METRIC_ROW_HEIGHT`. `stickyHeader` pins every `th` at `top: 0`, so the second header row carries an
explicit `top` — the height of the first row, measured with a `ResizeObserver` rather than assumed. The group
row holds a name over an article and `height` on a `th` is only a minimum, so a hardcoded offset parks the
metric row over the group row and the column borders visibly merge as soon as the table scrolls. A thick left
border opens a group, a thin one separates metrics inside it.

**Metrics** are named filters — an action set, a direction set and a receipt-tag set, ANDed, all optional.
The value is a signed net, so a metric restricted to `out` reports `−45` without the client deciding a sign.
Metrics may overlap («Новый товар» covers «Приёмка HOT»), which is why the net and balance columns come from
raw directions and the sub-columns are never summed to produce them.

**Presets** (`useStockMovementPresets`) are server-side and shared by everyone who can view the report; the
list query is invalidated after every write. The active one is `?preset=`, else the one flagged default, else
the first — the report has no columns without a preset, so there is no unselected state. `MetricsEditorDrawer`
edits a **draft** held on the page and passed straight into the pivot request: the preset is shared, and
saving merely to preview a metric would change the table for the whole team. The draft holds the name as well
as the metrics, so renaming is the same unsaved edit as adding a column; the chip keeps showing the stored
name until it is saved. The draft carries the id of the preset it was made against, so switching presets
discards it during render rather than through an effect, and selecting another preset clears it outright.
Saving sends the `version` the edit started from — the server requires it on every update — and a 409 means
someone else got there first.

Two details keep the live preview usable. A metric's **name is not part of the pivot query key** and a blank
one is sent as a `#n` placeholder: the name is a column label that changes no figure, so keying on it would
re-POST the whole pivot per keystroke, and an empty one would fail the server's `[Required]` and replace the
table with an error mid-rename. And each drafted metric carries a client-side `key` (`metricDraft.ts`) —
the API identifies a metric by position, which is also what dragging changes, so keying rows by index leaves
a focused input attached to whichever metric lands in that slot.

**«Итого за период»** renders only when `from` is set. Without it the table is in infinite mode, walking
backwards in 30-day windows, and a total over "however far the user happened to scroll" names no period.

**Full-tab mode** (`?full=1`) is a `<Dialog fullScreen>` holding the same table instance, with the preset bar
and a close button above it; `Escape` and the focus trap come from the Dialog. It deliberately does **not**
use `useBackClosable`: that hook marks a pushed history entry, and syncing `full` to the query string
replaces the entry and wipes the marker, so Back would reopen what was just closed. The table takes `fill`
there and stretches instead of capping at `70vh`. The «Метрики» button is hidden while expanded — the editor
is a `Drawer` and would render underneath the dialog.

**Catalog item selector** — `CatalogItemsSelect` restricted to `STOCK_MOVEMENT_ITEM_TYPES` (`standard` + `unit`;
groups, variations and bundles never hold stock). The selected items are what the pivot columns are made of, so
the clear icon is disabled.

**«По тегу» button** — opens `AddItemsByTagDialog`: pick one or more tags (`CatalogTagsFilter`), the dialog
queries `GET /api/catalog/for-select` with `tagIds` + `types` + `take=200`, previews the match count and appends
the found ids to the current selection (duplicates skipped). It's a one-shot action — the tag itself is not
persisted in the URL. A warning is shown when the result hits the 200-item cap.

**Receipt tags are per metric, not global.** `ReceiptTagsFilter` lives inside each metric row of
`MetricsEditorDrawer`: a movement matches when the receipt it came from carries any of the picked tags, so a
non-empty selection also excludes every movement made outside a receipt. That is a column-shaping decision —
«сколько приняли с тегом HOT» next to «сколько ушло в заказы» — and as one bar-level filter it could only ever
answer it for the whole table at once. The tag list comes from `GET /api/receipts/tags`, which needs
`receipts.view` or `receipts.view_assigned`, so the picker is rendered only for a user holding one of them,
the way the employee filter is gated on `users.view`.

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

### `TagsSettingsPage` (Теги)

`/settings/tags`, requires `tags.manage`. One tab per tag kind (`receipt`, `catalogItem`) kept in `?kind=` via
`useSyncedWithQueryState` — `ALL_TAG_KINDS` and the Russian labels live in `tagKinds.ts`, so a new kind on the
backend is one entry there plus one enum value in the generated `TagKind`.

The table reads `GET /api/tags?kind=` — name, `usageCount` and the row actions. `TagDialog` serves both create
and rename: with `tag === null` it posts `{kind, name}`, otherwise it puts the name to `/api/tags/{id}`. Names
are unique per kind, and the duplicate comes back as a 422 on the `name` field, so `useRhfApiErrors` puts it
under the input.

Deletion goes through `ConfirmDialog` and always states the count: «Тег привязан к N приёмок — привязка будет
снята со всех». The tag is deleted regardless — the count warns, it does not block, because the alternative
would be asking someone to unbind a tag from a hundred receipts by hand.

The page watches `("tags", empty guid)` — the backend publishes the list as one object — and on `entityChanged`
invalidates three operations, not one: `tagsGetAll` plus `catalogGetTags` and `receiptsGetTags`. The pickers
inside the catalog and receipt forms read their own module's endpoint, so their caches would otherwise keep
showing a renamed tag under its old name and offering a deleted one until a reload. There is no edit lock: rows
are independent and a rename is one field.

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
                    ├── ThemeColorMeta       (self-closing — syncs the theme-color meta tags)
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

## Theme and color mode

`src/theme.ts` builds the theme with MUI's `colorSchemes` (`light` and `dark`), so both palettes live in
one theme object and MUI swaps them at runtime. Never add a top-level `palette` key: it takes precedence
over `colorSchemes` and pins the app to one mode.

`ThemeProvider` is mounted with `defaultMode="system" noSsr`, so a first-time visitor follows the OS
`prefers-color-scheme` and only departs from it once they choose a scheme themselves. `noSsr` skips the
second render pass, so `mode` is defined on the first render of a client-only app. MUI persists the choice
in `localStorage` under `mui-mode` and syncs it across tabs.

The switch lives in the user menu in `MainAppBar` — a `MenuItem` flipping between `"light"` and `"dark"`.
A component that needs to know which scheme is *currently rendered* reads `scheme` from
`useResolvedColorScheme()` (`hooks/useResolvedColorScheme.ts`), which wraps `useColorScheme()` and
re-exports its result with that field added. `mode` stays `"system"` until the user picks a scheme, so
testing `mode === "dark"` alone mislabels the toggle for anyone on a dark OS who has never touched it —
`scheme` falls back to `systemMode` in that case. Read `mode` itself only to tell an explicit choice from
following the OS, as `ThemeColorMeta` does. Either way the source is this hook, never `theme.palette.mode`.

### Pre-mount paint

The theme only reaches the DOM once React mounts, so the document would otherwise show the browser's
default white ground for the first frames of every load. A blocking inline script in `index.html` closes
that gap: it reads `mui-mode` from `localStorage` (falling back to `matchMedia("(prefers-color-scheme: dark)")`
when the mode is `system`), resolves it through `mui-color-scheme-{light,dark}` and stamps
`data-mui-color-scheme` on `<html>` — the same attribute MUI writes itself, so mounting adds no second
repaint. An inline `<style>` in the same head paints `<html>` from that attribute and sets `color-scheme`
so the browser's own scrollbars and native controls start out in the right scheme. Both the script and
the style are duplicated state: the color there must track `palette.background.default`, and the whole
block runs before any bundle, so it stays dependency-free ES5-shaped JS wrapped in `try`/`catch`.

### Browser theme color

`index.html` carries two `<meta name="theme-color">` tags, one per `prefers-color-scheme`, holding
`APP_BAR_LIGHT_BG` and `APP_BAR_DARK_BG` from `theme.ts`. They override `manifest.theme_color`, which the
manifest keeps only as a fallback, and they colour the mobile status bar and the installed-PWA title bar
to match the app bar rather than a single fixed blue.

Media-scoped tags answer the OS, not the user, so an explicit choice has to override them: the pre-mount
script collapses both tags onto the picked colour when `mui-mode` is not `system`, and `ThemeColorMeta`
(mounted next to `CssBaseline` inside `ThemeProvider`) keeps doing that from `useResolvedColorScheme()`
for the rest of the session, so flipping the switch updates the status bar without a reload. Going back
to `system` restores one colour per tag and lets the media queries decide again.

`manifest.background_color` cannot follow the scheme — it paints the Android splash screen before any
page code runs, and the manifest has no media queries.

### Dark scheme

The dark scheme overrides the MUI defaults rather than inheriting them:

- `text.primary` is held just below full white — full-strength white on a near-black surface reads as
  glare and haloes at the glyph edges.
- `background.default` / `paper` are a slightly cool near-black, `paper` a step above `default`.
- `MuiPaper` drops the elevation overlay in the dark scheme (`backgroundImage: none`). MUI tints every
  Paper by its elevation, which compounds on nested Paper — a menu or dialog inside a card ends up
  visibly lighter than the surface it sits on. Without it a Paper is exactly its palette color and
  depth reads from the shadow instead.
- `error.main` is softened; MUI's default is the only fully saturated hue on a dark page and outshouts
  the content it belongs to.
- `MuiTableCell` takes its bottom border from `divider` in the dark scheme. MUI derives that border from
  a formula — `darken(alpha(divider, 1), 0.68)` — which lands far heavier against a dark surface than its
  light counterpart does against white, and heavier than every other line in the app.
- A raised sub-surface inside a `Paper` — an `AccordionSummary` header, a sticky table head — takes
  `action.hover` (and `action.selected` on hover), never a hard-coded `grey.50`/`grey.100`. The `grey` ramp
  is one fixed scale shared by both schemes, so a light grey stays light on a near-black card and the header
  turns into a white bar.
- `MuiListItemIcon` is retinted to `text.secondary` in the dark scheme. MUI gives list icons
  `action.active`, which is a little over half-opacity black in light but pure white in dark — so icons
  come out *brighter* than the labels beside them and the hierarchy inverts between schemes.

The app bar is dark in the dark scheme: `MuiAppBar.styleOverrides` wraps its rules in
`theme.applyStyles("dark", …)`, drops the elevation overlay and shadow, and separates the bar from the
page with a `divider` bottom border. The brand mark carries the accent instead — the `WarehouseIcon` and
the "Warehouse" wordmark in `MainAppBar` take `primary.main` through the same `applyStyles("dark", …)`
helper, since in the light scheme the bar is already `primary` and the mark stays white.

### Chip colors

Chips are labels, not actions, so in the dark scheme every filled chip is a muted tint — a dark colored
ground with a light colored label — instead of a saturated block with dark text. Two tint tables in
`theme.ts` carry that, and they are deliberately separate:

- `DARK_CHIP_TINTS` — one entry per hue (`bg`, `fg`, `hover`) for MUI's **built-in** colors.
- `ITEM_CHIP_TINTS` — one entry per **catalog item type**.

**Built-in colors** (`default`, `primary`, `secondary`, `error`, `info`, `success`, `warning`) are
retinted through `MuiChip.styleOverrides`, which targets `&.MuiChip-filled.MuiChip-color*` inside
`theme.applyStyles("dark", …)`. This covers order kinds, statuses and assembly task states, so a screen
never mixes muted and saturated chips.

**Catalog item types** get their own palette colors — `itemStandard`, `itemUnit`, `itemProductGroup`,
`itemVariation`, `itemBundle` — declared per scheme and augmented in `src/extend-theme.d.ts` (both
`Palette`/`PaletteOptions` and `ChipPropsColorOverrides`), the same pattern as `ozon` and `wb`.
`CATALOG_ITEM_TYPE_CONFIG` in `features/catalog/catalogItemTypes.ts` maps each `CatalogItemType` to one
of them, and `CatalogItemTypeChip` passes it straight to `Chip`.

Item types hold hues of their own rather than reusing the built-in ones. `ORDER_STATUS_COLORS`,
`ORDER_TYPE_COLORS` and `TASK_STATUS_COLORS` between them already claim all six built-ins, and an order
list puts an order kind, a status and an item type in the same row — sharing a hue there makes the three
read as one thing. The `ozon` and `wb` brand colors are spoken for too, so the item set stays clear of
those hues as well.

Two of the entries encode a relationship rather than an identity: `itemStandard` and `itemUnit` share a
hue and differ only in tone, because they are exactly `PHYSICAL_CATALOG_ITEMS` — the types that hold
stock. Keep that pairing if either color changes.

Note the token roles when editing any of these. `main` is the chip ground and `contrastText` the label,
while `dark` is what `Chip` uses on **hover** — so in the dark scheme `dark` is *lighter* than `main`,
which reads like a typo and is not. Labels are white on every light-scheme item chip; a ground light
enough to need dark text breaks that row, and such a ground also fails to clear 4.5:1 against white — so
grounds are chosen dark enough to keep the white label.

Both tint tables are dark-scheme-only, so the built-in chips render as stock MUI in the light scheme.
Item-type chips are the exception: they carry their own colors in both schemes.

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

Manifest: name "Project Warehouse", theme `#1976d2`, standalone display. The theme colour is
overridden per scheme by the `<meta name="theme-color">` tags described in
[Browser theme color](#browser-theme-color).

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
- Sets `baseUrl` to `window.location.origin`
- Starts the cross-tab auth channel (below)
- Installs a request interceptor that proactively refreshes the JWT access token when < 30 s of its lifetime
  remains
- Installs a response interceptor that on a 401 attempts a refresh and retries the request. If no `accessToken`
  is in `localStorage` the response is passed through without a refresh attempt (avoids spurious refreshes on
  unauthenticated requests).

The replay needs the request body, and `fetch` consumes it, so the request interceptor stores a clone in a
`WeakMap` — but only for a request that has one. A bodyless request (every `GET`) is replayed as it is, which
keeps a 25 MB file upload from being buffered twice on its way out.

### Refresh outcomes

`refreshTokens()` resolves to one of three values, and only one of them ends the session:

| Outcome | When | What the 401 interceptor does |
|---|---|---|
| `ok` | the server issued a new pair | replays the original request with the new token |
| `invalid` | the server rejected the refresh token (4xx), or there is none | raises `auth:refreshTokenInvalid`, clears tokens |
| `unavailable` | the server is unreachable or answered 5xx | returns the 401 untouched, tokens kept |

`unavailable` is the case that matters on the warehouse floor: a dropped connection must not log a picker out
while their refresh token is good for another week. A successful refresh whose original request has no stored
clone to replay is likewise not a session failure — the caller just sees the 401.

Tokens live in `localStorage`, written only through `storeTokens()` / `clearTokens()` in
`services/apiClient.ts` — the expiry timestamp is stored alongside the tokens so the proactive refresh above
needs no JWT decode on every request. Call `storeTokens(tokenResponse)` after a successful login (the auth
context does this) and `clearTokens()` on logout. Each dispatches a window event — `auth:tokens` and
`auth:clear` — which is how code outside the React tree learns that authentication changed.

### Cross-tab authentication

Every tab of the origin shares `localStorage`, so the tokens are common state while the React trees are not.
`services/authChannel.ts` closes that gap: `storeTokens()` and `clearTokens()` post a `tokens` / `clear`
message on a `BroadcastChannel`, and receiving tabs re-raise the matching local window event. Receivers never
write storage back, so an echo loop is impossible. Where `BroadcastChannel` is missing the transport falls back
to a nonce-carrying `localStorage` key, which reaches other tabs through the `storage` event. That key is a
transport rather than state: `clearTokens()` removes it, and receivers ignore a `storage` event whose
`newValue` is null, so the removal reaches nobody as a message.

`AuthProvider` listens to both events, so a login, a token refresh or a logout in any tab is picked up by all
of them — the others set `hasTokens` and refetch `/api/auth/me` rather than continuing with a stale identity.

The rotation itself is serialized origin-wide with `navigator.locks.request("auth-refresh", …)`. A refresh
token is single-use, so two tabs presenting the same one would leave the loser holding a revoked token and log
everybody out; whoever waits on the lock re-reads `accessToken` afterwards. A changed value means the winner
rotated (`ok`); a missing one means the winner logged out (`invalid`). The wait carries a 30 s
`AbortSignal.timeout`, so a tab stuck mid-`fetch` cannot hold the rotation for the rest of them — a timed-out
wait is `unavailable`, which leaves the session alone.

The proactive refresh itself is `getFreshAccessToken()`, exported from the same module: it returns a token
that is valid for at least the next 30 seconds, refreshing first if it is not, and `null` once a refresh comes
back `invalid` — a known-dead token is not worth a round trip. The request interceptor is one
caller; the telemetry exporters, which send their own requests, are another. Anything that builds an
`Authorization` header by hand goes through it rather than reading `localStorage` directly.

### Reading the access token

`decodeJwtClaims(token)` in `@/utils/jwt` is the only JWT decoder — base64url with padding restored, decoded
through `TextDecoder`, so Cyrillic claims survive. Two callers: `parseJwtUser()`, which builds the provisional
`MeResponse` shown before `/api/auth/me` answers (`fullName` from `given_name` + `family_name`, falling back to
the `name` claim, which is the username), and `services/currentUser.ts`, which labels telemetry. The claim table
lives in [api.md](api.md#access-token-claims). The token is read as a label only — every permission it carries
is re-checked on the server.

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
