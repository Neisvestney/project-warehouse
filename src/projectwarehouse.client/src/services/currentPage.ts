/**
 * The screen the user is on, kept as a plain module value so that spans and log records can be
 * tagged with it from anywhere — including code that runs long before the telemetry SDK loads.
 * Fed by `TelemetryRouteLogger`.
 */

/** Attribute the page is reported under, on both spans and log records. */
export const ATTR_APP_PAGE = "app.page";

// Routes are static path segments, so the raw pathname is a low-cardinality value and needs no
// pattern matching. Seeded from the URL because the first requests fly before React mounts.
let currentPage = typeof window === "undefined" ? "" : window.location.pathname;

export function setCurrentPage(path: string) {
  currentPage = path;
}

export function getCurrentPage(): string {
  return currentPage;
}
