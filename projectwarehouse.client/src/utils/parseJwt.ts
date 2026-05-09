import type {MeResponse} from "@/api/types.gen";

type JwtUser = MeResponse & {roles: []};

export function parseJwtUser(): JwtUser | null {
  const token = localStorage.getItem("accessToken");
  if (!token) return null;
  try {
    const b64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const payload = JSON.parse(atob(b64));
    if (payload.exp && payload.exp * 1000 < Date.now()) return null;
    const perm = payload.permission;
    return {
      id: payload.sub,
      username: payload.name,
      email: payload.email ?? null,
      firstName: payload.given_name ?? null,
      lastName: payload.family_name ?? null,
      roles: [],
      permissions: Array.isArray(perm) ? perm : perm ? [perm] : [],
    };
  } catch {
    return null;
  }
}
