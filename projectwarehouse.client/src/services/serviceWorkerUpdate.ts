const MIN_CHECK_INTERVAL_MS = 60_000;

let lastCheckAt = 0;
let inFlight: Promise<ServiceWorkerRegistration | null> | null = null;

async function runCheck(): Promise<ServiceWorkerRegistration | null> {
  try {
    console.log("Checking for Service Worker update");
    const registration = await navigator.serviceWorker.getRegistration();
    if (!registration) return null;
    await registration.update();
    // Only a check that reached the network closes the window: a failed one left the question open.
    lastCheckAt = Date.now();
    return registration;
  } catch {
    return null;
  }
}

/**
 * Asks the browser to re-fetch `sw.js`; a changed script installs a new worker and raises
 * `needRefresh`. Nothing else in the app triggers this check — registration happens once, and
 * client-side routing is not a navigation, so a long-lived tab would never look for an update.
 *
 * Returns the registration when the check ran, `null` when it was skipped or failed. Callers share
 * one throttle so the reconnect and periodic triggers cannot pile up; `force` bypasses the throttle
 * but still joins a check already in flight.
 */
export function checkForServiceWorkerUpdate(
  options: {force?: boolean} = {},
): Promise<ServiceWorkerRegistration | null> {
  console.log("Trying to check for Service Worker update");
  if (!("serviceWorker" in navigator)) return Promise.resolve(null);
  if (!options.force && Date.now() - lastCheckAt < MIN_CHECK_INTERVAL_MS)
    return Promise.resolve(null);

  inFlight ??= runCheck().finally(() => {
    inFlight = null;
  });

  return inFlight;
}
