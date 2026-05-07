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
│   └── ServiceWorkerContext.ts  # Context: needRefresh, offlineReady, updateServiceWorker
│
├── layouts/
│   └── MainLayout/
│       └── MainLayout.tsx       # Shell with MainAppBar + <Outlet />
│
├── pages/
│   ├── HomePage/
│   │   └── HomePage.tsx         # Landing page with navigation cards
│   └── ScannerPage/
│       └── ScannerPage.tsx      # Full-screen scanner + scanned codes drawer
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
```

## Pages

### `HomePage`
Landing page. Shows navigation cards (scanner link), PWA offline-ready indicator, and the `InstallPrompt` when the app is installable.

### `ScannerPage`
Full-screen camera scanner. Uses `ScannerBlock` for capture/decode. Maintains a list of scanned barcodes in local state; opens a bottom drawer when `?scannedCodesDrawerOpen=true` is in the query string.

## Key Components

### `ScannerBlock`
Orchestrates the full camera scan loop:
1. Acquires a camera stream via `useCameraStream` (persists preferred device ID in `localStorage`)
2. Renders `ScanArea` (video element), `ScanFrameOverlay` (viewfinder rect), `ZoomControls`, `CameraSelectDialog`
3. On each frame: captures to canvas → runs Otsu binarization + optional inversion → decodes with zxing-wasm (primary) or native `BarcodeDetector` (fallback)
4. Emits decoded barcodes via `onScan` callback

Scan interval is configurable (4–25 FPS equivalent).

### `MainAppBar`
Top navigation bar. Logo/title + mobile hamburger menu with a link to `/scanner`.

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
