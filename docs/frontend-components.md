# Frontend Components

Reusable components and feature modules — the conventions, compositions and gotchas that reading the file will
not tell you. Props interfaces are deliberately not mirrored here; open the component.
Architecture overview: [frontend.md](frontend.md).

## Layout & tables

### `MainAppBar`

Top navigation bar. It lives in `@/components/MainNav/` together with `MainNavDrawer` and the shared
`mainNavConfig.tsx` they both read. The link set lives in `mainNavConfig.tsx`: `mainNavPages` declares
the entries, `resolveMainNavPages(permissions)` filters them and resolves each `url`. An entry supports
`requiredPermission` (`PermissionName | PermissionName[]` — an array means "any of"), `showIf` (an arbitrary
predicate over permissions) and `url`, which may be a plain string **or** a
`(permissions: PermissionName[]) => string` factory. The sidebar modules use the factory form
(`getStorageFirstPageUrl`, `getOperationsFirstPageUrl`, `getSettingsFirstPageUrl`) so the app bar links
straight to the first section the user can actually reach instead of a redirect that might bounce them to
`AccessDenied`.

An entry may also carry `basePath` + `sections: SectionConfig[]` — the module's own sidebar config.
`resolveMainNavPages` feeds those through `toNavItems` so each resolved page also exposes `navItems`, the
same permission-filtered tree `SidebarPage` renders. That is what `MainNavDrawer` expands.

On desktop (md+) the entries render as flat `Button` links; on mobile the burger opens `MainNavDrawer`.

### `MainNavDrawer`

Left-anchored mobile navigation `Drawer`. Pages without
`navItems` (e.g. Каталог) are plain links; pages with them are accordion rows — **only one section is
expanded at a time**, and the header row only toggles, it never navigates, so a mistap on a touch screen
cannot throw the user onto another page.

Expansion is derived, not stored: `expandedOverride === undefined` means "nobody picked one yet" and the
section containing the current route is shown open; toggling writes an explicit override, and closing the
drawer clears it back to `undefined`. Nested `SidebarNavGroup`s render as a non-clickable caption with their
children indented under it. Any navigation (link tap, logo) closes the drawer.

Back closes the drawer instead of leaving the page — see
[`useBackClosable`](frontend-state.md#usebackclosableopen-onclose). Every link inside therefore navigates with
`replace`, which is what keeps the history stack clean.

If the `/me` profile query fails, an inline red error is shown in the avatar dropdown via
`profileIsLoadError` / `profileLoadError` from `AuthContext` — a failed profile must not look like "no
permissions".

### `SidebarLayout`

Generic visual layout for pages with left-panel navigation: a MUI `List` sidebar on desktop (md+), hidden on
mobile, where the same items are reachable through `MainNavDrawer`.

The nav vocabulary both renderers share lives in `@/layouts/SidebarLayout/navItems.ts`: the
`SidebarNavLeafItem` / `SidebarNavGroup` / `SidebarNavItem` types plus `isGroup` and `isActive`. Active item
detection goes through `matchPath({end: false})`, so sub-routes highlight the parent item.

### `SidebarPage`

Routing wrapper on top of `SidebarLayout`. Takes `sections: SectionConfig[]` and a `basePath`, and:
- builds the nav filtered by user permissions and `showIf` via `toNavItems`
  (`@/layouts/SidebarPage/toNavItems.ts`, also used by `MainNavDrawer`)
- creates `<Routes>` with relative paths (leaves, subroutes, and redirect routes for groups)
- redirects a group with no `component` to its first visible child at runtime
- wraps every rendered route in `ProtectedRoute`, rendering `<AccessDenied />` when the section's
  `requiredPermission` is not met — a deliberate second check beyond nav filtering, since a URL can be typed

**To create a new sidebar-based page**, declare a `SectionConfig[]`, call `createHasAccess(sections)`
(`@/layouts/SidebarPage/createHasAccess.ts`) for an app-bar visibility helper, call `createFirstPageUrl(sections)`
(sibling file) for the permission-aware deep-link factory, and render `<SidebarPage sections={…} basePath="…" />`.
See `settingsConfig.tsx` for the reference implementation.

**`SectionConfig` fields:**

| Field | Type | Description |
|---|---|---|
| `label` | `string` | Nav item label |
| `path` | `string` | Relative path segment (e.g. `"roles"`) |
| `component` | `ComponentType?` | Page component; absent → redirect to first visible child |
| `requiredPermission` | `PermissionName \| PermissionName[]?` | Hides the item unless the user has it (any of, for an array) |
| `showIf` | `() => boolean?` | Additional visibility predicate (feature flags etc.) |
| `subroutes` | `SectionSubroute[]?` | Sub-paths (e.g. `":id"`) that highlight the parent nav item |
| `children` | `SectionConfig[]?` | Nested nav sections (max depth 1); the section becomes a group |
| `icon` | `React.ReactElement?` | Icon in the sidebar group header (desktop only) |

### `AppBreadcrumbs`

The breadcrumb trail at the top of every page, built from `{name, link?}` objects; the last item is plain text.
Two slots make it the page's whole top row: `viewersOf={{entityType, entityId}}` appends `EntityViewers` right
after the trail (one line to add presence to a page), and `right` is a free slot pushed to the far end.

### `DataTableContainer`

Standard list-page table shell: `Paper` → `LinearProgress` (while fetching) → `TableContainer` →
`TablePagination` with Russian labels baked in. Extends `PaperProps`. The `page` prop is **1-based**, matching
the `usePaginatedParams` convention; the component converts to MUI's 0-based value internally.

### `RouteFallback`

Centred `CircularProgress` shown while a whole route is unavailable — a lazy page chunk still loading
(`Suspense fallback` in `MainLayout` / `MainAppBarLayout` / `App.tsx`) or the `/me` query still deciding
whether the user is authenticated (`AuthGuard` in `ProtectedRoutes`). It renders **nothing** for the first
`delay` ms (50 by default), then fades in over a full second: a warm chunk resolves long before the spinner
is legible, so the common case shows no flash while a genuinely slow load still gets feedback.

A cold load runs two instances back to back — the auth guard's while `/me` is in flight, then the layout's
while the page chunk downloads. A module-level session ties them together: the second instance measures its
delay from when the *first* appeared, and the fade is a CSS keyframe given a **negative** `animation-delay`
equal to the time already faded, so it resumes at the exact opacity the previous instance reached. The
handover is invisible.

The session is keyed on a live-instance count rather than a timer, because a React render can be discarded
before it commits: a seed left behind by such a render is only honoured while an instance is mounted or
within `SESSION_GAP_MS` of the last one leaving, so an orphan expires instead of disabling the delay for the
rest of the page's life. That same gap is what separates one load from the next.

Distinct from `LoadingOverlay`: this one *replaces* the content, so it needs no positioned parent and does not
dim anything.

### `LoadingOverlay`

Dim-and-blur overlay for content that is **being refetched in the background** — an `entityChanged` SSE hint
invalidated the query, or the tab regained focus and TanStack refetched. Not for the first load: that is what
`TableRowLoader` and skeletons are for, so pages pass `open={isFetching && !isLoading}`.

```tsx
<Box sx={{position: "relative"}}>
  <LoadingOverlay open={isFetching && !isLoading} />
  <Stack spacing={2}>{/* page content */}</Stack>
</Box>
```

Pages wired to realtime take the flag ready-made instead of assembling it: `useStaleData`/`useEditLock`
return `showLoadingOverlay` (`isFetching && !isLoading && !isSilentRefresh`), so the page only adds its own
conditions — `open={lock.showLoadingOverlay && !isEditingAnything}`. The `isSilentRefresh` part is what keeps
the overlay from flashing on mount: the confirmed subscription triggers a refetch right behind the first read,
and that one is deliberately invisible (see `useSilentRefresh` in `frontend-realtime.md`).

The parent must be `position: relative` — the overlay is `position: absolute; inset: 0` and covers it whole.
Keep it out of a spacing `Stack`: the `& > * + *` margin rule would also apply to the absolute child and push
it off by one gap. It intentionally **blocks pointer events**, so nothing can be clicked in the moment before
it is replaced by fresher data. Dialogs are portalled to `body` and stay reachable.

**For detail pages, not list pages.** A list already reports a background refetch through
`DataTableContainer`'s `LinearProgress`, and dimming its filters would be a regression. A detail page has no
such affordance, so this is where the overlay earns its place.

The two layers are timed differently on purpose:

- The **backdrop follows `open` directly** — the dim and blur land on the first frame, so the freeze is
  acknowledged immediately rather than after a suspicious pause.
- `delay` (300 ms) gates only the **spinner**. A fast round-trip dims the page for a moment and never puts a
  spinner on screen, which is the difference between "that refreshed" and "that is loading".
- `minDuration` (200 ms) is the floor on the backdrop. Once shown it stays at least this long even if the data
  lands right after, so the overlay is never a one-frame flash.

The backdrop's `Fade` runs with `appear={false}`, so a page that mounts already refetching — the usual case
now that detail pages keep their cache and render from it instantly — opens with the overlay in place instead
of fading it in over data the user can see is not fresh. It only affects the first render; every later open
fades normally.

`visible` is derived (`open || holding`) rather than stored: only the post-`open` tail lives in state, which is
what keeps the backdrop free of a render's worth of lag and keeps `setState` out of the effect body.

`label`, `blur`, `size` and `sx` are optional. The spinner is centred by the overlay's own flexbox and is
additionally `position: sticky` with **both** `top` and `bottom` insets, so a page taller than the viewport
keeps it on screen. Do not express that as a `vh` offset: sticky can only shift an element inside its own
containing block, so on any overlay shorter than the offset the shift clamps and the spinner sinks to the
bottom edge. The backdrop is `alpha(theme.palette.background.default, 0.55)` — theme tokens, not a hardcoded
white.

**Every entity detail page carries it**: `ReceiptPage`, `WriteoffPage`, `StocktakePage`, `OrderPage`,
`MarketplaceAccountPage`, `UserViewPage`, `UserEditPage`, `WarehouseViewPage`, `MyProfilePage`.

Where the page has an edit mode the condition also carries `&& !<edit flag>` — blurring a form someone is
typing into is worse than showing nothing, and a concurrent write during an edit is already reported by
`useEditLock` / `StaleDataBanner`. The flag differs per page: `isEditingAnything` on the operation documents,
`isEditingMeta` on `OrderPage`, `form.formState.isDirty` on `UserEditPage` (the whole page is the form).

`WarehouseEditPage` has none: it is a canvas editor that holds the layout dirty from the moment it opens
(`useEditLock(..., {isDirty: true})`), so there is no read-only state for the overlay to belong to. It does
not take one.

`CatalogItemDrawer` takes it too, but on the **content area only**, not on the `Drawer` — an overlay over the
whole panel would swallow the close button and trap the user until the refetch ends. The wrapper also sits
*outside* the scrolling `Box` rather than inside it: an `inset: 0` child of an `overflow: auto` parent is sized
to the visible box and scrolls away with the content, leaving everything below it uncovered. Only the
read-only view carries it; `EditMode` is a separate branch, so no edit flag is needed.

The inventory pages (`WarehouseInventoryPage`, `StoragePlaceInventoryPage`, `NodeInventoryPage`) deliberately
**do not** take it. Their own query only resolves the warehouse or place name; the content is an
`ItemsBasePage` list that reports its own fetching, so an overlay there would almost never fire and would
cover a table that is already covered.

### `FiltersBar`

Filter row rendered as an outlined card — rounded border in `divider`, a faint `primary` tint background and
`px: 2 / py: 1.5` padding — so the filters read as a block separate from the table below.

The leading label is a `FilterAltIcon` on a tinted rounded square plus a «Фильтры» caption; the caption is
hidden below the `sm` breakpoint, leaving only the icon on narrow screens. Controls go in `children`; the
optional `actions` slot renders at the right edge (`ml: "auto"`) for things like a reset button.

The `sx` prop is **merged** with the component's own defaults via MUI's array `sx` syntax, not replaced.

### `SearchInput`, `SelectAllHeader`, `InfoRow`, `TableRowLoader`, `TableRowEmpty`, `PageGenericHeader`

Thin presentational wrappers; read the file. Two non-obvious details: `InfoRow`'s label column is fixed at
**160 px** from `sm` up (that is what keeps detail views aligned across pages), and `SelectAllHeader` swallows `mousedown`
and every `keydown` except `Escape`/`Tab`, so a dropdown stays open and its arrow-key navigation doesn't hijack
the «Выбрать все» / «Снять выбор» buttons.

`InfoRow`'s value slot renders as `Typography component="div"`, so a chip or any other block element can be
passed as `value` without invalid nesting.

Below `sm` `InfoRow` turns into a column: the label loses its fixed width, drops to `body2` size and sits above
the value, which then gets the full width of the container for long strings and chips.

`PageGenericHeader` has three slots besides `title`: `children` (the middle block — search, filters), `actions`
(action buttons) and `refresh`. The `refresh` slot is rendered twice with breakpoint-gated `display`: from `md`
up it is the first item of the actions group, below `md` — where the header stacks into a column — it moves into
the title row and is pushed to its right edge with `ml: "auto"`, so the reload action stays reachable without
adding a row on narrow screens.

The actions group is a wrapping row aligned to the right edge. Below `md` every direct child gets
`flex: 1 1 auto`, so buttons share the row evenly and a lone button spans the full width; `IconButton`s keep
`flex: 0 0 auto` to stay square. Pass buttons to `actions` as a fragment rather than a nested `Stack` — a
wrapper collapses into a single flex item and its buttons stop stretching.

### `WarehouseChip`, `UserChip`

`<WarehouseChip warehouseId={…} name={…} />` and `<UserChip userId={…} name={…} />` (both in
`components/shared/`) — outlined `size="small"` chips used as the `value` of an `InfoRow` wherever a detail view
names a warehouse («Склад») or a person («Создал», «Кем подключён»).

Each checks the viewer's rights itself: `WarehouseChip` needs `warehouses.view` or `warehouses.view_assigned`
and links to `/storage/warehouses/{id}`, `UserChip` needs `users.view` and links to
`/settings/employees/{id}`. Without the permission — or without an id — the same chip renders unlinked, so the
row keeps its shape either way. `onClick` stops propagation so a chip inside a clickable row doesn't trigger it.
Both forward the remaining `Chip` props.

### `UserAvatar`

`<UserAvatar userId={…} name={…} />` — a MUI `Avatar` whose background is derived from the user id, so the same
person keeps the same colour in presence avatars, the app bar and anywhere else. The letter is the first
character of `name`, `?` when there is none. Accepts every `Avatar` prop except `children`; `sx` is merged, so
sizing still works (`sx={{width: 32, height: 32}}`).

The colour comes from `userColor(userId)` in `utils/userColor.ts`: FNV-1a over the id → hue, fixed `55% 45%`
saturation/lightness, which keeps white text readable on every hue. A missing id falls back to `grey.500`. Use
`userColor` directly when something other than an avatar needs the same per-user tint.

## Placeholders & errors

`AccessDenied` (403), `NotFound` (404), `QueryError` (everything else) are full-page placeholders. The branch
convention and the `suppressGlobalError` / `suppressGlobalNotFound` pairing are documented once in
[frontend.md § Inline error branches](frontend.md#inline-error-branches).

### `QueryErrorHandler`

Subscribes to the TanStack Query cache and surfaces **unhandled** query/mutation errors as modal alerts. It
skips a query when `meta.suppressGlobalError` is `true` (all errors) or `meta.suppressGlobalNotFound` is `true`
(404s only) — that is the opt-out a page uses when it renders its own inline error state, so the user never
sees the modal and the placeholder at once.

Also listens for the `auth:refreshTokenInvalid` window event and shows a session-expired warning.

### `ConfirmDialog`

Generic confirmation dialog with a spinner on the confirm button; blocks backdrop-click dismissal while
`isPending`. Used for every destructive confirm flow — do not hand-roll another one.

## Catalog

### `features/catalog/catalogItemTypes.ts`

The single source of truth for catalog item type metadata:

```ts
CATALOG_ITEM_TYPE_CONFIG: Record<CatalogItemType, {label: string; color: ChipProps["color"]}>
CATALOG_ITEM_TYPES: CatalogItemType[]      // all types, derived from the config keys
PHYSICAL_CATALOG_ITEMS: CatalogItemType[]  // ["standard", "unit"] — the only types that hold stock
```

`CATALOG_ITEM_TYPES` is derived from the config keys, so the two cannot drift. `PHYSICAL_CATALOG_ITEMS` is the
type set for anything stock-related — `ItemsBasePage`'s type filter and `StockMovementsPage` (both its
`CatalogItemsSelect` and the by-tag dialog) restrict themselves to it, since groups, variations and bundles
never hold inventory of their own.

**When a new type is added to the backend OpenAPI schema, update only this file** — `CatalogItemTypeChip`,
`CatalogTypesFilter` and the creation `Select` all follow automatically.

### `CatalogItemDrawer`

Reusable right-side drawer for viewing and editing any catalog item. Mount it on a page with
`useDrawerSearchParamsState`; `onOpenItem` exists for in-drawer navigation (e.g. "open parent group").

Edit is hidden for items with a `groupId` — those are managed by the parent group, shown as an info alert.

**Header actions** (both modes, and also for group-managed items):
- **Скопировать GUID** — copies the raw id via `copyToClipboard` (`utils/clipboardUtils.ts`:
  `navigator.clipboard` with a hidden-textarea + `execCommand` fallback for insecure origins and the Capacitor
  shell), then reports the result via snackbar.
- **Печать этикетки** — opens `PrintLabelDialog`: payload + copy count (1–200), then `openPrintPage` with the
  item repeated N times.
  - *Внутренний код* — `DataMatrix` with `pw:ci:<guid>` (see
    [barcode payload format](frontend.md#barcode-payload-format))
  - *Штрихкод товара* — the item's own `barcode`; disabled when empty. Encoded as `EAN13` for 12–13 digit
    values, otherwise `Code128`, since bwip-js rejects non-numeric EAN13 payloads.
  - Label caption for both: `fullName · article`.

**Convention:** wherever a catalog item name is rendered — table cell, card headline, drawer row — it should be
a `CatalogItemLink` that opens this drawer. When building a new page or drawer that shows catalog items, add the
open-drawer affordance as part of the initial implementation, not as a follow-up. State always goes through
`useDrawerSearchParamsState`, so «назад» closes the drawer; only the param name differs:

- **Page, single link owner** → `useDrawerSearchParamsState("catalogItem")` plus a local `<CatalogItemDrawer>`.
  The opened item lands in the URL and stays deep-linkable (`ItemsBasePage`, `ReceiptItemsSection`,
  `WriteoffItemsSection`). `CatalogPage` is the one exception: it uses `"item"` for its own row drawer, a
  page-local param that must not be confused with the shared `"catalogItem"` name.
- **Page whose links live in components rendered in a loop** → wrap the page in `CatalogItemDrawerHost` and
  call `useOpenCatalogItem()` in the leaf. A per-component drawer would open N copies at once, since the state
  is shared via the URL.
- **Nested inside a drawer/dialog whose own open state is *not* in the URL** → use a distinct param name and
  register it in `EPHEMERAL_PARAMS`, e.g. `"fulfillmentCatalogItem"` in `FulfillmentsDrawer`. Otherwise a reload
  would reopen the nested drawer on top of a closed parent. Never reuse `"catalogItem"` for this — that name
  must survive a cold load.

### `CatalogItemDrawerHost`

Renders exactly one `CatalogItemDrawer` for a whole page (param `"catalogItem"`) and publishes its open function
through context. The context and the `useOpenCatalogItem()` hook live in a separate `CatalogItemDrawerContext.ts`
so the host file only exports a component (react-refresh rule).

```tsx
// page
<CatalogItemDrawerHost>
  <Stack spacing={2}>…</Stack>
</CatalogItemDrawerHost>

// any descendant, however deep
const openCatalogItem = useOpenCatalogItem();
<CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItem}>…</CatalogItemLink>
```

`useOpenCatalogItem()` **throws** outside the host — a missing wrapper fails loudly instead of silently doing
nothing. Used by `OrderPage` and `OrdersAssemblyPage`.

### `CatalogItemLink`

Gives any catalog item label the standard clickable look: pointer cursor, `fit-content` width and an
`OpenInNewIcon` that appears on hover. Click calls `onOpen(catalogItemId)` and `stopPropagation()`s, so it stays
safe inside clickable table rows.

Content is passed as **children** because the composition differs per call site (chip before or after the name,
inventory number, archive icon, extra badges) — the wrapper stays flag-free.

```tsx
<CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItemDrawer} spacing={0.5}>
  <Typography variant="body2">{c.catalogItemName}</Typography>
  <CatalogItemTypeChip type={c.catalogItemType} />
</CatalogItemLink>
```

### `CatalogItemTypeChip`

A `Chip` mapping `CatalogItemType` to a label and colour straight from `CATALOG_ITEM_TYPE_CONFIG`. Takes every
`ChipProps` except `label`/`color`.

### `CatalogTypesFilter` / `CatalogTagsFilter`

Shared filter controls used by `CatalogPage`, `ItemsBasePage` and `AddItemsByTagDialog`.

`CatalogTypesFilter` is a multiselect `Select` with a checkbox per type; `options` defaults to
`CATALOG_ITEM_TYPES`, inventory pages pass `PHYSICAL_CATALOG_ITEMS`. `renderValue` shows «Все» for the full set,
«Нет» for none, `"N типов"` for 2+, or the single label.

`CatalogTagsFilter` is a multiselect `Autocomplete` over the full tag list (`GET /api/catalog/tags`, filtered
client-side). Value and `onChange` are tag **id** arrays; chips resolve their names from the loaded list.

Both pin a `SelectAllHeader` above the options — injected as the `Select` menu's first child and via the
`Autocomplete` `paper` slot.

**URL state** — `useCatalogTypesFilter(key?, options?)` keeps the type selection in a query param. The full
`options` set is the default and is omitted from the URL; an empty selection is stored as `none`
(`NO_ITEM_TYPES`), because serializing it to `null` would drop the param and read back as "all types". Tag ids
are plain comma-separated values via `useSyncedWithQueryState`.

### `CatalogItemsSelect`

Autocomplete for catalog items, single or multi.

- **Single mode** — the value is an entity `id` (`string | null`); the optional `onDtoChange` callback fires
  with the resolved `CatalogItemSummaryDto`, so a caller needing `fullName` or `type` does not have to issue a
  second query.
- **Multi mode** — value and `onChange` work with `CatalogItemSummaryDto[]`.

Both debounce the search input (300 ms), fetch via `catalogGetForSelectOptions`, and **cache selected items so
they survive search changes** — otherwise a selected chip would vanish as soon as the user typed a query that
does not match it. `types?` is passed straight through to the endpoint as server-side filtering.

## Files

### The files subsystem (`components/files/`)

Three layers, plus a viewer. `FileInput` and `FileView` are pure components that know nothing about the API;
`FileControl` is the **only** layer that calls it. Compose them by passing the other two in as props:

```tsx
<SingleFileControl value={value} onChange={onChange} View={ImageCardFileView} Input={AddFileInput} accept="image/*" />
```

A control is a controlled component over a `DataFileDto`, **not** over a `File`: picking a file uploads it
immediately (`POST /api/files`) and hands the form a ready DTO, so the form only ever stores an identifier.
That is upload-first in practice; files nothing ends up referencing are collected server-side. Wire it with
`Controller` like any other field.

> **The clock is running on an open form.** A file uploaded and left unsaved past `OrphanTtlHours` (48 h) is
> collected, and the save then fails with `dataFileNotFound`.

`SingleFileControl` keeps its own hidden `<input type="file">` for the view's replace action, so a filled
control can be re-picked without deleting first. `FileListControl` uploads a batch sequentially and keeps its
own `failures` list — the upload hook holds only the last error, and a batch needs one message per file name.

**Reordering.** `FileListControl` takes `sortable` and wraps its views in `@dnd-kit` (`rectSortingStrategy` for
a wrapping row, `verticalListSortingStrategy` for a column). Position *is* the value: the control just calls
`onChange` with a reordered array, and the owner decides what that means — for catalog images
`mapImagesToRequest` turns the index into `order`. The whole tile is the drag handle, since a photo grid is
dragged by the photo; `PointerSensor` with `activationConstraint: {distance: 5}` keeps a tap opening the viewer
instead of starting a drag. Listeners live on a wrapper inside the control, never in `FileViewProps` — the views
stay pure. The wrapper is rendered only while `sortable` is on, so a plain list has no extra DOM layer at all.

### `FileImage`

Behaves like an `<img>` but takes a file instead of a `src` and handles authorization, preview size and object
URL lifetime itself. Used wherever an image is just an image: catalog thumbnails, previews inside the viewer's
filmstrip, marketplace card avatars.

Images cannot use a plain `<img src="/api/files/…">` — the bearer token is injected by the request interceptor
in `services/apiClient.ts`, and an `src` attribute carries no `Authorization` header. So content is fetched as a
`Blob` and turned into an object URL. The consequence is **no browser HTTP cache**; React Query replaces it,
keyed by id + width, so the same image in a list and in the modal is fetched once.

> **Object URLs are revoked on a deferred tick, not synchronously.** StrictMode runs an effect's setup, cleanup
> and setup again on mount, and the URL comes from a `useMemo` that the second setup does not re-run — revoking
> in the cleanup would kill a URL nothing recreates. The revoke is queued in a module-level map and cancelled if
> a setup follows immediately.

- **Lazy by default** via `IntersectionObserver` — a catalog page would otherwise fire a request per row. Where
  the observer is missing (oldest targeted WebViews) it degrades to eager loading, not to a blank box.
- **`previewWidth: "auto"`** measures the container, multiplies by `devicePixelRatio` and rounds **up** to an
  allowed width; the endpoint rejects arbitrary values.
- **Space is reserved** from `imageWidth`/`imageHeight` using a padding-top percentage box — CSS `aspect-ratio`
  is unsupported at the `chrome >= 49` build target.
- **External sources** go straight to `src` with `referrerPolicy="no-referrer"`, no request and no resize. An
  `http:` URL on an `https:` page is rejected up front, since the browser would block it silently.

### `FileViewerModal`

Gallery modal opened through `useModal().showModal`. It is **not tied to the files subsystem**: a `ViewableFile`
is either one of our files (`viewable(dto)`) or an external link (`viewableUrl(url)`), and mixed lists are fine —
a product photo and a marketplace card image scroll in one gallery. `useViewableSource` collapses both kinds into
one shape so the renderers never branch on the source.

```ts
await showModal(FileViewerModal, {files: item.images.map((i) => viewable(i.file)), initialIndex: 2});
```

Arrows, the counter and the filmstrip appear only for more than one file. `Esc` closes, `←`/`→` navigate.
Renderers are tried in order: image → PDF → unsupported. Images support wheel zoom and drag pan (hand-rolled —
there is no lightbox library here); an `onError` drops to the unsupported card, which is also the degradation
path for an external link that turned out not to be an image.

For external sources the metadata line is blank (we have none) but keeps its height so the toolbar does not jump,
the delete button is hidden, and «Скачать» becomes «Открыть в новой вкладке» — the browser ignores `download` on
a cross-origin link.

## Marketplace

### `marketplaceUtils`

Label maps for the marketplace enums plus date/duration/price formatters.

`hasCapability(capabilities, flag)` exists because `MarketplaceCapabilities` is a **`[Flags]` enum**:
`JsonStringEnumConverter` sends a combination as one comma-separated string (`"warehouses, cards, sellerInfo"`),
which the generated union of single values does not describe. **Never compare `capabilities` with `===`.**

### Chips and alerts

- **`MarketplaceStatusChip`** — colours `MarketplaceSyncStatus`, and renders «Не синхронизировался» when the
  status is `null`: a freshly created account has none, and an empty cell would read as a failure.
- **`CardMappingChip`** — `isMappedToArchivedItem` **wins over the source** and renders a warning chip
  «Привязана к архивному товару»; otherwise the mapping source is shown. Renders nothing for an unmapped card.
- **`WarehouseStatusChip`** — renders the marketplace-agnostic `MarketplaceWarehouseDto.status`
  (`active`/`inactive`/`unavailable`). The per-marketplace wording is collapsed into these three server-side
  (see [marketplaces-specification.md](marketplaces-specification.md)); `externalStatus` still travels along and
  shows up as a tooltip, so «почему недоступен» stays answerable.
- **`SyncErrorAlert`** — renders an `AppFieldError` as a localized alert. The message comes from
  `resolveErrorMessage` (`code` + `args`); the English `detail` is **never** displayed. When
  `args.marketplaceResponse` is present it is appended as a monospace block — that is the raw marketplace body,
  already truncated server-side.
- **`TestConnectionButton`** — probes credentials without saving them. `accountId="new"` works before the
  account exists, because the server ignores the route id when the body carries an `apiKey`.

### `CardImage`

The one place in the app where the image lives on a foreign host. Without a `src` it is a plain letter `Avatar`;
with one it wraps `FileImage` in external mode (direct `src`, `referrerPolicy="no-referrer"`, no resize) under a
tooltip and hover overlay, and opens `FileViewerModal` on click rather than a new tab. Click propagation is
stopped so it does not also trigger the surrounding row.

## Orders

### `OrdersListPage` slots

The orders list is shared by FBS, FBO and Direct, so type-specific behaviour arrives as props rather than an
internal `type === "fbs"` branch: `headerActions?`, `bulkActions?: (selectedIds: string[]) => ReactNode`,
`extraColumns?: {key, label, render}[]`, `marketplaceFilters?` and `showNotes?`. That keeps marketplace imports —
and the `integrations.sync` permission — out of the pages that have nothing to do with marketplaces.

`bulkActions` receives **every** selected id, not just the confirmed ones self-assign cares about, and the
selection toolbar appears whenever something is selected and either action set applies. Adding an
`extraColumns` entry also widens the loader/empty-row `colSpan`, which is computed rather than hard-coded —
as does dropping the notes column with `showNotes={false}` (FBO trades it for the posting number).

`marketplaceFilters` renders `MarketplaceOrderFilters` (marketplace / account / posting status) and is the only
thing that puts `marketplaceType`, `marketplaceAccountId` and `marketplaceStatus` into the query — Direct never
sends them. The three filters live in the URL as `marketplace`, `account` and `mpStatus`; `status` stays the WMS
status. Marketplace and account are cascaded: the account list is fetched scoped to the selected marketplace, so
switching marketplace clears the account id in the same tick (`setParam` batches both into one navigation).

The account picker never falls back to "Все аккаунты" while it is uncertain: `keepPreviousData` holds the old
list through the refetch, and on a deep link that carries `account=` before the first list arrives the Select
renders a temporary "Загрузка…" item for that id. Collapsing to the empty value would show a filter the URL and
the request do not agree with.

### `src/components/orders/marketplace/`

FBS-only pieces: `SyncOrdersButton` / `SyncOrdersDialog` / `SyncOrdersAccountAccordion` / `SkippedOrdersList`
(the import dialog and its per-account results), `DownloadLabelsButton` / `DownloadLabelsDialog` /
`DownloadOrderLabelButton` / `LabelsErrorDialog` / `useDownloadLabels`, `MarketplaceOrderStatusChip`,
`MarketplaceOrderFilters` (shared by FBS and FBO, reads `/accounts/short` so a warehouse role without
`integrations.view` still gets the account picker), and `marketplaceOrderUtils` for the label and colour maps.
The maps live here rather than in `MarketplacesSettingsPage/marketplaceUtils` so the operations tree never
imports from the settings tree.

Labels are downloaded from two places and both need the same request, so the call lives in the
`useDownloadLabels` hook: it calls `ordersGetLabels` with `parseAs: "blob"`, unwraps the error via
`parseProblemFromBlob` and returns it as `{message, postingNumbers}`. The components stay markup.

Printing fills `LabelFileId`, and button availability depends on it, so after a successful download the hook
invalidates the order list and every printed order's card — otherwise the button would stay greyed out until a
manual refresh.

- `DownloadLabelsButton` — the bulk button in the FBS list's selection toolbar. It opens `DownloadLabelsDialog`
  with a «Группировать по» choice (`Не группировать` / `По артикулам`; the choice survives a reload in
  `localStorage` under `orders-labels-grouping`) and sends **all** selected orders: the button *could* know in
  advance whether an order has a stored label, but filtering the user's selection for them is not its job — the
  server's refusal comes back with a clear message.
- `DownloadOrderLabelButton` — the button in the FBS order page header. A single order has nothing to group, so
  there is no dialog. The button is always visible but greys out when `labelFileId` is empty and the status is
  not `awaitingDeliver`, with a tooltip explaining why.
- `LabelsErrorDialog` — the refusal is shown as a modal rather than a snackbar: both `marketplaceLabelNotReady`
  and `marketplaceOrderNotAwaitingDeliver` carry a list of postings in `args.postingNumbers`, and a list of
  thirty numbers does not fit a snackbar and times out before it can be read. The last error is held in state
  until the closing animation finishes, otherwise the modal empties in front of the user.

### `FulfillmentsDrawer`

Read-only right-hand drawer listing what was actually picked for one order position: source cell breadcrumb,
inventory number for `Unit`, chosen variant for `Variation`, an expanded component table for `Bundle`, plus who
assembled it and when. Opened from the order page's «Коробки и состав» and «Задания на сборку» cards, and from
the eye `IconButton` on every component row of `OrdersAssemblyPage`.

Every catalog item inside the drawer is a `CatalogItemLink` opening a nested `CatalogItemDrawer`, held in the
`?fulfillmentCatalogItem=` param (an ephemeral param, since this drawer's own open state is local): the card
headline uses the optional `catalogItemId` prop — the position's own item, which `AssemblyFulfillmentDto` does
not carry, so callers pass it down; the «Вариант: …» row uses `resolvedCatalogItemId`; bundle rows use each
component's `catalogItemId`. Without `catalogItemId` the headline renders as plain text.

Helpers in `components/orders/orderAssemblyUtils.ts`:
- `countFulfilledQty(fulfillments)` — progress count; a `Unit`/`Bundle` fulfillment always counts as 1.
- `getFulfillmentKind(fulfillment)` — `"unit" | "bundle" | "standard"`, so the three call sites don't each
  re-derive it.
- `collectBoxComponentFulfillments(order, orderBoxId, catalogItemId)` — fulfillments hang off
  `AssemblyTaskBoxComponent`, so an order box component's ones have to be gathered across every assembly task
  that took on that box.

### `OrdersAssemblyPage` accordions

Every order in the assembly list is one `AssemblyOrderAccordion`, whatever its task count — the top level is
always the order, so rows line up.

Orders start collapsed. An order with several tasks renders one `AssemblyTaskAccordion` per task, each with its
own summary: batch checkbox, status chip, positions progress. The order summary carries the tasks counter.

An order with exactly **one** task carries that task's status chip in its own `AccordionSummary` and shows
`fulfilled/total позиций` instead of the tasks counter. The task itself is rendered as
`<AssemblyTaskAccordion inline />`: with `inline` the component returns just the body — boxes, component rows,
«Начать»/«Завершить» — without an `Accordion` of its own.

Every order summary opens with a batch-assembly checkbox covering all selectable tasks of that order: `checked`
when they are all selected, MUI's `indeterminate` when only some are, disabled when the order has none.
Toggling it calls `onTaskCheckChange` per selectable task, so the page keeps its flat `selectedTaskIds` set.

Selectable means `getBatchDisabledReason(task, eligible)` in `batchEligibility.ts` returns an empty string; that
reason is also the tooltip and disabled state of the per-task checkboxes.

The order summary is split into two groups: a head (checkbox, order number link, progress caption pushed right
with `margin-left: auto`) and a chip row (order type, warehouse, marketplace account, the sole task's status,
posting number). From `md` up both groups are `display: contents`, so their children join the summary's own flex
row and it reads as a single line; `order: 1` on the progress and `order: 2` on the posting number keep those
two at the end of it. Below `md` the summary becomes a column of the two groups, and the chip row wraps.

### `components/marketplace/MarketplaceAccountChip`

The marketplace account chip: account name, colored by `MARKETPLACE_TYPE_COLORS`, linking to
`/settings/integrations/{accountId}`. Takes `accountId` / `name` / `type` — the sources differ (an account
object, a flattened `MarketplaceOrderDto`) — plus an optional `search` for the link's query string
(`?tab=warehouses`, `?tab=cards&catalogItemId=…`), and passes the remaining `ChipProps` through. The click
`stopPropagation()`s, so it stays safe inside accordion summaries and clickable rows.

Call sites: `OrderMetaSection` («Магазин»), the `OrdersAssemblyPage` order row, `CatalogItemDrawer`
(«Привязан к карточкам») and `WarehouseViewPage` («Привязано к складам маркетплейсов»).

## Warehouse & scanning

### `features/warehouse/`

Read-only warehouse visualization shared between pages. `WarehouseCanvas` is a generic pan/zoom Konva canvas
managing its own refs, scale and auto-fit; its root is a `position: relative` full-size `Box`, so **the caller
must wrap it in a container with a fixed height**. The caller also decides the `fill` colour per storage place
and may override the text label — that is why the render item type is the component's own, not the DTO.

`StoragePlaceNodeTree` is a read-only `SimpleTreeView` that builds the tree internally from a flat list, and
optionally shows a coloured dot per node when `hasOrderItems` is present.

`WarehouseEditPage` has a **separate** `WarehouseCanvas` of its own with drag/resize/add behaviour — do not
confuse the two.

### `SortableNodeTree`

Drag-and-drop tree used only in `StoragePlaceDrawer` edit mode (`@dnd-kit`). Renders a recursive tree from a flat
`StoragePlaceNodeDto[]` (sorted by `order` then `name`); each sibling group is its own `SortableContext`.
**Only same-level reordering is allowed** — dragging across parent boundaries is a no-op. Fires
`onReorder(NodeOrderItem[])` with zero-based index positions for the affected siblings. `isDisabled` blanks all
actions and the drag cursor while API mutations are in flight.

### `ScannerBlock`

Orchestrates the full camera scan loop: acquires a stream via `useCameraStream` (the preferred device id
persists in `localStorage`), renders `ScanArea` / `ScanFrameOverlay` / `ZoomControls` / `CameraSelectDialog`,
and per frame captures to canvas → Otsu binarization + optional inversion → decodes with zxing-wasm (primary) or
the native `BarcodeDetector` (fallback). Decoded values arrive via `onScan`. Scan interval is configurable
(4–25 FPS equivalent).

### `StorageNodePickerContent`

Shared body of the storage-node picker dialogs (`components/shared/`). Four tabs:

| Tab | Behaviour |
|---|---|
| Карта | `WarehouseCanvas`; clicking a storage place selects it and jumps to the Схема tab |
| Схема | Storage place `Select` + `StoragePlaceNodeTree` scoped to it (parent nodes are not selectable) |
| Камера | `ScannerBlock` |
| Сканер | Hint text; the hardware scanner is bound globally via `useHardwareScanner` while `open` |

**Scanning is warehouse-wide, not limited to the storage place chosen in the Схема tab.** The picker loads
`GET /api/warehouses/{id}/print` while open, which returns every node of the warehouse as `{id, name: string[]}`
(full path, root-first). A scan is resolved with `parseEntityBarcode`; only `storagePlaceNode` payloads are
accepted, anything else fails with an inline `Alert`. On a hit the picker calls `onSelect` with the node's full
path and also switches the Схема `Select` to the owning storage place, matched by the path root (`name[0]`) —
the print DTO carries no storage place id, so places sharing a name inside one warehouse can switch the dropdown
to the wrong one (cosmetic only; the selected node is still correct).

Each failed scan bumps a `scanKey` that remounts `ScannerBlock` so the camera re-arms.

A scan that arrives before the node list has loaded does **not** report "не найдено" — it shows «Ячейки склада
ещё загружаются, повторите сканирование» instead. If the list request failed, the scan triggers a `refetch` and
asks the user to scan again.

### `useDefaultStorageNode(warehouseId, enabled?)`

Fetches the warehouse's default storage cell via `GET /api/warehouses/{id}/default-node`. Returns
`SelectedNode | null` — `null` while loading, on error, or when the warehouse has no default assigned. `enabled`
(default `true`) gates the query, e.g. to skip it for non-`standard` catalog item types that need no cell.

Used to pre-fill node pickers instead of requiring a manual pick every time — see
[orders-specification.md § Дефолтная ячейка склада](orders-specification.md#дефолтная-ячейка-склада-в-фулфилменте)
for the override-over-default merge used in `AddFulfillmentDialog` / `BatchAssemblyDialog` (directly
`setState`-ing from inside a `useEffect` trips the `react-hooks/set-state-in-effect` lint rule).
`AddPlacementDialog` calls the same endpoint directly, since it only seeds initial state once and never merges
with a live override.

## Forms

### `FormTextField`

Thin RHF + MUI `TextField` integration wrapping `Controller` and wiring `error`/`helperText` from `fieldState`.
**Use it in all RHF forms instead of a manual `Controller` + `TextField`.** For fields needing a custom
`InputAdornment` (a password show/hide toggle, for instance) drop to `Controller` directly.

```tsx
<FormTextField control={form.control} name="username" label="Логин"
  rules={{required: "Обязательное поле"}} fullWidth />
```

### `ClampedIntegerField`

Number `TextField` for editing a quantity **outside** of RHF (local state, or committed through a callback).
Keeps raw keystrokes uncommitted — including a temporarily empty field — until blur, so the min/max clamp does
not fight the user while they type or clear the field. Use this instead of hand-rolling
`Math.max(min, Number(e.target.value))` in an `onChange`, which snaps an emptied field back to the min on every
keystroke.

`min` defaults to **1**; pass `min={0}` wherever zero is a legitimate value (stocktake counting relies on this).
If `value` changes externally — after a mutation invalidates and refetches — the displayed text re-syncs,
**unless the field is currently focused**, so it never clobbers an in-progress edit.

