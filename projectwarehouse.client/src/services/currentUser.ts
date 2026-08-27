/**
 * Who the telemetry belongs to, read off the access token. Names and values match what
 * `TelemetryEnrichmentMiddleware` writes on the server, so one filter by `user.id` catches both
 * sides of a trace.
 *
 * Depends on nothing but `localStorage`, which is what lets the light log module use it before the
 * SDK loads and outside of React.
 */

/** Claim `sub`. */
export const ATTR_USER_ID = "user.id";
/** Claim `name`. */
export const ATTR_USER_NAME = "user.name";

let cachedToken: string | null = null;
let cachedAttributes: Record<string, string> = {};

function decodeClaims(token: string): Record<string, unknown> {
  // Not verified, and does not need to be: the token comes from our own server and is used here as
  // a label, never as a permission.
  const payload = token.split(".")[1];
  if (!payload) return {};

  const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
  const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
}

/** Empty while nobody is signed in — the attributes are then simply absent from the record. */
export function getCurrentUserAttributes(): Record<string, string> {
  const token = localStorage.getItem("accessToken");
  if (token === cachedToken) return cachedAttributes;

  cachedToken = token;
  cachedAttributes = {};
  if (!token) return cachedAttributes;

  try {
    const claims = decodeClaims(token);
    if (typeof claims.sub === "string") cachedAttributes[ATTR_USER_ID] = claims.sub;
    if (typeof claims.name === "string" && claims.name)
      cachedAttributes[ATTR_USER_NAME] = claims.name;
  } catch {
    // A malformed token is the auth layer's problem; telemetry just goes out unlabelled.
  }
  return cachedAttributes;
}
