# Frontend Architecture

## Tech Stack

| Tool | Version | Role |
|------|---------|------|
| React | 19 | UI framework |
| TypeScript | 6 | Type safety |
| Vite | 8 | Build tool + dev server |
| MUI (Material UI) | v9 | Component library |
| Emotion | 11 | CSS-in-JS (MUI peer) |
| React Router | v7 | Client-side routing |
| TanStack React Query | v5 | Server state management |
| MobX + mobx-react-lite | 6 | Local complex edit state (used in RolesSettingsPage) |
| @dnd-kit/core + sortable | — | Drag-and-drop (used in RolesSettingsPage) |
| @hey-api/openapi-ts | 0.97+ | OpenAPI → TypeScript codegen |
| notistack | 3 | Toast notifications |
| zxing-wasm | 3 | Barcode/QR decoding (WASM) |
| vite-plugin-pwa | 1 | PWA + service worker |
| sass-embedded | — | Sass support |
| use-double-tap | 1 | Double-tap gesture (camera focus on mobile) |

## Directory Layout

```
src/
├── App.tsx                      # Root: providers, routing
├── main.tsx                     # Entry point
├── theme.ts                     # MUI theme (primary #1976d2)
├── index.css                    # Global reset
├── barcode.d.ts                 # BarcodeDetector API typings
│
├── assets/                      # Static images
│
├── components/
│   ├── form/
│   │   └── FormTextField.tsx    # RHF-wired TextField wrapper
│   ├── modals/
│   │   ├── ConfirmModal.tsx     # Reusable confirm modal (modal system)
│   │   └── AlertModal.tsx       # Reusable alert modal (modal system)
│   ├── ProtectedRoute/
│   │   ├── ProtectedRoute.tsx         # Single-permission route guard
│   │   ├── ProtectedRoutes.tsx        # Multi-permission route group guard
│   │   └── _protectedRouteMarker.ts   # Internal marker type
│   ├── ScannerBlock/
│   │   ├── ScannerBlock.tsx     # Camera scanner orchestrator
│   │   └── components/
│   │       ├── ScanArea.tsx            # <video> element + canvas
│   │       ├── ScanFrameOverlay.tsx    # Viewfinder + barcode rect overlay
│   │       ├── CameraSelectDialog.tsx  # Camera device picker dialog
│   │       ├── ZoomControls.tsx        # Zoom slider/buttons
│   │       └── index.ts
│   ├── InfoRow.tsx              # Label + value row used in detail views
│   ├── MainAppBar.tsx           # Top nav bar with logo and links
│   ├── InstallPrompt.tsx        # PWA "Add to Home Screen" prompt
│   ├── UpdatePrompt.tsx         # Service worker update banner
│   ├── AppBreadcrumbs.tsx       # Page breadcrumb trail
│   ├── PageGenericHeader.tsx    # Page header: title + filters + action buttons
│   ├── SearchInput.tsx          # TextField with search icon; extends TextFieldProps (omits onChange/value)
│   ├── FiltersBar.tsx           # Filters row: FilterAlt icon + "Фильтры:" label + children slot; extends StackProps
│   ├── DataTableContainer.tsx   # Paper + LinearProgress + TableContainer + TablePagination; extends PaperProps
│   ├── TableRowLoader.tsx       # Full-width TableRow with CircularProgress for loading state
│   ├── TableRowEmpty.tsx        # Full-width TableRow with message text for empty state
│   ├── ConfirmDialog.tsx        # Confirmation dialog with loading state
│   ├── AccessDenied.tsx         # 403 access denied placeholder
│   ├── NotFound.tsx             # 404 not found placeholder
│   ├── QueryError.tsx           # Generic query error placeholder (5xx, network)
│   └── QueryErrorHandler.tsx    # TanStack Query global error boundary
│
├── configuration/
│   └── flagsConstants.ts        # IS_DEV flag (enables console logs)
│
├── contexts/
│   ├── Auth/
│   │   ├── AuthContext.ts       # Context: user, login/logout, profileIsLoadError, profileLoadError
│   │   └── AuthProvider.tsx     # Fetches /me (suppressGlobalError), exposes AuthContext; clears full query cache on logout
│   ├── Modal/
│   │   ├── ModalContext.ts      # Context: open/close modals imperatively
│   │   └── ModalProvider.tsx    # Renders active modals, wires modalService
│   ├── SearchParams/
│   │   ├── SearchParamsContext.ts   # Context + useSearchParamsContext hook
│   │   └── SearchParamsProvider.tsx # Batched URL search params provider
│   └── ServiceWorker/
│       └── ServiceWorkerContext.ts  # Context: needRefresh, offlineReady, updateServiceWorker
│
├── layouts/
│   ├── MainLayout/
│   │   └── MainLayout.tsx       # Shell with MainAppBar + <Outlet />
│   ├── SidebarLayout/
│   │   └── SidebarLayout.tsx    # Visual layout: left sidebar (desktop) / top tabs (mobile) + children slot
│   └── SidebarPage/
│       ├── SidebarPage.tsx      # Routing wrapper: takes SectionConfig[], builds <Routes> + nav; exports createHasAccess
│       └── createFirstPageUrl.ts  # Factory: given SectionConfig[], returns (permissions) => firstVisiblePath
│
├── hooks/
│   ├── useAuth.ts                            # Current user + permission helpers
│   ├── usePermission.ts                      # Single-permission check hook
│   ├── useModal.ts                           # Open modals via ModalContext
│   ├── useFormErrors.ts                      # Map API error fields → RHF setError
│   ├── useRhfApiErrors.ts                    # RHF + API error wiring shorthand
│   ├── useDebounce.ts                        # Generic debounce: T → debounced T after delay ms
│   ├── useSyncedWithQueryState.ts            # Sync a typed value with a URL query param
│   ├── useDebouncedSyncedWithQueryState.ts   # Local state + debounce + URL sync in one hook
│   ├── useParamsState.ts                     # Merge debounced + immediate params for API queries
│   └── usePaginatedParams.ts                 # Pagination wrapper: page/pageSize from URL + page reset
│
├── pages/
│   ├── HomePage/
│   │   └── HomePage.tsx         # Landing page with navigation cards
│   ├── LoginPage/
│   │   └── LoginPage.tsx        # Login form
│   ├── MyProfilePage/
│   │   ├── MyProfilePage.tsx        # Current user's profile: info + roles + permissions (authenticated)
│   │   └── ChangePasswordDialog.tsx # Change own password dialog (requires current password)
│   ├── ScannerPage/
│   │   └── ScannerPage.tsx      # Full-screen scanner + scanned codes drawer
│   ├── UsersPage/
│   │   ├── UsersPage.tsx        # Paginated, searchable, filterable user list (requires users.view)
│   │   └── pages/
│   │       ├── UserCreatePage/
│   │       │   └── UserCreatePage.tsx   # Create user form (requires users.create)
│   │       ├── UserEditPage/
│   │       │   └── UserEditPage.tsx     # Edit profile + roles + permissions (requires users.edit_profile)
│   │       └── UserViewPage/
│   │           ├── UserViewPage.tsx          # Read-only user detail (requires users.view)
│   │           ├── ChangePasswordDialog.tsx  # Admin password reset dialog
│   │           └── DeleteUserDialog.tsx      # Delete confirmation dialog
│   ├── WarehousesPage/
│   │   ├── WarehousesPage.tsx   # Paginated, searchable warehouse list (no permission guard yet)
│   │   └── pages/
│   │       └── WarehouseViewPage/
│   │           ├── WarehouseViewPage.tsx    # Warehouse detail with storage places grid
│   │           └── StoragePlaceDialog.tsx   # Create/edit storage place dialog
│   └── SettingsPage/
│       ├── SettingsPage.tsx     # Sections declaration only — drives routes + sidebar nav for /settings/*
│       └── pages/
│           └── RolesSettingsPage/
│               ├── RolesSettingsPage.tsx   # Role-permission matrix table (observer, data fetching, QueryError on initial load, header buttons)
│               ├── RolesTable.tsx          # Sticky matrix table with @dnd-kit sortable columns
│               ├── RoleColumnHeader.tsx    # Header cell: drag handle + name + edit/delete actions
│               ├── RenameRoleDialog.tsx    # MUI Dialog for renaming a role (used via showModal)
│               ├── RolesStoreContext.tsx   # React context + provider for RolesStore
│               └── rolesStore.ts          # MobX store: EditableRole class + RolesStore
│
├── api/                         # Auto-generated — run `npm run generate-api` to refresh
│   ├── client/                  # Bundled fetch client (from @hey-api/openapi-ts)
│   │   ├── client.gen.ts
│   │   ├── types.gen.ts
│   │   ├── utils.gen.ts
│   │   └── index.ts
│   ├── core/                    # Generated runtime internals (auth, serializers, SSE)
│   ├── client.gen.ts            # Client singleton
│   ├── types.gen.ts             # TypeScript types from OpenAPI schema
│   ├── sdk.gen.ts               # Typed SDK functions for all endpoints
│   ├── index.ts                 # Re-exports everything
│   └── @tanstack/
│       └── react-query.gen.ts   # queryOptions / mutationOptions factories
│
├── services/
│   ├── apiClient.ts             # Client config, token storage, refresh interceptor
│   └── modalService.ts          # Imperative modal open/close (used outside React tree)
│
└── utils/
    ├── camera/
    │   ├── useCameraStream.ts   # Hook: getUserMedia, device selection
    │   ├── useCameraFocus.ts    # Hook: focus via MediaStreamTrack
    │   ├── useCameraZoom.ts     # Hook: zoom via MediaStreamTrack capabilities
    │   ├── cameraUtils.ts       # Device enumeration, constraint helpers
    │   └── index.ts
    ├── qrTools.ts               # zxing-wasm decode + Otsu binarization + BarcodeDetector fallback
    ├── errorUtils.ts            # API error shape helpers: extractErrorMessage, isNotFoundError, isAppProblemDetails; errorCodeMessages map (supports {placeholder} interpolation from error args)
    ├── parseJwt.ts              # Decode JWT payload without verification
    ├── permissionLabels.ts      # Human-readable labels for permission enum values
    ├── printUtils.ts            # openPrintPage(items) helper — builds URL and opens /print in a new tab
    └── useInstallPrompt.ts      # Hook: beforeinstallprompt event
```

## Routing

`BrowserRouter` in `main.tsx`. Pages are lazy-loaded via `React.lazy` + `Suspense`. Access control is handled by `ProtectedRoute` / `ProtectedRoutes` components; unauthenticated users are redirected to `/login`.

```
/login             → LoginPage               (public)
/scanner           → ScannerPage             (authenticated, no layout wrapper)
/print             → PrintPage               (authenticated, no layout wrapper)
/                  → MainLayout > HomePage       (authenticated)
/profile           → MainLayout > MyProfilePage  (authenticated)
/users             → MainLayout > UsersPage         (users.view)
/users/new         → MainLayout > UserCreatePage    (users.create)
/users/:id         → MainLayout > UserViewPage      (users.view)
/users/:id/edit    → MainLayout > UserEditPage      (users.edit_profile)
/settings/*        → MainLayout > SettingsPage      (authenticated)
/settings          →   redirect to /settings/roles
/settings/roles    →   RolesSettingsPage            (roles.view)
```

## Pages

### `HomePage`
Landing page. Shows navigation cards (Warehouses — gated by `warehouses.view`, Scanner), PWA offline-ready indicator, and the `InstallPrompt` when the app is installable. Cards are rendered via the local `HomeCard` component (title, link, linkText, icon).

### `ScannerPage`
Full-screen camera scanner. Uses `ScannerBlock` for capture/decode. Maintains a list of scanned barcodes in local state; opens a bottom drawer when `?scannedCodesDrawerOpen=true` is in the query string.

### `PrintPage`
Print-ready label sheet generator at `/print`. Reads `?item=TYPE:VALUE|LABEL` query params (repeatable, batch) and renders a grid of barcode/datamatrix labels. Supported types: `DataMatrix`, `EAN13`, `Code128`, `QR`. Uses `bwip-js` for canvas rendering.

Query param format: `TYPE:VALUE` or `TYPE:VALUE|LABEL` — pipe separates value from an optional human-readable label shown above the barcode. The value may contain colons (e.g. URLs).

Items are loaded from the URL once into local state on mount; the list is not reactive to subsequent URL changes. This allows removing individual labels before printing without navigating away.

Each label is rendered by `BarcodeLabel` — a `<canvas>` element via `bwip-js.toCanvas()`. Optional label text is shown above the canvas in bold; the raw value is shown below in small print. Invalid codes (e.g. EAN-13 with wrong digit count) show an inline error.

Each label card has a floating **×** `IconButton` in the top-right corner that removes that label from the list. The button is hidden via `@media print` (CSS class `delete-btn`).

Print layout is controlled by `PrintSettings` (hidden on print via `@media print`):
- **Preset selector** — built-in presets (A4 4×7, A4 2×5, A5 2×4, Термо 58мм) plus user-saved custom presets stored in `localStorage` under `print-page-presets`. Last selected preset is restored from `print-page-last-preset`.
- **Manual fields** — label width/height (mm), columns, gap, page padding.
- **Save preset** — saves current settings as a named custom preset; custom presets can be deleted.

To open the print page programmatically use `openPrintPage(items)` from `@/utils/printUtils`.

Example URL: `/print?item=DataMatrix:ABC123|Товар А&item=EAN13:5901234123457&item=Code128:HELLO&item=QR:test`

### `UsersPage`
Server-side paginated, searchable, and filterable table of users. Requires `users.view` permission. State is stored in URL params (`?search=`, `?role=`, `?page=`, `?pageSize=`) using `useDebouncedSyncedWithQueryState` + `useSyncedWithQueryState` + `usePaginatedParams`. The search field updates instantly without lag; the URL and API call update after a 300 ms debounce. A roles filter (`RolesSelect`) is shown in a `FiltersBar` below the header. Rows are clickable and navigate to `UserViewPage`.

### `WarehousesPage`
Server-side paginated and searchable table of warehouses. State is stored in URL params (`?search=`, `?page=`, `?pageSize=`) via `useDebouncedSyncedWithQueryState` + `usePaginatedParams`. Uses `SearchInput`, `DataTableContainer`, `TableRowLoader`, and `TableRowEmpty`. Rows navigate to `WarehouseViewPage`.

### `UserViewPage`
Read-only detail view for a single user. Displays username, email, first/last name, assigned roles (chips), and direct permissions (chips). Action buttons: **Редактировать** → `UserEditPage`, **Сменить пароль** → opens `ChangePasswordDialog`, **Удалить** → opens `DeleteUserDialog`. Requires `users.view`.

On query error: renders `<NotFound />` for 404, `<QueryError />` for everything else. The `usersGetById` query sets `suppressGlobalError: true` and `suppressGlobalNotFound: true` so the global modal is never shown alongside these inline states. Error screens are only shown on the initial load — background refetch errors (`isRefetchError`) are ignored so a transient network blip doesn't replace visible data with an error screen.

### `UserEditPage`
RHF form for editing a user's profile (email, first/last name) and, if the current user has `users.manage_roles`, also roles (typeahead via `rolesSearch` API) and direct permissions (multi-select from `permissionsGetAll`). Pre-populated from `usersGetById`; refetches on window focus without losing unsaved edits (`keepDirtyValues: true`). Requires `users.edit_profile`.

Same error handling as `UserViewPage`: `<NotFound />` on 404, `<QueryError />` otherwise; refetch errors are suppressed.

### `MyProfilePage`
Read-only profile page for the currently authenticated user. Displays username, email, first/last name, assigned roles (chips), and effective permissions (chips with tooltip showing the raw permission string). Action button: **Сменить пароль** → opens `ChangePasswordDialog`. Accessible to all authenticated users at `/profile` via the user avatar menu in `MainAppBar`.

`ChangePasswordDialog` is a modal form with two fields: current password and new password. Submits to `authChangeOwnPassword`. On success it closes and resets the form; on error, API errors are mapped back to form fields via `useRhfApiErrors`. Backdrop-click dismiss is disabled while the mutation is pending.

Error handling mirrors `UserViewPage`: `<NotFound />` on 404, `<QueryError />` otherwise; refetch errors are suppressed. Both `suppressGlobalError` and `suppressGlobalNotFound` are set on the `/me` query to prevent global modals.

### `UserCreatePage`
RHF form for creating a new user. Fields: username (required), password (required, with show/hide toggle), email, first name, last name. On success navigates to the new user's `UserViewPage`. Requires `users.create`.

## Key Components

### `ScannerBlock`
Orchestrates the full camera scan loop:
1. Acquires a camera stream via `useCameraStream` (persists preferred device ID in `localStorage`)
2. Renders `ScanArea` (video element), `ScanFrameOverlay` (viewfinder rect), `ZoomControls`, `CameraSelectDialog`
3. On each frame: captures to canvas → runs Otsu binarization + optional inversion → decodes with zxing-wasm (primary) or native `BarcodeDetector` (fallback)
4. Emits decoded barcodes via `onScan` callback

Scan interval is configurable (4–25 FPS equivalent).

### `MainAppBar`
Top navigation bar. Logo/title + mobile hamburger menu with permission-filtered links. Each entry in the `pages` array supports `requiredPermission` (must match a user permission), `showIf` (arbitrary boolean predicate), and `url` — which can be a plain string **or** a `(permissions: PermissionName[]) => string` factory (used by "Настройки" to link directly to the first accessible settings page via `getSettingsFirstPageUrl`). If the `/me` profile query fails, an inline red error message is shown in the user avatar dropdown via `profileIsLoadError`/`profileLoadError` from `AuthContext`.

### `SidebarLayout`
Generic visual layout for pages with a left-panel navigation. On desktop (md+) renders a MUI `List` sidebar with a right border; on mobile renders scrollable MUI `Tabs` at the top. Takes `navItems: SidebarNavItem[]` (leaves with `path`, or groups with `defaultPath` + `children` + optional `icon`) and `children` (the content area). Active item detection uses `matchPath({ end: false })` so sub-routes highlight the parent item.

On mobile, **groups are expanded into individual child tabs** (each child gets its own `<Tab>`), so every route is directly reachable. Desktop sidebar groups render the group label with its optional icon as a non-selected header link, with children indented below it.

### `SidebarPage`
Higher-level routing wrapper built on top of `SidebarLayout`. Takes a `sections: SectionConfig[]` declaration and a `basePath` string, and automatically:
- Builds `SidebarNavItem[]` filtered by user permissions and `showIf`
- Creates `<Routes>` with relative paths (leaf routes, subroutes, and redirect routes for groups)
- Groups with no `component` redirect to their first visible child at runtime
- Wraps every rendered route in `ProtectedRoute` — renders `<AccessDenied />` if the section's `requiredPermission` is not met at render time (double-checks beyond nav filtering)

**To create a new sidebar-based page**, declare a `SectionConfig[]`, call `createHasAccess(sections)` to get an AppBar visibility helper, call `createFirstPageUrl(sections)` to get a permission-aware deep link factory, and render `<SidebarPage sections={...} basePath="..." />`. See `SettingsPage.tsx` for the reference implementation.

**`SectionConfig` fields:**
| Field | Type | Description |
|---|---|---|
| `label` | `string` | Nav item label |
| `path` | `string` | Relative path segment (e.g. `"roles"`) |
| `component` | `ComponentType?` | Page component; absent → redirect to first visible child |
| `requiredPermission` | `PermissionName?` | Hides item if user lacks this permission |
| `showIf` | `() => boolean?` | Additional visibility predicate (feature flags etc.) |
| `subroutes` | `SectionSubroute[]?` | Sub-paths (e.g. `":id"`) that highlight the parent nav item |
| `children` | `SectionConfig[]?` | Nested nav sections (max depth 1); section becomes a group |
| `icon` | `React.ReactElement?` | Icon shown in the sidebar group header (desktop only) |

### `SearchInput`
Controlled `TextField` with a `SearchIcon` start adornment and `size="small"` default. Accepts a plain `(value: string) => void` onChange instead of the raw event. Extends `TextFieldProps` (omits `onChange` and `value`) — all other TextField props (e.g. `sx`, `fullWidth`, `size`) pass through. The search icon is not overrideable.

```tsx
<SearchInput value={inputValue} onChange={setInputValue} />
<SearchInput value={inputValue} onChange={setInputValue} label="Поиск по имени" sx={{width: 300}} />
```

Props: `value: string`, `onChange: (value: string) => void`, `label?: string` (default `"Поиск"`), plus all `TextFieldProps` except `onChange`/`value`.

### `FiltersBar`
Horizontal row with a `FilterAltIcon` + `"Фильтры:"` label and a children slot for filter controls. Extends `StackProps` (omits `direction` and `spacing`). The `sx` prop is **merged** (not replaced) with the default `{ alignItems: "center" }` via MUI's array `sx` syntax.

```tsx
<FiltersBar>
  <RolesSelect value={roleId} onChange={setRoleId} size="small" />
</FiltersBar>
```

Props: `children: React.ReactNode`, plus all `StackProps` except `direction`/`spacing`.

### `DataTableContainer`
Standard list-page table shell: `Paper` → `LinearProgress` (visible while fetching) → `TableContainer` → `TablePagination` with Russian labels baked in. Extends `PaperProps` — pass `elevation`, `sx`, etc. directly. The `page` prop is **1-based** (matches `usePaginatedParams` convention); the component converts internally for MUI.

```tsx
<DataTableContainer
  isFetching={isFetching}
  count={data?.total ?? 0}
  page={page}
  onPageChange={setPage}
  rowsPerPage={pageSize}
  onRowsPerPageChange={setPageSize}
>
  <Table size="small">...</Table>
</DataTableContainer>
```

Props: `isFetching: boolean`, `count: number`, `page: number` (1-based), `onPageChange: (page: number) => void`, `rowsPerPage: number`, `onRowsPerPageChange: (rowsPerPage: number) => void`, `rowsPerPageOptions?: number[]` (default `[10, 20, 50]`), `children: React.ReactNode`, plus all `PaperProps` except `children`.

### `TableRowLoader`
Single `TableRow` spanning `colSpan` columns with a centered `CircularProgress`. Place inside `<TableBody>` as the loading branch.

```tsx
{isLoading ? <TableRowLoader colSpan={5} /> : ...}
```

Props: `colSpan: number`.

### `TableRowEmpty`
Single `TableRow` spanning `colSpan` columns with a centered `Typography` message. Place inside `<TableBody>` as the empty-results branch.

```tsx
{data?.items.length === 0 ? <TableRowEmpty colSpan={5} message="Пользователи не найдены" /> : ...}
```

Props: `colSpan: number`, `message: string`.

### `InfoRow`
Simple label + value row used in detail views (`UserViewPage`, `MyProfilePage`).

```tsx
<InfoRow label="Email" value={user.email ?? "—"} />
```

Props: `label: string`, `value: string`. The label column is fixed at 160 px.

### `AppBreadcrumbs`
Renders a MUI `Breadcrumbs` trail from an array of `{ name, link? }` objects. Items with a `link` render as React Router `<Link>`; the last item is plain text. Used at the top of every page.

```tsx
<AppBreadcrumbs path={[
  {name: "Пользователи", link: "/users"},
  {name: user.username, link: `/users/${id}`},
  {name: "Редактировать"},
]} />
```

Props: `path: Array<{ name: string; link?: string }>`.

### `PageGenericHeader`
Three-zone page header: title (`h5`, left), optional middle slot for search/filters, optional right slot for action buttons. On mobile (`< md`) all zones stack vertically.

```tsx
<PageGenericHeader
  title="Пользователи"
  right={<Button component={RouterLink} to="/users/new">Создать</Button>}
>
  <TextField label="Поиск..." />
</PageGenericHeader>
```

Props: `title: React.ReactNode`, `children?: React.ReactNode` (middle slot), `right?: React.ReactNode`.

### `AccessDenied`
Full-page placeholder rendered when the user lacks permission. Shows a lock icon, a short message, and a "Вернуться назад" button that navigates to `/`. Used by `ProtectedRoute`.

### `NotFound`
Full-page placeholder rendered when a requested resource does not exist (HTTP 404). Shows a search-off icon, a short message, and a "Вернуться назад" button that navigates to `/`. Used inline in pages that fetch a single resource by ID.

### `QueryError`
Full-page placeholder rendered when a query fails with a non-404 error (e.g. 502, network failure). Accepts an optional `error` prop and displays the human-readable message via `extractErrorMessage`. Use alongside `NotFound` to cover all error branches.

```tsx
if (query.isError)
  return isNotFoundError(query.error) ? <NotFound /> : <QueryError error={query.error} />;
```

Props: `error?: unknown`.

### `QueryErrorHandler`
Subscribes to the TanStack Query cache and surfaces unhandled query/mutation errors as modal alerts. Skips a query error if:
- `meta.suppressGlobalError` is `true` — suppresses the modal for all errors on this query
- `meta.suppressGlobalNotFound` is `true` — suppresses the modal only for 404 errors

Use `suppressGlobalError: true` together with `suppressGlobalNotFound: true` when the page renders `<QueryError />` inline (to avoid showing both the inline component and the global modal).

Also listens for the `auth:refreshTokenInvalid` window event and shows a session-expired warning.

### `ConfirmDialog`
Generic confirmation dialog with a loading spinner on the confirm button. Blocks backdrop-click dismiss while `isPending`. Used for all destructive confirm flows.

```tsx
<ConfirmDialog
  open={open}
  onClose={onClose}
  title="Удалить запись?"
  onConfirm={handleDelete}
  isPending={mutation.isPending}
  confirmText="Удалить"
  confirmColor="error"
>
  <Typography>Это действие нельзя отменить.</Typography>
</ConfirmDialog>
```

Props: `open`, `onClose`, `title`, `children?` (body), `onConfirm`, `isPending?`, `confirmText?` (default `"Подтвердить"`), `confirmColor?` (default `"primary"`), `maxWidth?` (default `"xs"`).

### `ObservableForm<TFieldValues>`
A class that creates a bidirectional bridge between a **react-hook-form** instance and **MobX**. It holds `_data` — a MobX observable snapshot of the form values — and keeps it in sync with the RHF form in both directions via a `watch` subscription (RHF → MobX) and a MobX `reaction` (MobX → RHF). A `_syncing` flag prevents feedback loops.

**When to use:** when a page uses a MobX store alongside an RHF form and you need other store computeds or reactions to react to form field changes, or you need to push external data (e.g. an API response) back into the form from the store.

```ts
// In the store
class MyStore {
  form = new ObservableForm<MyFormValues>();

  loadData = async () => {
    const data = await fetchSomething();
    this.form.data = data; // pushes into RHF via setValue / reset
  };
}

// In the component
const rhf = useForm<MyFormValues>();
useEffect(() => store.form.init(rhf), []);

// In an observer component or MobX reaction — reactive read:
const value = store.form.data?.someField;
```

**Key behaviour:**
- `init(deps)` — connects to RHF; must be called once inside `useEffect`. Returns a cleanup function — return it from the effect so subscriptions are torn down on unmount.
- `data` getter — MobX-observable; reading it inside an `observer` / `computed` / `reaction` makes that context re-run on any field change.
- `data` setter — replaces all form values; changed fields are applied via `setValue`, a full-object replacement falls back to `reset` (preserves dirty/touched/error state). Throws if called before `init`.

**MobX → RHF detail:** uses `recursive-diff` to compute the minimal set of changed paths and calls `setValue` only for those paths. When the diff touches the root (e.g. the entire object was replaced), falls back to `reset` with `keepDirtyValues`, `keepErrors`, `keepDirty`, `keepIsSubmitted`, `keepTouched`, `keepIsValid`, and `keepSubmitCount` all set to `true` to preserve form state as much as possible.

### `FormTextField`
Thin RHF + MUI `TextField` integration. Wraps `Controller` and automatically wires `error` and `helperText` from `fieldState`. Use in all RHF forms instead of manual `Controller` + `TextField`.

```tsx
<FormTextField
  control={form.control}
  name="email"
  label="Email"
  fullWidth
/>
<FormTextField
  control={form.control}
  name="username"
  label="Логин"
  rules={{required: "Обязательное поле"}}
  fullWidth
/>
```

Props: `control`, `name` (type-safe `Path<T>`), `rules?`, `helperText?` (shown when no error) — plus all MUI `TextFieldProps` except `error`/`helperText` (managed internally). For fields with custom `InputAdornment` (e.g. password show/hide toggle) use `Controller` directly.

## URL State Hooks

A set of hooks for managing page state via URL search params, enabling bookmarkable and shareable URLs.

### `SearchParamsProvider`
Wraps pages that use URL-synced state (mounted in `MainLayout`). Batches all `setParam` calls within the same synchronous tick into a single `setSearchParams` navigation via `queueMicrotask`, preventing concurrent hook updates from overwriting each other.

### `useDebounce<T>(value, delay?)`
Generic debounce hook. Returns the debounced copy of `value`; updates only after `delay` ms of inactivity (default 300 ms).

```typescript
const debouncedQuery = useDebounce(inputValue, 300);
```

### `useSyncedWithQueryState(key, fromQuery, toQuery)`
Binds a typed state value to a single URL query param. Returns `[value, setValue]`; `setValue` writes to the URL via `SearchParamsProvider` (batched via `queueMicrotask`).

```typescript
const [search, setSearch] = useSyncedWithQueryState(
  "search",
  (q) => (typeof q === "string" ? q : ""),
  (v) => v || null,
);
```

### `useDebouncedSyncedWithQueryState(key, fromQuery, toQuery, delay?)`
Combines local state, `useDebounce`, and `useSyncedWithQueryState` into one hook for lag-free inputs that sync to the URL after a debounce. Returns `[localValue, setLocalValue, urlValue]`.

- `localValue` / `setLocalValue` — bind to the input element (updates instantly, no re-navigation per keystroke)
- `urlValue` — debounced URL-synced value; pass this to API query params
- Syncs `localValue` back from the URL when it changes externally (browser back/forward, deep link)

```typescript
const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
  "search",
  (q) => (typeof q === "string" ? q : ""),
  (v) => v || null,
);
// inputValue → TextField value
// searchString → usePaginatedParams immediateParams
```

### `useParamsState(debouncedParams, debouncedDeps, immediateParams, delay?)`
Merges debounced and immediate params into one object. Debounced params settle after `delay` ms (default 300 ms); immediate params are always current. Use the merged result as query options.

### `usePaginatedParams(debouncedParams, debouncedDeps, immediateParams?, immediateDeps?, options?)`
Pagination wrapper that manages `page` and `pageSize` from the URL. Resets `page` to 1 atomically when debounced params settle or immediate params change (prevents a spurious API call with new filters + old page). Syncs `page` back from the URL on browser back/forward navigation.

```typescript
const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
  {},
  [],
  {searchString},  // immediate params — bypass internal debounce, also reset page
  [searchString],
);
```

### `InstallPrompt` / `UpdatePrompt`
PWA lifecycle UI. `InstallPrompt` triggers `beforeinstallprompt`. `UpdatePrompt` calls `updateServiceWorker()` from `ServiceWorkerContext` when a new SW version is available.

## Providers (in `App.tsx`)

```
ServiceWorkerContext.Provider
  └── ThemeProvider (MUI)
        └── SnackbarProvider (notistack)
              └── ModalProvider
                    ├── QueryErrorHandler    (self-closing — global query error handler)
                    ├── CssBaseline          (self-closing — global CSS reset)
                    ├── UpdatePrompt
                    └── AuthProvider
                          └── Suspense
                                └── ProtectedRoutes
                                      └── Routes (lazy pages)
```

## PWA

`vite-plugin-pwa` with `registerType: "prompt"` — user is prompted before SW update, not auto-updated.

Workbox caching strategy:
- All static assets → precached (`globPatterns: ["**/*"]`)
- `/api/*` → `NetworkOnly` (never cached)

Manifest: name "Project Warehouse", theme `#1976d2`, standalone display.

## Dev Proxy

Vite (`vite.config.ts`) proxies these paths to the backend at `https://localhost:7095`:

```
/api/*       → https://localhost:7095/
/openapi/*   → https://localhost:7095/
/scalar/*    → https://localhost:7095/
```

This means frontend code can call `/api/auth/login` without CORS or hardcoded URLs.

## Path Alias

`@` → `./src`. Import as `import foo from "@/utils/qrTools"`.

## API Client

TypeScript client is auto-generated from the backend's OpenAPI schema using `@hey-api/openapi-ts`.

### Regenerating

```bash
# Backend must be running first
npm run generate-api
```

Reads from `https://localhost:7095/openapi/v1.json` (dev cert TLS check is bypassed for the CLI only). Outputs to `src/api/`. Generated files are committed to git.

### Runtime setup

`setupApiClient()` is called once in `main.tsx` before `ReactDOM.createRoot`. It:
- Sets `baseUrl` to `/api` (Vite proxy routes this to the backend)
- Installs a request interceptor that proactively refreshes the JWT access token when < 30s of its lifetime remains
- Installs a response interceptor that on a 401 attempts to refresh tokens and retry the request. If no `accessToken` is in `localStorage` the response is passed through without a refresh attempt (avoids spurious refresh on unauthenticated requests). Clears stored tokens when the refresh token is also invalid.

### Token storage (`src/services/apiClient.ts`)

Three `localStorage` keys, managed by `storeTokens()` / `clearTokens()`:

| Key | Value |
|-----|-------|
| `accessToken` | JWT bearer token |
| `refreshToken` | Opaque refresh token |
| `tokenExpiry` | Unix ms timestamp when the access token expires |

Call `storeTokens(tokenResponse)` after a successful login (auth context handles this). Call `clearTokens()` on logout.

### Using generated hooks

```typescript
import { useQuery } from '@tanstack/react-query';
import { getApiAuthMeOptions } from '@/api/@tanstack/react-query.gen';

function MyComponent() {
  const { data, error } = useQuery(getApiAuthMeOptions());
  // data is typed as MeResponse
}
```

For mutations:

```typescript
import { useMutation } from '@tanstack/react-query';
import { postApiAuthLoginMutation } from '@/api/@tanstack/react-query.gen';

const login = useMutation({
  ...postApiAuthLoginMutation(),
  onSuccess: (data) => storeTokens(data),
});
```
