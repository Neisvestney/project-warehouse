import type {MeResponse} from "@/api/types.gen";
import {decodeJwtClaims} from "./jwt";

type JwtUser = MeResponse & {roles: []};

function claimString(value: unknown): string | null {
  return typeof value === "string" && value ? value : null;
}

export function parseJwtUser(): JwtUser | null {
  const token = localStorage.getItem("accessToken");
  if (!token) return null;
  try {
    const claims = decodeJwtClaims(token);
    if (typeof claims.exp === "number" && claims.exp * 1000 < Date.now()) return null;

    // A payload without `sub` is not a token of ours — treat it as no user rather than an empty one.
    const id = claimString(claims.sub);
    if (!id) return null;

    const perm = claims.permission;
    const username = claimString(claims.name) ?? "";
    const firstName = claimString(claims.given_name);
    const lastName = claimString(claims.family_name);

    return {
      id,
      username,
      fullName: [firstName, lastName].filter(Boolean).join(" ") || username,
      email: claimString(claims.email),
      firstName,
      lastName,
      roles: [],
      // The token carries no warehouse assignment; the real list arrives with /me, so warehouse-scoped UI
      // stays closed until then.
      assignedWarehouseIds: [],
      permissions: Array.isArray(perm)
        ? perm.filter((p): p is string => typeof p === "string")
        : typeof perm === "string"
          ? [perm]
          : [],
    };
  } catch {
    return null;
  }
}
