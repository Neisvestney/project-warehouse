import {client} from "@/api/client.gen";
import type {TokenResponse} from "@/api/types.gen";
import {getCurrentConnectionId} from "@/contexts/Realtime/currentConnectionId";
import {broadcastAuth, clearAuthChannelStorage, setupAuthChannel} from "@/services/authChannel";
import {isAppProblemDetails} from "@/utils/errorUtils";

/**
 * `invalid` is the only outcome that ends the session. `unavailable` means the server could not answer —
 * the refresh token is still good, so the caller keeps it and lets the user retry.
 */
export type RefreshOutcome = "ok" | "invalid" | "unavailable";

let refreshingPromise: Promise<RefreshOutcome> | null = null;

async function refreshTokens(): Promise<RefreshOutcome> {
  if (refreshingPromise) return refreshingPromise;

  refreshingPromise = lockedRefresh().finally(() => {
    refreshingPromise = null;
  });

  return refreshingPromise;
}

// The refresh token is single-use: the server revokes it the instant it is accepted, so two tabs racing
// with the same token would leave the loser holding a revoked one and log everybody out. The lock makes
// the rotation origin-wide, and whoever waited on it finds the winner's tokens already in localStorage.
async function lockedRefresh(): Promise<RefreshOutcome> {
  if (!navigator.locks) return doRefreshTokens();

  const tokenBefore = localStorage.getItem("accessToken");
  try {
    return await navigator.locks.request(
      "auth-refresh",
      // A tab whose fetch hangs must not hold the rotation for every other tab.
      {signal: AbortSignal.timeout(30_000)},
      async () => {
        const current = localStorage.getItem("accessToken");
        if (current === tokenBefore) return doRefreshTokens();
        // Another tab finished while we waited: it rotated, or it logged out and took the token away.
        return current ? "ok" : "invalid";
      },
    );
  } catch {
    // Waiting for the lock timed out; the holder's own refresh decides the session, not this one.
    return "unavailable";
  }
}

async function doRefreshTokens(): Promise<RefreshOutcome> {
  const refreshToken = localStorage.getItem("refreshToken");
  if (!refreshToken) return "invalid";

  let res: Response;
  try {
    res = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({refreshToken}),
    });
  } catch {
    return "unavailable";
  }

  if (!res.ok) {
    return res.status >= 400 && res.status < 500 ? "invalid" : "unavailable";
  }

  try {
    storeTokens(await res.json());
  } catch {
    // 200 with a body that is not a token pair — a captive portal or a proxy, not our server.
    return "unavailable";
  }
  return "ok";
}

export function storeTokens(tokens: TokenResponse) {
  localStorage.setItem("accessToken", tokens.accessToken);
  localStorage.setItem("refreshToken", tokens.refreshToken);
  localStorage.setItem("tokenExpiry", String(Date.now() + Number(tokens.expiresIn) * 1000));
  window.dispatchEvent(new Event("auth:tokens"));
  broadcastAuth("tokens");
}

// Proactively refreshes the token 30s before it expires, the same window the request interceptor
// uses. Exported for callers that send their own requests instead of going through the client —
// the telemetry exporters take their Authorization header from here.
export async function getFreshAccessToken(): Promise<string | null> {
  const token = localStorage.getItem("accessToken");
  if (!token) return null;

  const expiry = parseInt(localStorage.getItem("tokenExpiry") ?? "0");
  if (expiry <= 0 || Date.now() + 30_000 > expiry) {
    // A rejected refresh ends the session here rather than letting a known-dead token go out and come
    // back as a 401 the interceptor has to refresh all over again.
    if ((await refreshTokens()) === "invalid") {
      window.dispatchEvent(new CustomEvent("auth:refreshTokenInvalid"));
      clearTokens();
      return null;
    }
  }

  return localStorage.getItem("accessToken");
}

export function clearTokens() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
  localStorage.removeItem("tokenExpiry");
  window.dispatchEvent(new Event("auth:clear"));
  broadcastAuth("clear");
  clearAuthChannelStorage();
}

// Stores clones of requests that carry a body, for 401 retry — a consumed body cannot be re-read,
// while a bodyless request is replayable as it is. WeakMap keys are GC'd with their requests.
const retryClones = new WeakMap<Request, Request>();

export function setupApiClient() {
  client.setConfig({baseUrl: window.location.origin});
  setupAuthChannel();

  // Proactively refresh the token 30s before it expires so requests never hit 401 due to expiry.
  // Concurrent calls share a single in-flight refresh promise to prevent rotation conflicts.
  client.interceptors.request.use(async (request) => {
    const current = await getFreshAccessToken();
    if (current) request.headers.set("Authorization", `Bearer ${current}`);

    // Resolved per request, not once at startup: a PWA left open overnight can cross a DST
    // transition or a zone border and has to start sending the new value on its own.
    request.headers.set("X-Time-Zone", Intl.DateTimeFormat().resolvedOptions().timeZone);

    // Lets the server skip this tab when fanning out the entityChanged event this request triggers —
    // other tabs or devices of the same user still get it.
    const connectionId = getCurrentConnectionId();
    if (connectionId) request.headers.set("X-Realtime-Connection-Id", connectionId);

    if (request.body !== null) retryClones.set(request, request.clone());

    return request;
  });

  // On 401: try refreshing tokens and replay the request once with the new token.
  // Only an explicit rejection of the refresh token ends the session.
  client.interceptors.response.use(async (response, request) => {
    if (response.status !== 401) return response;
    if (!localStorage.getItem("accessToken")) return response;

    const outcome = await refreshTokens();
    const replay = retryClones.get(request) ?? (request.body === null ? request : null);
    retryClones.delete(request);

    if (outcome === "invalid") {
      window.dispatchEvent(new CustomEvent("auth:refreshTokenInvalid"));
      clearTokens();
      return response;
    }

    // Unreachable server, or a request with a body that never passed through the request interceptor
    // and so has nothing to replay: neither says anything about the session, so the caller just sees
    // the 401.
    if (outcome === "unavailable" || !replay) return response;

    const newToken = localStorage.getItem("accessToken");
    const headers = new Headers(replay.headers);
    if (newToken) headers.set("Authorization", `Bearer ${newToken}`);

    return fetch(new Request(replay, {headers}));
  });

  // Normalize non-AppProblemDetails HTTP errors (e.g. 502 {} body) to the status code string
  // so extractErrorMessage can map them to a human-readable message.
  // Network errors have no response, so they pass through as-is.
  client.interceptors.error.use((error, response) => {
    if (response && !isAppProblemDetails(error)) {
      return String(response.status);
    }
    return error;
  });
}
