# Frontend Architecture

## Tech Stack

| Tool | Version | Role |
|------|---------|------|
| React | 19 | UI framework |
| TypeScript | 7 (native Go compiler) | Type safety |
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

### TypeScript 7 tooling note

The `typescript` package is aliased to `npm:@typescript/typescript6` because `typescript-eslint`'s
programmatic API support doesn't cover TS 7 yet (its `typescript` peer range is `<6.1.0`). The real
TS 7 (native Go) compiler is installed under the `typescript-7` alias and used only for type-checking
via `npm run typecheck` (`node ./node_modules/typescript-7/bin/tsc -b`, wired into `npm run build`
and documented in `CLAUDE.md`). `npx --package typescript-7 tsc` does **not** work for this alias — npx resolves
`--package` by the package's own internal name, not the local alias key, and falls back to fetching
a nonexistent `typescript-7` from the registry. Once `typescript-eslint` supports TS 7, drop the
`@typescript/typescript6` alias and use `typescript@^7` directly for both linting and building.

Both aliased packages declare a `tsc` bin, so `node_modules/.bin/tsc` (and therefore bare `tsc -b` /
`npx tsc`) resolves to whichever package npm linked last — currently `typescript-7`, but this is an
npm install-order artifact, not a guaranteed contract, and can silently flip to TS 6 on a different
npm version or lockfile state. Always use `npm run typecheck` (or invoke
`node ./node_modules/typescript-7/bin/tsc` directly) rather than relying on `tsc`/`npx tsc` to pick
the right version.

`strict` is explicitly set to `true` in `tsconfig.app.json`/`tsconfig.node.json` (verified clean —
0 errors — against the full codebase when enabled during the TS 7 migration).

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
├── features/
│   ├── catalog/
│   │   ├── catalogItemTypes.ts    # CATALOG_ITEM_TYPE_CONFIG + CATALOG_ITEM_TYPES — shared type metadata (label, chip color)
│   │   └── index.ts
│   └── warehouse/
│       ├── WarehouseCanvas.tsx         # Generic pan/zoom Konva canvas for warehouse floor plans
│       ├── StoragePlaceNodeTree.tsx    # Read-only SimpleTreeView of storage place nodes
│       └── index.ts
│
├── components/
│   ├── catalog/
│   │   ├── CatalogItemDrawer.tsx        # Reusable right-drawer: view + edit any catalog item (all types)
│   │   ├── CatalogItemDrawerHost.tsx    # One CatalogItemDrawer per page, open fn shared via context
│   │   ├── CatalogItemDrawerContext.ts  # Context + useOpenCatalogItem() for the host
│   │   ├── CatalogItemLink.tsx          # Clickable catalog item label with hover OpenInNew icon
│   │   └── CatalogItemTypeChip.tsx      # MUI Chip mapping CatalogItemType → label + color
│   ├── receipts/
│   │   ├── ReceiptStatusChip.tsx       # MUI Chip for ReceiptStatus (color per status)
│   │   ├── ReceiptItemsSection.tsx     # Collapsible per-item section: planned/received counts + placements table
│   │   ├── ReceiptItemsEditorDrawer.tsx # Right drawer: edit expected items list (add/remove catalog items + plannedCount)
│   │   ├── AddPlacementDialog.tsx      # Dialog: place items at a node (standard / unit / assembled-bundle)
│   │   ├── SelectNodeModal.tsx         # Modal for selecting a storage place node (warehouse schema or tree)
│   │   └── receiptUtils.ts            # RECEIPT_REASON_LABELS, formatReceiptNumber helpers
│   ├── inventory/
│   │   ├── ItemsBasePage.tsx        # Reusable inventory table: search, filters (type/archive/warehouse), pagination, row-click drawers
│   │   └── UnitItemsDrawer.tsx      # Bottom drawer: paginated list of individual UnitInventoryItem instances for a clicked catalog item
│   ├── CatalogItemsSelect.tsx   # Autocomplete for catalog items; single (id/dto) or multi (dto[]) mode; supports type filter
│   ├── form/
│   │   ├── FormTextField.tsx        # RHF-wired TextField wrapper
│   │   └── ClampedIntegerField.tsx  # Number TextField that only clamps min/max on blur, not on keystroke
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
│   ├── usePaginatedParams.ts                 # Pagination wrapper: page/pageSize from URL + page reset
│   └── useDrawerSearchParamsState.ts         # Drawer/dialog open state via URL param; supports back-button close
│
├── pages/
│   ├── HomePage/
│   │   └── HomePage.tsx         # Landing page; navigation cards driven by AppEntity from /api/home
│   ├── CatalogPage/
│   │   └── CatalogPage.tsx      # Paginated, searchable catalog item list (catalog.view); opens CatalogItemDrawer
│   ├── InventoryPage/
│   │   └── InventoryPage.tsx    # Global stock overview (warehouses.view); uses ItemsBasePage with warehouse filter
│   ├── LoginPage/
│   │   └── LoginPage.tsx        # Login form
│   ├── MyProfilePage/
│   │   ├── MyProfilePage.tsx        # Current user's profile: info + roles + permissions (authenticated)
│   │   └── ChangePasswordDialog.tsx # Change own password dialog (requires current password)
│   ├── ScannerPage/
│   │   └── ScannerPage.tsx      # Full-screen scanner + scanned codes drawer
│   ├── UsersPage/               # Reused inside SettingsPage at /settings/employees
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
│   │       ├── WarehouseViewPage/
│   │       │   ├── WarehouseViewPage.tsx    # Warehouse detail with pan/zoom storage place grid; "Остатки", "Этикетки", "Редактировать" buttons
│   │       │   ├── StoragePlaceDrawer.tsx   # Right-drawer: node tree (SimpleTreeView) + NodeDetails panel; "Остатки", "Остатки ячейки", "Редактировать ячейки" buttons
│   │       │   ├── SortableNodeTree.tsx     # Drag-and-drop sortable node tree (@dnd-kit); used in edit mode inside StoragePlaceDrawer
│   │       │   └── NodeDetails.tsx          # Node detail panel: view/edit item groups (catalog item + characteristic + count)
│   │       ├── WarehouseEditPage/
│   │       │   ├── WarehouseEditPage.tsx    # Full warehouse editor: canvas-based layout edit + metadata form; uses MobX WarehouseEditStore
│   │       │   ├── warehouseEditStore.ts    # MobX store: canvas state, drag/resize/add/delete storage places
│   │       │   ├── WarehouseEditStoreContext.tsx
│   │       │   └── components/
│   │       │       ├── WarehouseCanvas.tsx        # Konva canvas — edit-mode variant with drag/resize/add storage places
│   │       │       ├── WarehouseMetaForm.tsx      # Name/dimensions/notes form embedded in the edit page
│   │       │       ├── WarehouseEditToolbar.tsx   # Toolbar: add storage place, save, undo buttons
│   │       │       ├── ObjectPropertiesDialog.tsx # Dialog for editing selected storage place properties
│   │       │       └── DeleteWarehouseDialog.tsx  # Confirm dialog for warehouse deletion
│   │       ├── WarehouseNewPage/
│   │       │   └── WarehouseNewPage.tsx    # Create warehouse form (requires warehouses.edit)
│   │       ├── WarehouseInventoryPage/
│   │       │   └── WarehouseInventoryPage.tsx  # Stock overview scoped to one warehouse; uses ItemsBasePage with warehouseId
│   │       ├── StoragePlaceInventoryPage/
│   │       │   └── StoragePlaceInventoryPage.tsx  # Stock overview scoped to one storage place; uses ItemsBasePage with warehouseId + storagePlaceId
│   │       ├── NodeInventoryPage/
│   │       │   └── NodeInventoryPage.tsx   # Stock overview scoped to one node; uses ItemsBasePage with all 3 IDs
│   │       └── WarehouseItemsPage/
│   │           └── WarehouseItemsPage.tsx   # Paginated, searchable table of all item groups in a warehouse; link-to-catalog per row
│   ├── StoragePage/             # /storage/* — SidebarPage module (Склады + Остатки)
│   │   ├── StoragePage.tsx      # SidebarPage wrapper for /storage/*
│   │   └── storageConfig.tsx    # storageSections, hasStorageAccess, getStorageFirstPageUrl
│   ├── OperationsPage/          # /operations/* — SidebarPage module (Приемки + Заказы + Перемещения + Списания)
│   │   ├── OperationsPage.tsx   # SidebarPage wrapper for /operations/*
│   │   ├── operationsConfig.tsx # operationsSections, hasOperationsAccess, getOperationsFirstPageUrl
│   │   └── pages/
│   │       ├── OrdersDirectPage/
│   │       │   └── OrdersDirectPage.tsx     # Paginated, searchable list of direct orders
│   │       ├── OrderDirectCreatePage/
│   │       │   └── OrderDirectCreatePage.tsx # RHF form for creating a direct order
│   │       ├── OrderPage/
│   │       │   ├── OrderPage.tsx             # Order detail: meta, components, boxes, assembly tasks
│   │       │   ├── OrderMetaSection.tsx      # Order metadata block (status transitions, edit)
│   │       │   ├── OrderComponentsTable.tsx  # Table of order components
│   │       │   ├── OrderBoxesSection.tsx     # Boxes attached to the order
│   │       │   ├── OrderAssemblyTasksSection.tsx # Assembly tasks list for the order
│   │       │   ├── AssemblyTaskAccordionItem.tsx # Single assembly task accordion row
│   │       │   ├── CreateAssemblyTaskDialog.tsx  # Dialog for creating an assembly task
│   │       │   └── EditAssemblyTaskDialog.tsx    # Dialog for editing an assembly task
│   │       ├── OrdersAssemblyPage/
│   │       │   ├── OrdersAssemblyPage.tsx    # Assembly workspace: orders + tasks + fulfillments
│   │       │   ├── AssemblyOrderAccordion.tsx / AssemblyOrderInline.tsx / AssemblyOrderBoxesSection.tsx
│   │       │   ├── AssemblyTaskAccordion.tsx # Task accordion with fulfillments drawer
│   │       │   ├── AddFulfillmentDialog.tsx / MoveTaskComponentDialog.tsx / BatchAssemblyDialog.tsx
│   │       │   └── batchEligibility.ts       # Pure helpers deciding which tasks can be batch-fulfilled
│   │       ├── OrdersFbsPage/
│   │       │   └── OrdersFbsPage.tsx         # Stub — страница в разработке
│   │       ├── OrdersFboPage/
│   │       │   └── OrdersFboPage.tsx         # Stub — страница в разработке
│   │       ├── ReceiptsPage/
│   │       │   ├── ReceiptsPage.tsx          # Paginated, searchable, sortable list of receipts (приёмки)
│   │       │   └── pages/
│   │       │       ├── ReceiptCreatePage/
│   │       │       │   └── ReceiptCreatePage.tsx # RHF form for creating a new receipt (requires edit permission)
│   │       │       └── ReceiptPage/
│   │       │           └── ReceiptPage.tsx    # Receipt detail: metadata edit, status transitions, items + placements
│   │       ├── TransfersPage/
│   │       │   └── TransfersPage.tsx         # Перемещения: pick source/target location, execute transfer
│   │       └── WriteoffsPage/
│   │           ├── WriteoffsPage.tsx         # Paginated, searchable, sortable list of writeoffs (списания)
│   │           └── pages/
│   │               ├── WriteoffCreatePage/
│   │               │   └── WriteoffCreatePage.tsx # RHF form for creating a new writeoff
│   │               └── WriteoffPage/
│   │                   └── WriteoffPage.tsx   # Writeoff detail: metadata edit, status transitions, items
│   └── SettingsPage/
│       ├── SettingsPage.tsx     # Sections declaration only — drives routes + sidebar nav for /settings/*
│       ├── settingsConfig.tsx   # settingsSections (Роли + Сотрудники + Маркетплейсы), hasSettingsAccess, getSettingsFirstPageUrl
│       └── pages/
│           ├── RolesSettingsPage/
│           │   ├── RolesSettingsPage.tsx   # Role-permission matrix table (observer, data fetching, QueryError on initial load, header buttons)
│           │   ├── RolesTable.tsx          # Sticky matrix table with @dnd-kit sortable columns
│           │   ├── RoleColumnHeader.tsx    # Header cell: drag handle + name + edit/delete actions
│           │   ├── RenameRoleDialog.tsx    # MUI Dialog for renaming a role (used via showModal)
│           │   ├── RolesStoreContext.tsx   # React context + provider for RolesStore
│           │   └── rolesStore.ts          # MobX store: EditableRole class + RolesStore
│           └── MarketplacesSettingsPage/
│               ├── MarketplacesSettingsPage.tsx  # Marketplace account list (search, type/active filters, sort)
│               ├── marketplaceUtils.ts           # Enum label maps, getWarehouseStatus, hasCapability([Flags] parser), date/duration/price formatters
│               ├── components/
│               │   ├── MarketplaceStatusChip.tsx # MarketplaceSyncStatus → coloured chip
│               │   ├── CardMappingChip.tsx       # «архивный товар» / «вручную» / «авто (артикул|штрихкод)»
│               │   ├── CardImage.tsx             # Card thumbnail, opens the full image in a new tab on click
│               │   ├── WarehouseStatusChip.tsx   # MarketplaceWarehouseStatus → chip, raw status in a tooltip
│               │   ├── SyncErrorAlert.tsx        # AppFieldError → localized alert + raw marketplace response
│               │   └── TestConnectionButton.tsx  # Credential probe, works before the account exists
│               └── pages/
│                   ├── MarketplaceAccountCreatePage/
│                   │   └── MarketplaceAccountCreatePage.tsx
│                   └── MarketplaceAccountPage/
│                       ├── MarketplaceAccountPage.tsx  # Account shell: header actions, <Tabs>, running-sync polling
│                       ├── AccountOverviewTab.tsx      # Connection, seller details, synced-data counters
│                       ├── AccountWarehousesTab.tsx    # Warehouse table + inline WarehousesSelect mapping
│                       ├── AccountCardsTab.tsx         # Card table, mapping filters, auto-map button
│                       ├── AccountSyncRunsTab.tsx      # Run history with expandable error rows
│                       ├── EditAccountDialog.tsx       # Interval, active flag, key rotation
│                       ├── DeleteAccountDialog.tsx     # ConfirmDialog wrapper
│                       └── CardMappingDialog.tsx       # Card preview + CatalogItemsSelect mapping editor
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
    ├── errorUtils.ts            # API error shape helpers: extractErrorMessage (root error, then any field error), firstFieldError, isNotFoundError, isAppProblemDetails; errorCodeMessages map + errorCodeArgMessages (detailed variants used only when args cover every {placeholder})
    ├── parseJwt.ts              # Decode JWT payload without verification
    ├── permissionLabels.ts      # Human-readable labels for permission enum values
    ├── printUtils.ts            # openPrintPage(items) helper — builds URL and opens /print in a new tab
    ├── barcodeUtils.ts          # Entity-tagged barcode payloads: formatEntityBarcode(entity, id) / parseEntityBarcode(raw)
    ├── clipboardUtils.ts        # copyToClipboard(text) — navigator.clipboard with execCommand fallback
    ├── appEntityUtils.tsx       # entitiesTypes registry (user/roles/warehouse/receipt/marketplaceAccount/marketplaceCard → icon, typeName, linkTemplate); resolveEntity(entity) → {link, typeName, icon, ...entity}
    ├── fetchWithTimeout.ts      # fetchWithTimeout(url, options, timeoutMs) — fetch wrapper with AbortController timeout
    ├── interpolateArgs.ts       # interpolateArgs(template, args) — replaces {key} placeholders in a string
    └── useInstallPrompt.ts      # Hook: beforeinstallprompt event
```

## Routing

`BrowserRouter` in `main.tsx`. Pages are lazy-loaded via `React.lazy` + `Suspense`. Access control is handled by `ProtectedRoute` / `ProtectedRoutes` components; unauthenticated users are redirected to `/login`.

```
/login                    → LoginPage                (public)
/scanner                  → ScannerPage              (authenticated, no layout wrapper)
/print                    → PrintPage                (authenticated, no layout wrapper)
/                         → MainLayout > HomePage        (authenticated)
/profile                  → MainLayout > MyProfilePage   (authenticated)
/catalog                  → MainLayout > CatalogPage     (catalog.view)

/storage/*                → MainLayout > StoragePage     (SidebarPage)
/storage                  →   redirect to /storage/warehouses
/storage/warehouses                                                              →   WarehousesPage                  (authenticated)
/storage/warehouses/new                                                          →   WarehouseNewPage                (warehouses.edit)
/storage/warehouses/new                                                          →   WarehouseNewPage                (warehouses.edit)
/storage/warehouses/:id                                                          →   WarehouseViewPage               (authenticated)
/storage/warehouses/:id/edit                                                     →   WarehouseEditPage               (warehouses.edit | warehouses.edit_assigned)
/storage/warehouses/:id/inventory                                                →   WarehouseInventoryPage          (authenticated)
/storage/warehouses/:warehouseId/storage-places/:storagePlaceId/inventory        →   StoragePlaceInventoryPage       (authenticated)
/storage/warehouses/:warehouseId/storage-places/:storagePlaceId/nodes/:nodeId/inventory → NodeInventoryPage         (authenticated)
/storage/inventory        →   InventoryPage                   (authenticated)

/operations/*             → MainLayout > OperationsPage  (SidebarPage)
/operations               →   redirect to first accessible section (/operations/orders/assembly)
/operations/orders        →   redirect to /operations/orders/assembly
/operations/orders/assembly   →   OrdersAssemblyPage
/operations/orders/direct     →   OrdersDirectPage
/operations/orders/direct/new →   OrderDirectCreatePage
/operations/orders/fbs    →   OrdersFbsPage                   (stub)
/operations/orders/fbo    →   OrdersFboPage                   (stub)
/operations/orders/:id    →   OrderPage
/operations/receipts      →   ReceiptsPage                    (receipts.view | receipts.view_assigned | receipts.process_assigned)
/operations/receipts/new  →   ReceiptCreatePage               (receipts.edit | receipts.edit_assigned)
/operations/receipts/:id  →   ReceiptPage                     (authenticated)
/operations/transfers     →   TransfersPage
/operations/writeoffs     →   WriteoffsPage
/operations/writeoffs/new →   WriteoffCreatePage
/operations/writeoffs/:id →   WriteoffPage

/settings/*               → MainLayout > SettingsPage    (SidebarPage)
/settings                 →   redirect to first accessible section
/settings/roles           →   RolesSettingsPage               (roles.view)
/settings/employees                                            →   UsersPage                       (users.view)
/settings/employees/new                                        →   UserCreatePage                  (users.create)
/settings/employees/:id                                        →   UserViewPage                    (users.view)
/settings/employees/:id/edit                                   →   UserEditPage                    (users.edit_profile)
/settings/integrations                                         →   MarketplacesSettingsPage        (integrations.view)
/settings/integrations/new                                     →   MarketplaceAccountCreatePage    (integrations.view)
/settings/integrations/:id                                     →   MarketplaceAccountPage          (integrations.view)
```

> **Convention:** subroutes carry no `requiredPermission` of their own — `SidebarPage` only gates the section route. Sub-pages that need a stronger right (`integrations.edit`, `integrations.map`) hide their actions with `useHasPermission`, and the server enforces it regardless.

## Pages

### `HomePage`
Landing page. Fetches `AppEntity[]` from `/api/home` and renders navigation cards for each entity (icon, title, type label, link). Cards are resolved via `resolveEntity` from `appEntityUtils`. Also shows PWA offline-ready indicator and the `InstallPrompt` when the app is installable.

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
- **Manual fields** — label width/height (mm), columns, gap (min 0 mm), page padding (min 0 mm), label padding (min 0 mm). All fields use `NumField` — the input can be cleared while focused and only snaps to the minimum value on blur.
- **Save preset** — saves current settings as a named custom preset; custom presets can be deleted.

`@page { margin: 0 }` is injected globally via `GlobalStyles` so that browser default print margins are removed and `pagePaddingMm` (applied as CSS `padding` on the page container) is the sole source of page margins. `labelPaddingMm` adds inner padding to each `BarcodeLabel` box; it applies in both screen preview and print. For 1D barcodes the bwip-js bar height is calculated from the unpadded label height so that the rendered canvas resolution stays fixed as padding changes — only the CSS `maxHeight` constraint shrinks.

To open the print page programmatically use `openPrintPage(items)` from `@/utils/printUtils`.

Example URL: `/print?item=DataMatrix:ABC123|Товар А&item=EAN13:5901234123457&item=Code128:HELLO&item=QR:test`

#### Barcode payload format

Barcodes printed for app entities carry an entity tag so a scanner can tell what was scanned. Built with `formatEntityBarcode(entity, id)` from `@/utils/barcodeUtils` and read back with `parseEntityBarcode(raw)` → `{entity, id} | null`.

Format: `pw:<entityCode>:<guid>`

| Entity | Code | Example |
| --- | --- | --- |
| `storagePlaceNode` | `spn` | `pw:spn:3f2a1b6c-…` |
| `catalogItem` | `ci` | `pw:ci:9d4e…` (printed from `CatalogItemDrawer`) |

Parsing is strict: an untagged bare GUID is **not** accepted. Labels printed before this format was introduced must be reprinted.

### `UsersPage`
Server-side paginated, searchable, and filterable table of users. Requires `users.view` permission. State is stored in URL params (`?search=`, `?role=`, `?page=`, `?pageSize=`) using `useDebouncedSyncedWithQueryState` + `useSyncedWithQueryState` + `usePaginatedParams`. The search field updates instantly without lag; the URL and API call update after a 300 ms debounce. A roles filter (`RolesSelect`) is shown in a `FiltersBar` below the header. Rows are clickable and navigate to `UserViewPage`.

### `WarehousesPage`
Server-side paginated and searchable table of warehouses. State is stored in URL params (`?search=`, `?page=`, `?pageSize=`) via `useDebouncedSyncedWithQueryState` + `usePaginatedParams`. Uses `SearchInput`, `DataTableContainer`, `TableRowLoader`, and `TableRowEmpty`. Rows navigate to `WarehouseViewPage`.

### `UserViewPage`
Read-only detail view for a single user. Displays username, email, first/last name, assigned roles (chips), and direct permissions (chips). Action buttons: **Редактировать** → `UserEditPage`, **Сменить пароль** → opens `ChangePasswordDialog`, **Удалить** → opens `DeleteUserDialog`. Requires `users.view`.

On query error: renders `<NotFound />` for 404, `<QueryError />` for everything else. The `usersGetById` query sets `suppressGlobalError: true` and `suppressGlobalNotFound: true` so the global modal is never shown alongside these inline states. Error screens are only shown on the initial load — background refetch errors (`isRefetchError`) are ignored so a transient network blip doesn't replace visible data with an error screen.

### `UserEditPage`
RHF form for editing a user's profile (email, first/last name) and, if the current user has `users.manage_roles_and_permissions`, also roles (typeahead via `rolesSearch` API) and direct permissions (multi-select from `permissionsGetAll`). Pre-populated from `usersGetById`; refetches on window focus without losing unsaved edits (`keepDirtyValues: true`). Requires `users.edit_profile`.

Same error handling as `UserViewPage`: `<NotFound />` on 404, `<QueryError />` otherwise; refetch errors are suppressed.

### `MyProfilePage`
Read-only profile page for the currently authenticated user. Displays username, email, first/last name, assigned roles (chips), and effective permissions (chips with tooltip showing the raw permission string). Action button: **Сменить пароль** → opens `ChangePasswordDialog`. Accessible to all authenticated users at `/profile` via the user avatar menu in `MainAppBar`.

`ChangePasswordDialog` is a modal form with two fields: current password and new password. Submits to `authChangeOwnPassword`. On success it closes and resets the form; on error, API errors are mapped back to form fields via `useRhfApiErrors`. Backdrop-click dismiss is disabled while the mutation is pending.

Error handling mirrors `UserViewPage`: `<NotFound />` on 404, `<QueryError />` otherwise; refetch errors are suppressed. Both `suppressGlobalError` and `suppressGlobalNotFound` are set on the `/me` query to prevent global modals.

### `UserCreatePage`
RHF form for creating a new user. Fields: username (required), password (required, with show/hide toggle), email, first name, last name. On success navigates to the new user's `UserViewPage`. Requires `users.create`.

### `ReceiptsPage` (Приёмки)
Server-side paginated, searchable, sortable list of receipts. Access requires any of `receipts.view`, `receipts.view_assigned`, or `receipts.process_assigned`. State in URL params (`?search=`, `?page=`, `?pageSize=`, `?status=`, `?sortBy=`, `?sortOrder=`). Columns: **№**, **Название**, **Причина**, **Статус** (`ReceiptStatusChip`), **Склад**, **Дата доставки**, **Факт/план** (received/planned counts). Rows navigate to `ReceiptPage`. A **Создать** button is shown if the user has an edit permission.

### `ReceiptCreatePage`
RHF form for creating a new receipt. Fields: name (optional), reason (select: `newGoods`/`return`/`other`), warehouse (required, via `WarehousesSelect`), planned delivery date (optional), notes (optional). On success navigates to the created receipt's `ReceiptPage`. Requires `receipts.edit` or `receipts.edit_assigned`.

### `ReceiptPage`
Detail page for a single receipt (`/operations/receipts/:id`). Shows receipt metadata with inline edit form (PATCH) and status action buttons. Body section is `ReceiptItemsSection` — one collapsible card per item showing planned/received counts and a placements table.

**Status transitions rendered as action buttons based on `receipt.status`:**
- `draft` → **Запланировать** + **Редактировать состав** (opens `ReceiptItemsEditorDrawer`) + **Удалить**
- `planned` → **Начать приёмку** + **Редактировать состав** (opens `ReceiptItemsEditorDrawer`) + **Вернуть** + **Отменить**
- `processing` → **Завершить** + **Вернуть** + **Отменить** (Вернуть/Отменить disabled if any placements exist)
- `finished` → **Вернуть в обработку**
- `canceled` → read-only, no actions

**`ReceiptItemsSection`:** per-item collapsible panel. Shows `receivedCount` field (editable in Processing status via PATCH `.../received-count`). Placements table lists node path + count/SKU per placement with a delete button (Processing only). **Разместить** button opens `AddPlacementDialog`.

### `CatalogPage`
Server-side paginated, searchable list of catalog items. Requires `catalog.view` or `receipts.process_assigned`. State in URL params (`?search=`, `?page=`, `?pageSize=`, `?types=` comma-separated `CatalogItemType`). Clicking a row opens `CatalogItemDrawer` (right-side MUI Drawer); the selected item ID is stored in `?item=` query param via `useDrawerSearchParamsState`. Columns: **Тип** (`CatalogItemTypeChip`), **Название** (fullName + archive icon if isArchived), **Артикул**, **Штрихкод**.

**Type filter** — multiselect `Select` with `Checkbox` per item; default is all types (URL param omitted when default is active). `renderValue` shows "Все" for all 5, `"N типов"` for 2+, or the single type label. `DEFAULT_ITEM_TYPES` constant holds the default selection.

### `InventoryPage`
Global stock overview at `/inventory`. Uses `ItemsBasePage` without any ID props, so the warehouse filter Select is shown. Requires `warehouses.view` or `warehouses.view_assigned`.

### `WarehouseInventoryPage`
Stock overview scoped to a single warehouse at `/warehouses/:id/inventory`. Fetches the warehouse by ID for the breadcrumb name, passes `warehouseId` to `ItemsBasePage` (no warehouse filter shown). Requires `warehouses.view` or `warehouses.view_assigned`.

### `StoragePlaceInventoryPage`
Stock overview scoped to a single storage place at `/warehouses/:warehouseId/storage-places/:storagePlaceId/inventory`. Fetches the warehouse to resolve the storage place name for breadcrumbs. Passes `warehouseId` + `storagePlaceId` to `ItemsBasePage`. Requires `warehouses.view` or `warehouses.view_assigned`.

### `NodeInventoryPage`
Stock overview scoped to a single storage place node at `/warehouses/:warehouseId/storage-places/:storagePlaceId/nodes/:nodeId/inventory`. Fetches the warehouse (for storage place name) and `storagePlacesGetNodes` (for node name). Passes all three IDs to `ItemsBasePage`. Requires `warehouses.view` or `warehouses.view_assigned`.

### `ItemsBasePage`
Reusable inventory table component (`components/inventory/ItemsBasePage.tsx`). Accepts `warehouseId?`, `storagePlaceId?`, `nodeId?` as scope constraints.

- **Warehouse filter Select** — shown only when `warehouseId` prop is not provided; fetches all warehouses (`pageSize: 200`); URL-synced via `?warehouse=`
- **Type filter** — `catalogItemType` Select using `CATALOG_ITEM_TYPE_CONFIG`; URL-synced via `?type=`
- **Archive filter** — ToggleButtonGroup (Активные / Архивные) for `isArchived`; URL-synced via `?archived=`
- **Search** — debounced via `useDebouncedSyncedWithQueryState("search")`
- **Table columns:** Тип (`CatalogItemTypeChip`), Название (`CatalogItemLink` with fullName + archive icon), Артикул, Количество
- **Catalog item link** — the name cell is a `CatalogItemLink` calling `openCatalogDrawer(row.catalogItemId)` → opens `CatalogItemDrawer` (it `stopPropagation()`s, so the row's own click handler doesn't fire)
- **Row click** — type `unit` → opens `UnitItemsDrawer`; other types → no action
- All drawer state via `useDrawerSearchParamsState`: `"catalogItem"`, `"unitCatalogItem"`

### `UnitItemsDrawer`
Bottom MUI Drawer (`components/inventory/UnitItemsDrawer.tsx`). Opens when `?unitCatalogItem=` param is set (managed by `ItemsBasePage`). Shows a paginated, searchable list of individual `UnitInventoryItem` instances for the selected catalog item filtered to the same scope (warehouseId/storagePlaceId/nodeId). Search by SKU. Columns: Артикул, Склад, Место хранения, Ячейка.

### `WarehouseViewPage`
Warehouse detail page with a pan/zoom Konva canvas showing storage place rectangles. The canvas is rendered by `WarehouseCanvas` from `src/features/warehouse/`. Clicking a storage place opens `StoragePlaceDrawer` (1000 px wide right drawer) with a `StoragePlaceNodeTree` from `src/features/warehouse/` on the left and `NodeDetails` on the right when a node is selected.

**"Остатки" button** is a `Link` to `/storage/warehouses/:id/inventory`.

**"Этикетки" button** fetches `GET /api/warehouses/{id}/print`, then calls `openPrintPage` with all nodes as `DataMatrix` labels (value = `formatEntityBarcode("storagePlaceNode", node.id)` → `pw:spn:<guid>`, label = full path joined by ` / `). A `CircularProgress` spinner replaces the print icon while the request is in-flight. `StoragePlaceDrawer` has its own "Этикетки" button printing only that place's nodes in the same format.

**"Редактировать" button** navigates to `WarehouseEditPage` (`/storage/warehouses/:id/edit`).

**"Редактировать ячейки" button** (inside `StoragePlaceDrawer`) toggles tree edit mode. In edit mode the tree switches from `SimpleTreeView` to `SortableNodeTree`; `NodeDetails` panel is hidden. Each node row shows add-child, rename, and delete icon buttons plus a drag handle. A root-level "Добавить ячейку" button appears above the tree. Add/rename open a `Dialog` with a name input; delete opens a `ConfirmDialog`. Drag-and-drop reorder calls `PUT .../nodes/reorder`. All operations update the nodes query cache in-place from the returned flat list.

### `WarehouseEditPage`
Full warehouse editor at `/storage/warehouses/:id/edit`. Combines a Konva canvas (the local `WarehouseCanvas` component, distinct from the read-only variant in `src/features/warehouse/`) with a metadata form (`WarehouseMetaForm`). State is managed by `WarehouseEditStore` (MobX) provided via `WarehouseEditStoreProvider`. The toolbar (`WarehouseEditToolbar`) exposes add/save/delete actions. `ObjectPropertiesDialog` opens when a storage place is selected on the canvas, allowing name/position/size edits. `DeleteWarehouseDialog` is shown on the delete action.

### `SortableNodeTree`
Drag-and-drop sortable tree component used exclusively in `StoragePlaceDrawer` edit mode. Built on `@dnd-kit/core` + `@dnd-kit/sortable`. Renders a recursive tree from a flat `StoragePlaceNodeDto[]` (sorted by `order` then `name`); each sibling group is its own `SortableContext`. Only same-level reordering is allowed — dragging across parent boundaries is a no-op. Fires `onReorder(NodeOrderItem[])` (zero-based index positions for affected siblings) on drop. Accepts an `isDisabled` flag that disables all actions and the drag handle cursor while API mutations are in-flight.

### `WarehouseItemsPage`
Paginated, searchable table of all item groups aggregated across the entire warehouse. Requires `warehouses.view`. State in URL params (`?search=`, `?page=`, `?pageSize=`). Also fetches the warehouse by ID (cached from `WarehouseViewPage`) for the breadcrumb name.

Columns: **Название**, **Артикул**, **Характеристика**, **Штрихкод** (characteristic barcode falling back to catalog item barcode), **Количество** (shown as a `Chip`), and an action column with an `OpenInNew` icon button linking to `/catalog?item={catalogItemId}` — opens the catalog drawer directly on the item.

### `MarketplacesSettingsPage` (Маркетплейсы)
Server-side paginated, searchable, sortable list of marketplace accounts at `/settings/integrations`. Requires `integrations.view`. State in URL params (`?search=`, `?type=`, `?active=`, `?sortBy=`, `?sortOrder=`, `?page=`, `?pageSize=`). Columns: **Магазин**, **Площадка**, **Статус** (`MarketplaceStatusChip`), **Синхронизация** (last sync timestamp), **Складов**, **Карточек**, **Не сопоставлено** (warning chip when > 0), **Активен**. Sortable columns are **Магазин** and **Синхронизация** only — the backend `MarketplaceAccountSortBy` accepts nothing else. The **Подключить магазин** button requires `integrations.edit`. Rows navigate to `MarketplaceAccountPage`.

### `MarketplaceAccountCreatePage`
RHF form at `/settings/integrations/new`: площадка (Ozon only for now), **Client-Id**, **Api-Key** (`type="password"`), **Интервал синхронизации** (1…10080, default 30), and a **Синхронизировать по расписанию** switch.

**There is no name field** — `MarketplaceAccount.Name` comes from the marketplace's own seller info and is overwritten by every sync; until the first run the server stores a `Ozon ••••1234` placeholder. An inline `Alert` says so, otherwise the missing field reads as a bug.

`TestConnectionButton` probes the credentials before the record exists (the route id is ignored when the body carries an `apiKey`). On submit the server enqueues the first sync itself when the account is active, so the client does not call `/sync` after creating.

> **Note:** `input[type=number]` hands RHF a string. Numeric fields are coerced with `Number(...)` at submit — the API rejects `"30"` for an `int`.

### `MarketplaceAccountPage`
Account shell at `/settings/integrations/:id`. Same error handling as `UserViewPage` (`<NotFound />` on 404, `<QueryError />` otherwise, refetch errors suppressed). Header shows the sync status chip plus **Синхронизировать** (a `Menu` picking scope: Всё / Склады / Карточки, requires `integrations.map`), **Изменить** and **Удалить** (both `integrations.edit`).

Four tabs — **Обзор**, **Склады**, **Карточки**, **История** — live on a single route with the active tab in `?tab=` (see the tabbed-page convention under URL State Hooks). The Склады and Карточки tabs are hidden unless the account's `capabilities` declare them; a `?tab=` pointing at a hidden tab falls back to Обзор. Only the active tab is mounted, so background tabs hold no queries.

While `lastSyncStatus === "running"`, the account query and the run-history query poll at 3 s and stop on their own afterwards. This is the single place to swap for a live subscription once a realtime client exists (see [realtime-specification.md](realtime-specification.md)).

Tabs:
- **Обзор** — connection details, seller details (юрлицо, ИНН, ОГРН, форма собственности), synced-data counters, `SyncErrorAlert` for `lastSyncError`, and a hard error alert when `credentialsUnreadable` (the Data Protection key ring was lost and the key must be re-entered).
- **Склады** — sortable table with an inline `WarehousesSelect` per row saving on change; unmapped rows carry a warning icon, the Seller API status renders as a `WarehouseStatusChip`. `?archived=` toggles archived warehouses.
- **Карточки** — image, название, артикул, цена, обновлена, SKU, позиция каталога, `CardMappingChip`. Filters in URL (`?search=`, `?mappingState=`, `?archived=`); **`mappingState` defaults to `unmapped`** because that is the working list. **Сопоставить автоматически** runs account-wide auto-mapping and reports «Сопоставлено N, требует ручного разбора M». Clicking a row opens `CardMappingDialog` (requires `integrations.map`); a mapped row's catalog cell is a `CatalogItemLink` opening `CatalogItemDrawer` (the tab is wrapped in `CatalogItemDrawerHost`, drawer state in `?catalogItem=`). The thumbnail is a `CardImage` — opens the full-size marketplace image in a new tab.
- **История** — run history; rows carrying an error expand into a `SyncErrorAlert`. `MarketplaceSyncStatus.canceled` is reserved and never produced by the current backend.

### `NodeDetails`
Panel rendered inside `StoragePlaceDialog` for the selected node. Fetches `StoragePlaceNodeDetailsDto` via `GET /api/storagePlaces/{id}/nodes/{nodeId}`. Displays and edits the node's item groups:
- View mode: table of catalog item name + characteristic + count + barcode
- Edit mode: editable rows via `EditItemRow` — each row has a catalog `Autocomplete` (debounced search, 20 results), a characteristic `Select` (populated from the selected catalog item), and a count field. Rows can be added/removed. On save calls `PUT .../items` to atomically sync the groups.

## Features

### `src/features/catalog/`

Shared catalog domain constants consumed by components and pages.

#### `catalogItemTypes.ts`

```ts
CATALOG_ITEM_TYPE_CONFIG: Record<CatalogItemType, {label: string; color: ChipProps["color"]}>
CATALOG_ITEM_TYPES: CatalogItemType[]   // all types in declaration order
```

`CATALOG_ITEM_TYPE_CONFIG` maps every `CatalogItemType` to a human-readable Russian label and a MUI chip color. Used by `CatalogItemTypeChip`, `CatalogPage` (filter Select), and `CreateCatalogItemDialog` (creation Select). `CATALOG_ITEM_TYPES` is derived from the config keys and guarantees the two stay in sync.

When adding a new type to the backend OpenAPI schema, update only this file — all consumers update automatically.

### `src/features/warehouse/`

Reusable warehouse visualization components shared between `WarehouseViewPage` and other pages that need a read-only canvas or node tree.

#### `WarehouseCanvas`

Generic pan/zoom Konva canvas for rendering a warehouse floor plan. Manages its own `containerRef`, `stageRef`, `stageScale`, and auto-fit effect. Root element is `<Box sx={{position: "relative", width: "100%", height: "100%"}}>` — the caller wraps it in a `<Paper>` with a fixed height.

```tsx
interface WarehouseStoragePlaceRenderItem {
  id: string; x: number; y: number; width: number; height: number;
  rotation: number; name: string;
  fill: string;    // caller decides color per storage place
  label?: string;  // optional override for the text label, defaults to name
}
interface WarehouseCanvasProps {
  width: number; height: number;
  layoutObjects: WarehouseLayoutElementDto[];
  storagePlaces: WarehouseStoragePlaceRenderItem[];
  onStoragePlaceClick?: (id: string) => void;
}
```

Usage in **WarehouseViewPage** (static display):
```tsx
storagePlaces={warehouse.storagePlaces.map(p => ({
  ...p,
  fill: green[300],
  label: p.totalItemsCount > 0 ? `${p.name}\n${p.totalItemsCount} тов.` : p.name,
}))}
```

Usage in **WarehouseSchemaDrawer** (processing, coloring by `hasOrderItems`):
```tsx
storagePlaces={order.warehouse.storagePlaces.map(p => ({
  ...p,
  fill: p.hasOrderItems ? green[500] : green[200],
}))}
```

#### `StoragePlaceNodeTree`

Read-only `SimpleTreeView` of storage place nodes. Handles `buildTree` internally from a flat `StoragePlaceNodeTreeNode[]`. Optionally shows an 8 px colored dot per node when `hasOrderItems` is present (`green.main` if true, `grey.400` if false). Shows `<CircularProgress>` while `isLoading` and "Ячейки не найдены" when the list is empty.

```tsx
interface StoragePlaceNodeTreeNode {
  id: string; name: string; parentNodeId?: string | null;
  order: number; totalItemsCount: number;
  hasOrderItems?: boolean;
}
interface StoragePlaceNodeTreeProps {
  nodes: StoragePlaceNodeTreeNode[];
  selectedNodeId?: string | null;
  onSelect?: (id: string) => void;
  isLoading?: boolean;
}
```

## Key Components

### `CatalogItemDrawer`

Reusable right-side MUI Drawer (`components/catalog/CatalogItemDrawer.tsx`) for viewing and editing any catalog item. Mount it on any page using `useDrawerSearchParamsState`.

```tsx
const [selectedItemId, openDrawer, closeDrawer] = useDrawerSearchParamsState("item");
<CatalogItemDrawer itemId={selectedItemId} onClose={closeDrawer} onOpenItem={openDrawer} />
```

Props: `{ itemId: string | null; onClose: () => void; onOpenItem?: (id: string) => void }`.
`onOpenItem` is used for in-drawer navigation (e.g. clicking "open parent group").

**View mode** shows: type chip + name, article, barcode, description (with "effective" indicator if inherited from group), notes, tags (chips), archive badge, group membership with navigate-to-parent button. Type-specific sections: ProductGroup → children table; Bundle → components table; Variation → members list; Standard/Unit → variations list.

**Edit mode** (react-hook-form): base fields (name, article, barcode, description, notes, isArchived switch) + tags Autocomplete (fetches via `GET /api/catalog/tags`). Type-specific RHF sections:
- Standard/Unit → Variations multi-select (`CatalogItemsSelect`)
- Variation → Members multi-select (`CatalogItemsSelect`, types: standard/unit/bundle — not variation)
- Bundle → Components `useFieldArray` (`CatalogItemsSelect` + quantity per row; types: standard/unit/productGroup/variation — not bundle)
- ProductGroup → Children `useFieldArray` (inline form per child: type, name, article, barcode, description, notes, tags, variations)

Edit is hidden for items with `groupId` (managed by parent group — shown as an info alert).

**Header actions** (left of the close button, available in both view and edit mode, and for items managed by a group):
- **Скопировать GUID** — copies the raw item id via `copyToClipboard` (`utils/clipboardUtils.ts`: `navigator.clipboard` with a hidden-textarea + `execCommand` fallback for insecure origins / the Capacitor shell), then reports the result with a notistack snackbar.
- **Печать этикетки** — opens `PrintLabelDialog`: choose the payload and the number of copies (1–200), then `openPrintPage` with the item repeated N times.
  - *Внутренний код* — `DataMatrix` with `pw:ci:<guid>` (see [barcode payload format](#barcode-payload-format))
  - *Штрихкод товара* — the item's own `barcode` field; disabled when empty. Encoded as `EAN13` for 12–13 digit values, otherwise `Code128`, since bwip-js rejects non-numeric EAN13 payloads.
  - Label caption for both: `fullName · article`.

**Convention:** wherever a catalog item name is rendered — table cell, card headline, drawer row — it should be a `CatalogItemLink` that opens this drawer. When building a new page or drawer that shows catalog items, add the open-drawer affordance as part of the initial implementation, not as a follow-up. State always goes through `useDrawerSearchParamsState`, so «назад» closes the drawer; only the param name differs:

- **Page, single link owner** → `useDrawerSearchParamsState("catalogItem")` plus a local `<CatalogItemDrawer>`. The opened item lands in the URL and stays deep-linkable (`ItemsBasePage`, `ReceiptItemsSection`, `WriteoffItemsSection`). `CatalogPage` predates the convention and uses `"item"` for its own row drawer — its param is page-local and must not be confused with the shared `"catalogItem"` name.
- **Page whose links live in components rendered in a loop** → wrap the page in [`CatalogItemDrawerHost`](#catalogitemdrawerhost) and call `useOpenCatalogItem()` in the leaf. A per-component drawer would open N copies at once, since the state is shared via the URL.
- **Nested inside a drawer/dialog whose own open state is *not* in the URL** → use a distinct param name and register it in `EPHEMERAL_PARAMS` (`utils/ephemeralSearchParams.ts`), e.g. `"fulfillmentCatalogItem"` in `FulfillmentsDrawer`. Otherwise a reload would reopen the nested drawer on top of a closed parent. Never reuse `"catalogItem"` for this — that name must survive a cold load.

### `CatalogItemDrawerHost`

`components/catalog/CatalogItemDrawerHost.tsx` — renders exactly one `CatalogItemDrawer` for a whole page (param `"catalogItem"`) and publishes its open function through context. The context and the `useOpenCatalogItem()` hook live in a separate `CatalogItemDrawerContext.ts` so the host file only exports a component (react-refresh rule).

```tsx
// page
<CatalogItemDrawerHost>
  <Stack spacing={2}>…</Stack>
</CatalogItemDrawerHost>

// any descendant, however deep
const openCatalogItem = useOpenCatalogItem();
<CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItem}>…</CatalogItemLink>
```

`useOpenCatalogItem()` throws outside the host — a missing wrapper fails loudly instead of silently doing nothing. Used by `OrderPage` (→ `OrderComponentsTable`, `AssemblyTaskAccordionItem`) and `OrdersAssemblyPage` (→ `AssemblyTaskAccordion`).

### `stripEphemeralSearchParams()`

Called in `main.tsx` before `mountApp()` (same slot as the existing `clear_server` cleanup). Deletes every param listed in `EPHEMERAL_PARAMS` from the current URL via `history.replaceState`, so they never survive a cold entry (F5, bookmark, pasted link) but are untouched by in-app SPA navigation. Running before React mounts means the drawer doesn't flash open for a frame, and the history entry is replaced rather than pushed.

### `CatalogItemLink`

Wrapper (`components/catalog/CatalogItemLink.tsx`) giving any catalog item label the standard clickable look: pointer cursor, `fit-content` width, and an `OpenInNewIcon` that only appears on hover. Click calls `onOpen(catalogItemId)` and `stopPropagation()`s, so it stays safe inside clickable table rows.

Props: `{ catalogItemId: string; onOpen: (id: string) => void; spacing?: number; sx?: SxProps<Theme>; children: ReactNode }`.

Content is passed as children because the composition differs per call site (chip before or after the name, extra badges), and the wrapper stays flag-free.

```tsx
<CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItemDrawer} spacing={0.5}>
  <Typography variant="body2">{c.catalogItemName}</Typography>
  <CatalogItemTypeChip type={c.catalogItemType} />
</CatalogItemLink>
```

Used by `ItemsBasePage` (name + archive icon), `CatalogPage` (name + archive icon), `ReceiptItemsSection` (chip + name), `WriteoffItemsSection` (chip + name; falls back to plain text when `catalogItemId` is null), `FulfillmentsDrawer` (card headline, resolved variant row, bundle component rows), `OrderComponentsTable` / `AssemblyTaskAccordionItem` / `AssemblyTaskAccordion` (component name, via `useOpenCatalogItem()`).

### `CatalogItemTypeChip`

Small `Chip` (`components/catalog/CatalogItemTypeChip.tsx`) that maps `CatalogItemType` to a label and color. Uses `CATALOG_ITEM_TYPE_CONFIG` from `src/features/catalog/`.

| Type | Label | Color |
|------|-------|-------|
| standard | Товар | default |
| unit | Единица | info |
| productGroup | Группа | secondary |
| variation | Вариация | warning |
| bundle | Комплект | success |

Props: `type: CatalogItemType` + all `ChipProps` except `label`/`color`.

```tsx
<CatalogItemTypeChip type={item.type} />
<CatalogItemTypeChip type="bundle" size="medium" />
```

### `CatalogItemsSelect`

Autocomplete for catalog items (`components/CatalogItemsSelect.tsx`). Supports single and multi selection.

**Single mode** — value is an entity `id` (`string | null`); `onChange` receives `(id: string | null)`; optional `onDtoChange` callback fires with the resolved `CatalogItemSummaryDto` (useful for reading `fullName`, `type`, etc. without a separate query).

**Multi mode** — value and onChange work with `CatalogItemSummaryDto[]`.

Both modes debounce the search input (300 ms), fetch via `catalogGetAllOptions`, and cache selected items so they survive search changes.

Optional `types?: CatalogItemType[]` prop for client-side filtering by item type.

```tsx
// Single
<CatalogItemsSelect
  value={selectedId}
  onChange={(id) => setSelectedId(id)}
  onDtoChange={(dto) => console.log(dto?.fullName)}
  types={["standard", "unit"]}
  label="Товар"
/>

// Multi
<CatalogItemsSelect
  multiple
  value={selectedItems}
  onChange={(items) => setSelectedItems(items)}
  types={["variation"]}
/>
```

### `ScannerBlock`
Orchestrates the full camera scan loop:
1. Acquires a camera stream via `useCameraStream` (persists preferred device ID in `localStorage`)
2. Renders `ScanArea` (video element), `ScanFrameOverlay` (viewfinder rect), `ZoomControls`, `CameraSelectDialog`
3. On each frame: captures to canvas → runs Otsu binarization + optional inversion → decodes with zxing-wasm (primary) or native `BarcodeDetector` (fallback)
4. Emits decoded barcodes via `onScan` callback

Scan interval is configurable (4–25 FPS equivalent).

### `StorageNodePickerContent`
Shared body of the storage-node picker dialogs (`components/shared/StorageNodePickerContent.tsx`). Takes `warehouseId`, `open`, and `onSelect(node: SelectedNode)`; four tabs:

| Tab | Behaviour |
|---|---|
| Карта | `WarehouseCanvas`; clicking a storage place selects it and jumps to the Схема tab |
| Схема | Storage place `Select` + `StoragePlaceNodeTree` scoped to the selected place (parent nodes are not selectable) |
| Камера | `ScannerBlock` |
| Сканер | Hint text; the hardware scanner is bound globally via `useHardwareScanner` while `open` |

**Scanning is warehouse-wide, not limited to the storage place chosen in the Схема tab.** The picker loads `GET /api/warehouses/{id}/print` (`warehousesGetByIdForPrintOptions`) while open, which returns every node of the warehouse as `{id, name: string[]}` (full path, root-first). A scan is resolved with `parseEntityBarcode`; only `storagePlaceNode` payloads are accepted, anything else fails with an inline `Alert`. On a hit the picker calls `onSelect` with the node's full path and also switches the Схема `Select` to the owning storage place, matched by the path root (`name[0]`) — the print DTO carries no storage place id, so places sharing a name inside one warehouse can switch the dropdown to the wrong one (cosmetic only; the selected node is still correct).

Each failed scan bumps a `scanKey` that remounts `ScannerBlock` so the camera re-arms.

A scan that arrives before the node list has loaded does **not** report "не найдено" — it shows «Ячейки склада ещё загружаются, повторите сканирование» instead. If the list request failed, the scan triggers a `refetch` and asks the user to scan again.

### `MainAppBar`
Top navigation bar. Logo/title + mobile hamburger menu with permission-filtered links. Nav entries: **Склад** (`/storage/*`, requires warehouses.view or warehouses.view_assigned), **Каталог** (`/catalog`, requires catalog.view), **Операции** (`/operations/*`, always visible), **Настройки** (`/settings/*`, requires at least one settings permission).

Each entry in the `pages` array supports `requiredPermission` (must match a user permission), `showIf` (arbitrary boolean predicate over permissions), and `url` — which can be a plain string **or** a `(permissions: PermissionName[]) => string` factory (used by sidebar modules to link directly to the first accessible section via `getStorageFirstPageUrl`, `getOperationsFirstPageUrl`, `getSettingsFirstPageUrl`). If the `/me` profile query fails, an inline red error message is shown in the user avatar dropdown via `profileIsLoadError`/`profileLoadError` from `AuthContext`.

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

### `MarketplaceStatusChip`
Coloured chip for `MarketplaceSyncStatus` (`running` → info, `success` → success, `failed` → error, `canceled` → default). Renders «Не синхронизировался» when the status is `null` — a freshly created account has none.

Props: `status: MarketplaceSyncStatus | null | undefined`.

### `CardMappingChip`
Mapping badge for a marketplace card. `isMappedToArchivedItem` wins over the source and renders a warning chip «Привязана к архивному товару»; otherwise the source is shown as «вручную» / «авто (артикул)» / «авто (штрихкод)». Renders nothing for an unmapped card.

Props: `card: MarketplaceCardDto`.

### `CardImage`
Marketplace card thumbnail. Without a `src` it is a plain letter `Avatar`; with one it becomes an `<a target="_blank">` to the full-size image, marked by a tooltip and a hover overlay with an open-in-new icon, and stops click propagation so it does not also trigger the surrounding row. Used in the cards table (40 px) and in `CardMappingDialog` (72 px).

Props: `src?: string | null`, `name: string`, `size?: number` (default 40).

### `WarehouseStatusChip`
Renders the marketplace-agnostic `MarketplaceWarehouseDto.status`: `active` → «Активный» (success), `inactive` → «Не активный» (default), `unavailable` → «Недоступен» (warning). The per-marketplace wording is collapsed into these three server-side (see [marketplaces-specification.md](marketplaces-specification.md)); `externalStatus` still travels along and shows up as a tooltip so «почему недоступен» stays answerable. Labels live in `WAREHOUSE_STATUS_LABELS` (`marketplaceUtils.ts`).

Props: `status: MarketplaceWarehouseStatus`, `externalStatus?: string | null`.

### `SyncErrorAlert`
Renders an `AppFieldError` (from `MarketplaceSyncRunDto.error` or `MarketplaceAccountDto.lastSyncError`) as a localized alert. The message comes from `resolveErrorMessage` (`code` + `args`); the English `detail` is never displayed. When `args.marketplaceResponse` is present it is appended as a monospace block — that is the raw marketplace body, already truncated server-side.

Props: `error: AppFieldError | null | undefined`, `title?`.

### `TestConnectionButton`
Probes marketplace credentials without saving them and renders the verdict inline. Passing `accountId="new"` works before the account exists — the server ignores the route id when the body carries an `apiKey`. Disabled until a key is typed.

Props: `accountId`, `type`, `clientId`, `apiKey`, `disabled?`.

### `marketplaceUtils`
Label maps for every marketplace enum (`MARKETPLACE_TYPE_LABELS`, `SYNC_STATUS_LABELS`, `SYNC_SCOPE_LABELS`, `WAREHOUSE_KIND_LABELS`, `MAPPING_SOURCE_LABELS`, `MAPPING_STATE_LABELS`) plus `formatDateTime`, `formatDuration`, `formatPrice`.

`hasCapability(capabilities, flag)` exists because `MarketplaceCapabilities` is a **`[Flags]` enum**: `JsonStringEnumConverter` sends a combination as one comma-separated string (`"warehouses, cards, sellerInfo"`), which the generated union of single values does not describe. Never compare `capabilities` with `===`.

### `pluralUtils`
Русская плюрализация счётчиков. Формы выбирает `Intl.PluralRules("ru-RU")`, а не ручная арифметика по `% 10` / `% 100`.

```ts
plural(n, forms)        // → одна форма: "задания"
pluralCount(n, forms)   // → "2 задания" (число через toLocaleString("ru-RU"))
```

`PluralForms` — это `{one, few, many}`: `one` — 1, 21, 31…; `few` — 2-4, 22-24…; `many` — 0, 5-20, 25-30…

Дробные CLDR относит к категории `"other"`, которой в `PluralForms` нет — `plural()` сводит её к `few`, потому что по-русски правильно «1,5 задания». Не заменяй это на индексацию `forms[category]`: она молча вернёт `undefined`.

`NOUNS` — общий словарь существительных в именительном падеже (`task`, `item`, `position`, `itemType`). Для повторно используемого счётчика добавляй слово сюда, а не в компонент.

Формы — произвольные строки, поэтому во фразах со согласованием глагола или прилагательного склоняй всю фразу целиком:

```ts
pluralCount(n, {
  one: "позиция будет удалена",
  few: "позиции будут удалены",
  many: "позиций будут удалены",
});
```

Слово в косвенном падеже (например после «для» — «для 2 **заданий**») в `NOUNS` не кладём: там только именительный. Такие формы объявляй константой рядом с местом использования (см. `TASKS_GENITIVE` в `BatchAssemblyDialog.tsx`).

Не трогаем сокращения — `шт.`, `поз.`, `комп.`, `м`, `мин`: они не склоняются.

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

### `ClampedIntegerField`
Number `TextField` (`components/form/ClampedIntegerField.tsx`) for editing a quantity/count outside of RHF (local state, or committed via a callback rather than a form). Keeps raw keystrokes uncommitted — including a temporarily empty field — until blur, so the min/max clamp doesn't fight the user while they're typing or clearing the field to type a new value. Use this instead of hand-rolling `Math.max(min, Number(e.target.value))` in an `onChange`, which snaps an emptied field back to the min on every keystroke.

```tsx
<ClampedIntegerField
  size="small"
  value={qty}
  min={0}       // default 1
  max={maxQty}  // optional
  onCommit={(n) => setQty(n)}
/>
```

Props: `value: number`, `min?: number` (default `1`), `max?: number`, `onCommit: (value: number) => void`, plus all `TextFieldProps` except `value`/`onChange`/`onBlur`/`onFocus`/`type`. If `value` changes externally (e.g. after a mutation invalidates and refetches), the displayed text re-syncs — unless the field is currently focused, so it won't clobber an in-progress edit.

### `FulfillmentsDrawer`
Read-only right-hand `Drawer` (`components/orders/FulfillmentsDrawer.tsx`) listing what was actually picked for one order position: source cell breadcrumb, inventory number for `Unit`, chosen variant for `Variation`, an expanded component table for `Bundle`, plus who assembled it and when. Opened by clicking a row in the order page's «Коробки и состав» and «Задания на сборку» cards, and by the eye `IconButton` on every component row of the assembler page (`OrdersAssemblyPage`).

```tsx
<FulfillmentsDrawer
  open
  onClose={() => setTarget(null)}
  title={component.catalogItemName}
  quantity={component.quantity}
  isVariation={component.catalogItemType === "variation"}
  catalogItemId={component.catalogItemId}
  fulfillments={fulfillments}
/>
```

Every catalog item inside the drawer is a `CatalogItemLink` opening a nested `CatalogItemDrawer`, held in the `?fulfillmentCatalogItem=` param (an [ephemeral param](#stripephemeralsearchparams), since this drawer's own open state is local): the card headline (`Инв. № …` / `× N` / `Комплект (N комп.)`) uses the optional `catalogItemId` prop — the position's own item, which `AssemblyFulfillmentDto` doesn't carry, so callers pass it down; the «Вариант: …» row uses `resolvedCatalogItemId`; bundle rows use each component's `catalogItemId`. Without `catalogItemId` the headline just renders as plain text.

Helpers in `components/orders/orderAssemblyUtils.ts`:
- `countFulfilledQty(fulfillments)` — progress count; a `Unit`/`Bundle` fulfillment always counts as 1.
- `getFulfillmentKind(fulfillment)` — `"unit" | "bundle" | "standard"`, so the three call sites don't each re-derive it.
- `collectBoxComponentFulfillments(order, orderBoxId, catalogItemId)` — fulfillments hang off `AssemblyTaskBoxComponent`, so an order box component's ones have to be gathered across every assembly task that took on that box.

### `useDefaultStorageNode(warehouseId, enabled?)`
Hook (`hooks/useDefaultStorageNode.ts`) fetching the warehouse's default storage cell via `GET /api/warehouses/{id}/default-node`. Returns a `SelectedNode | null` — `null` while loading, on error, or if the warehouse has no default cell assigned (`defaultStoragePlaceNodeId` unset). `enabled` (default `true`) gates the query, e.g. to skip it for non-`standard` catalog item types that don't need a storage cell.

```tsx
const defaultNode = useDefaultStorageNode(warehouseId, catalogItemType === "standard");
```

Used to pre-fill node pickers with the default cell instead of requiring a manual pick every time — see [orders-specification.md § Дефолтная ячейка склада](orders-specification.md#дефолтная-ячейка-склада-в-фулфилменте) for the pattern used in `AddFulfillmentDialog`/`BatchAssemblyDialog` (override-over-default merge, since directly `setState`-ing from inside a `useEffect` trips the `react-hooks/set-state-in-effect` lint rule). `AddPlacementDialog` uses the same underlying endpoint directly (`warehousesGetDefaultNodeOptions`) since it only needs to seed initial state once, not merge with a live override.

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

#### Tabbed detail pages

Tabs inside a detail page are **not** separate routes — they are a `<Tabs>` plus one `useSyncedWithQueryState` entry, so the page loads its entity once and every tab's own filter/sort/page params share the same URL. `toQuery` returns `null` for the default tab to keep the canonical URL clean. Introduced by `MarketplaceAccountPage`; reuse it rather than adding `:id/<tab>` subroutes.

```typescript
const TAB_KEYS = ["overview", "warehouses", "cards", "runs"] as const;
type TabKey = (typeof TAB_KEYS)[number];

const [tab, setTab] = useSyncedWithQueryState<TabKey>(
  "tab",
  (q) => (TAB_KEYS.includes(q as TabKey) ? (q as TabKey) : "overview"),
  (v) => (v === "overview" ? null : v),
);
```

Render tabs conditionally (`{tab === "cards" && <CardsTab />}`) so inactive tabs hold no queries, and validate the value against what is actually available — a deep link can name a tab the current entity does not have.

Because the tabs share one URL they also share param names, so switching tabs must clear every tab-scoped param (`search`, `page`, `pageSize`, `sortBy`, `sortOrder`, `archived`, and page-specific ones) — otherwise `page=2` from one tab lands the next on an empty page. `setParam` from `useSearchParamsContext` batches all same-tick calls into one `replace` navigation, so clearing the list and setting `tab` costs a single history entry:

```typescript
const changeTab = (next: TabKey) => {
  for (const key of TAB_SCOPED_PARAMS) setParam(key, null);
  setTab(next);
};
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

### `useDrawerSearchParamsState(name)`

Manages the open/close state of a detail drawer (or dialog) by storing the selected entity ID in a URL query param. Enables deep linking and browser back-button support.

Returns `[selectedItemId, openDrawer, closeDrawer]`:

- `selectedItemId` — `string | null`; the current value of `?{name}=`, or `null` when the drawer is closed. Pass directly to `open={!!selectedItemId}` and to fetch the entity.
- `openDrawer(id)` — navigates **forward** (`navigate(...)` without `replace`), adding `?{name}={id}` to the URL. Pressing browser back closes the drawer.
- `closeDrawer()` — removes the param using `replace: true`, so closing via button/×/escape doesn't add an extra history entry. Guards against no-ops when the param is already absent.

**When to use:** whenever a page has a drawer or side panel whose open state should survive a page refresh, be bookmarkable, and work with the browser back button. Use instead of `useState` for any persistent drawer. Do not use for transient UI state that should not be reflected in the URL (loading spinners, hover states, etc.).

**Usage:**

```tsx
const [selectedId, openDrawer, closeDrawer] = useDrawerSearchParamsState("item");

// open drawer on row click
<TableRow onClick={() => openDrawer(item.id)} />

// drawer
<MyDrawer
  open={!!selectedId}
  onClose={closeDrawer}
  item={data?.items.find(x => x.id === selectedId)}
/>
```

**Currently used in:**
- `CatalogPage` — `?item=` param, opens `CatalogItemDrawer`
- `WarehouseViewPage` — `?storagePlace=` param, opens `StoragePlaceDialog`
- `ItemsBasePage` — `?catalogItem=`, `?unitCatalogItem=`, `?bundleCatalogItem=` params for the three inventory drawers
- `ReceiptItemsSection`, `WriteoffItemsSection` — `?catalogItem=`, opens `CatalogItemDrawer`
- `CatalogItemDrawerHost` — `?catalogItem=`, one drawer per page shared via context (`OrderPage`, `OrdersAssemblyPage`)
- `FulfillmentsDrawer` — `?fulfillmentCatalogItem=`, an ephemeral param (see [`stripEphemeralSearchParams()`](#stripephemeralsearchparams))

**History semantics:**
- Open: pushes a new history entry → back button closes the drawer.
- Close via button: replaces current entry → back button goes to the page visited before the drawer was opened.

**Params of drawers nested in a non-URL parent** must be registered in `EPHEMERAL_PARAMS` — a refresh would otherwise restore the nested drawer with its parent closed.

### `InstallPrompt` / `UpdatePrompt`
PWA lifecycle UI. `InstallPrompt` triggers `beforeinstallprompt`. `UpdatePrompt` calls `updateServiceWorker()` from `ServiceWorkerContext` when a new SW version is available.

## Form Hooks

### `useRhfApiErrors<T extends FieldValues>(form)`

Bridges API error responses to an RHF form. Returns `{ setApiError }`.

**`setApiError(error: unknown)`** — call this in a mutation's `onError` to wire API errors into form fields automatically.

**Behavior:**

| Error shape | What happens |
|---|---|
| `AppProblemDetails` with field errors | Each field key (except `"root"`) → `form.setError(field, {type: "server", message})` |
| `AppProblemDetails` with root errors | `"root"` errors → `form.setError("root", {type: "server", message})` |
| `AppProblemDetails` with no matching errors | Falls back to `error.title ?? "Неизвестная ошибка"` set on `"root"` |
| Any other error shape | Shows a modal alert via `useModal().showAlert` |

Field error messages are resolved through `resolveErrorMessage` — which prefers the detailed `errorCodeArgMessages` template when `args` fills every `{placeholder}` (e.g. `insufficientInventory` gets the item name, quantities and the cell path), otherwise falls back to `errorCodeMessages` and interpolates whatever `args` are present.

```tsx
const form = useForm<LoginFormValues>();
const {setApiError} = useRhfApiErrors(form);

const mutation = useMutation({
  ...postApiAuthLoginMutationOptions(),
  onSuccess: handleSuccess,
  onError: setApiError,
});

// Field-level errors are shown automatically via FormTextField.
// Root errors must be rendered manually:
// {form.formState.errors.root && (
//   <Alert severity="error">{form.formState.errors.root.message}</Alert>
// )}
```

**Dependencies:** `useModal` (for non-structured errors), `isAppProblemDetails` + `resolveErrorMessage` from `@/utils/errorUtils`.

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
