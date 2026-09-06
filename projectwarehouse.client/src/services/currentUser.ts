/**
 * Who the telemetry belongs to, read off the access token. Names and values match what
 * `TelemetryEnrichmentMiddleware` writes on the server, so one filter by `user.id` catches both
 * sides of a trace.
 *
 * Depends on nothing but `localStorage`, which is what lets the light log module use it before the
 * SDK loads and outside of React.
 */

import {decodeJwtClaims} from "@/utils/jwt";

/** Claim `sub`. */
export const ATTR_USER_ID = "user.id";
/** Claim `name`. */
export const ATTR_USER_NAME = "user.name";

let cachedToken: string | null = null;
let cachedAttributes: Record<string, string> = {};

/** Empty while nobody is signed in — the attributes are then simply absent from the record. */
export function getCurrentUserAttributes(): Record<string, string> {
  const token = localStorage.getItem("accessToken");
  if (token === cachedToken) return cachedAttributes;

  cachedToken = token;
  cachedAttributes = {};
  if (!token) return cachedAttributes;

  try {
    const claims = decodeJwtClaims(token);
    if (typeof claims.sub === "string") cachedAttributes[ATTR_USER_ID] = claims.sub;
    if (typeof claims.name === "string" && claims.name)
      cachedAttributes[ATTR_USER_NAME] = claims.name;
  } catch {
    // A malformed token is the auth layer's problem; telemetry just goes out unlabelled.
  }
  return cachedAttributes;
}
