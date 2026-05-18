import {client} from "@/api/client.gen";
import type {TokenResponse} from "@/api/types.gen";
import {isAppProblemDetails} from "@/utils/errorUtils";

let refreshingPromise: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  if (refreshingPromise) return refreshingPromise;

  refreshingPromise = doRefreshTokens().finally(() => {
    refreshingPromise = null;
  });

  return refreshingPromise;
}

async function doRefreshTokens(): Promise<boolean> {
  const refreshToken = localStorage.getItem("refreshToken");
  if (!refreshToken) return false;

  try {
    const res = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({refreshToken}),
    });

    if (!res.ok) {
      // Only revoke on explicit auth rejections — 5xx means the server is down, not
      // that the refresh token is invalid, so keep tokens and let the user retry.
      if (res.status >= 400 && res.status < 500) {
        clearTokens();
      }
      return false;
    }

    storeTokens(await res.json());
    window.dispatchEvent(new Event("auth:refresh"));
    return true;
  } catch {
    // Network error — tokens are still valid, we just can't reach the server.
    return false;
  }
}

export function storeTokens(tokens: TokenResponse) {
  localStorage.setItem("accessToken", tokens.accessToken);
  localStorage.setItem("refreshToken", tokens.refreshToken);
  localStorage.setItem("tokenExpiry", String(Date.now() + Number(tokens.expiresIn) * 1000));
}

export function clearTokens() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
  localStorage.removeItem("tokenExpiry");
  window.dispatchEvent(new Event("auth:clear"));
}

// Stores request clones for 401 retry. WeakMap keys are GC'd automatically with their requests.
const retryClones = new WeakMap<Request, Request>();

export function setupApiClient() {
  client.setConfig({baseUrl: window.location.origin});

  // Proactively refresh the token 30s before it expires so requests never hit 401 due to expiry.
  // Concurrent calls share a single in-flight refresh promise to prevent rotation conflicts.
  // Clone is stored here — after fetch() the body is consumed and can't be re-read.
  client.interceptors.request.use(async (request) => {
    const expiry = parseInt(localStorage.getItem("tokenExpiry") ?? "0");
    const token = localStorage.getItem("accessToken");

    if (token) {
      const hasExpiry = expiry > 0;
      if (!hasExpiry || Date.now() + 30_000 > expiry) {
        await refreshTokens();
      }
      const current = localStorage.getItem("accessToken");
      if (current) request.headers.set("Authorization", `Bearer ${current}`);
    }

    retryClones.set(request, request.clone());

    return request;
  });

  // On 401: try refreshing tokens and replay the request once with the new token.
  // Falls back to clearTokens() when the refresh token is also invalid.
  client.interceptors.response.use(async (response, request) => {
    if (response.status !== 401) return response;
    if (!localStorage.getItem("accessToken")) return response;

    const refreshed = await refreshTokens();
    const clone = retryClones.get(request);
    retryClones.delete(request);

    if (!refreshed || !clone) {
      window.dispatchEvent(new CustomEvent("auth:refreshTokenInvalid"));
      clearTokens();
      return response;
    }

    const newToken = localStorage.getItem("accessToken");
    const headers = new Headers(clone.headers);
    if (newToken) headers.set("Authorization", `Bearer ${newToken}`);

    return fetch(new Request(clone, {headers}));
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
