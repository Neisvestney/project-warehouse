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
│   ├── MainAppBar/
│   │   └── MainAppBar.tsx       # Top nav bar with logo and links
│   ├── InstallPrompt/
│   │   └── InstallPrompt.tsx    # PWA "Add to Home Screen" prompt
│   ├── UpdatePromt/
│   │   └── UpdatePrompt.tsx     # Service worker update banner
│   └── ScannerBlock/
│       ├── ScannerBlock.tsx     # Camera scanner orchestrator
│       └── components/
│           ├── ScanArea.tsx            # <video> element + canvas
│           ├── ScanFrameOverlay.tsx    # Viewfinder + barcode rect overlay
│           ├── CameraSelectDialog.tsx  # Camera device picker dialog
│           ├── ZoomControls.tsx        # Zoom slider/buttons
│           └── index.ts
│
├── configuration/
│   └── flagsConstants.ts        # IS_DEV flag (enables console logs)
│
├── contexts/
│   ├── ServiceWorkerContext.ts  # Context: needRefresh, offlineReady, updateServiceWorker
│   ├── SearchParamsContext.ts   # Context + useSearchParamsContext hook
│   └── SearchParamsProvider.tsx # Batched URL search params provider
│
├── layouts/
│   └── MainLayout/
│       └── MainLayout.tsx       # Shell with MainAppBar + <Outlet />
│
├── hooks/
│   ├── useDebounce.ts                        # Generic debounce: T → debounced T after delay ms
│   ├── useSyncedWithQueryState.ts            # Sync a typed value with a URL query param
│   ├── useDebouncedSyncedWithQueryState.ts   # Local state + debounce + URL sync in one hook
│   ├── useParamsState.ts                     # Merge debounced + immediate params for API queries
│   └── usePaginatedParams.ts                 # Pagination wrapper: page/pageSize from URL + page reset
│
├── pages/
│   ├── HomePage/
│   │   └── HomePage.tsx         # Landing page with navigation cards
│   ├── ScannerPage/
│   │   └── ScannerPage.tsx      # Full-screen scanner + scanned codes drawer
│   └── UsersPage/
│       └── UsersPage.tsx        # Paginated, searchable user list (requires users.view)
│
├── api/                         # Auto-generated — run `npm run generate-api` to refresh
│   ├── client/                  # Bundled fetch client (from @hey-api/openapi-ts)
│   ├── client.gen.ts            # Client singleton
│   ├── types.gen.ts             # TypeScript types from OpenAPI schema
│   ├── sdk.gen.ts               # Typed SDK functions for all endpoints
│   ├── index.ts                 # Re-exports everything
│   └── @tanstack/
│       └── react-query.gen.ts   # queryOptions / mutationOptions factories
│
├── services/
│   └── apiClient.ts             # Client config, token storage, refresh interceptor
│
└── utils/
    ├── camera/
    │   ├── useCameraStream.ts   # Hook: getUserMedia, device selection
    │   ├── useCameraFocus.ts    # Hook: focus via MediaStreamTrack
    │   ├── useCameraZoom.ts     # Hook: zoom via MediaStreamTrack capabilities
    │   ├── cameraUtils.ts       # Device enumeration, constraint helpers
    │   └── index.ts
    ├── qrTools.ts               # zxing-wasm decode + Otsu binarization + BarcodeDetector fallback
    └── useInstallPrompt.ts      # Hook: beforeinstallprompt event
```

## Routing

`BrowserRouter` in `main.tsx`. Pages are lazy-loaded via `React.lazy` + `Suspense`.

```
/           → MainLayout > HomePage
/scanner    → ScannerPage  (no layout wrapper — full-screen)
/users      → MainLayout > UsersPage  (requires users.view permission)
```

## Pages

### `HomePage`
Landing page. Shows navigation cards (scanner link), PWA offline-ready indicator, and the `InstallPrompt` when the app is installable.

### `ScannerPage`
Full-screen camera scanner. Uses `ScannerBlock` for capture/decode. Maintains a list of scanned barcodes in local state; opens a bottom drawer when `?scannedCodesDrawerOpen=true` is in the query string.

### `UsersPage`
Server-side paginated and searchable table of users. Requires `users.view` permission. State is stored in URL params (`?search=`, `?page=`, `?pageSize=`) using `useDebouncedSyncedWithQueryState` + `usePaginatedParams`. The search field updates instantly without lag; the URL and API call update after a 300 ms debounce.

## Key Components

### `ScannerBlock`
Orchestrates the full camera scan loop:
1. Acquires a camera stream via `useCameraStream` (persists preferred device ID in `localStorage`)
2. Renders `ScanArea` (video element), `ScanFrameOverlay` (viewfinder rect), `ZoomControls`, `CameraSelectDialog`
3. On each frame: captures to canvas → runs Otsu binarization + optional inversion → decodes with zxing-wasm (primary) or native `BarcodeDetector` (fallback)
4. Emits decoded barcodes via `onScan` callback

Scan interval is configurable (4–25 FPS equivalent).

### `MainAppBar`
Top navigation bar. Logo/title + mobile hamburger menu with links to `/scanner` and `/users`.

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
              ├── CssBaseline      (self-closing — global CSS reset)
              ├── UpdatePrompt
              └── Suspense > Routes
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
- Installs a response interceptor that clears stored tokens on any 401 (revoked session, server security version bump)

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
