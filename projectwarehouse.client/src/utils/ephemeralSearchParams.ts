// Drawers that live inside another non-URL drawer: their param must not survive a cold load,
// otherwise the nested drawer reopens over a closed parent.
const EPHEMERAL_PARAMS = ["fulfillmentCatalogItem"];

export function stripEphemeralSearchParams() {
  const url = new URL(window.location.href);
  if (!EPHEMERAL_PARAMS.some((p) => url.searchParams.has(p))) return;
  EPHEMERAL_PARAMS.forEach((p) => url.searchParams.delete(p));
  window.history.replaceState(window.history.state, "", url);
}
