import { client } from '@/api/client.gen';
import type { TokenResponse } from '@/api/types.gen';

let refreshingPromise: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  if (refreshingPromise) return refreshingPromise;

  refreshingPromise = doRefreshTokens().finally(() => {
    refreshingPromise = null;
  });

  return refreshingPromise;
}

async function doRefreshTokens(): Promise<boolean> {
  const refreshToken = localStorage.getItem('refreshToken');
  if (!refreshToken) return false;

  try {
    const res = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });

    if (!res.ok) {
      clearTokens();
      return false;
    }

    storeTokens(await res.json());
    return true;
  } catch {
    clearTokens();
    return false;
  }
}

export function storeTokens(tokens: TokenResponse) {
  localStorage.setItem('accessToken', tokens.accessToken);
  localStorage.setItem('refreshToken', tokens.refreshToken);
  localStorage.setItem('tokenExpiry', String(Date.now() + Number(tokens.expiresIn) * 1000));
}

export function clearTokens() {
  localStorage.removeItem('accessToken');
  localStorage.removeItem('refreshToken');
  localStorage.removeItem('tokenExpiry');
}

export function setupApiClient() {
  client.setConfig({ baseUrl: '/api' });

  // Proactively refresh the token 30s before it expires so requests never hit 401 due to expiry.
  // Concurrent calls share a single in-flight refresh promise to prevent rotation conflicts.
  client.interceptors.request.use(async (request) => {
    const expiry = parseInt(localStorage.getItem('tokenExpiry') ?? '0');
    const token = localStorage.getItem('accessToken');

    if (token) {
      const hasExpiry = expiry > 0;
      if (!hasExpiry || Date.now() + 30_000 > expiry) {
        await refreshTokens();
      }
      const current = localStorage.getItem('accessToken');
      if (current) request.headers.set('Authorization', `Bearer ${current}`);
    }

    return request;
  });

  // Fallback: clear tokens on 401 (revoked session, security_version bump on server side, etc.)
  client.interceptors.response.use(async (response) => {
    if (response.status === 401) clearTokens();
    return response;
  });
}
