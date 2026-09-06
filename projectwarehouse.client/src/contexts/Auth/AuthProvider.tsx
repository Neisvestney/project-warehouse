import React, {useCallback, useEffect, useState} from "react";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {authLogin, authLogout, authMe} from "@/api/sdk.gen";
import {clearTokens, storeTokens} from "@/services/apiClient";
import type {MeResponse} from "@/api/types.gen";
import AuthContext from "./AuthContext";
import {parseJwtUser} from "@/utils/parseJwt";

const ME_QUERY_KEY = ["auth", "me"] as const;

export function AuthProvider({children}: {children: React.ReactNode}) {
  const queryClient = useQueryClient();
  const [initialUser] = useState(() => parseJwtUser());
  const [hasTokens, setHasTokens] = useState(
    () => !!localStorage.getItem("accessToken") || !!localStorage.getItem("refreshToken"),
  );

  const {data, isPending, isError, error} = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: async (): Promise<MeResponse | null> => {
      const {data: me, error, response} = await authMe();
      if (error) {
        // Typed non-optional, but absent when fetch itself threw — offline, DNS, aborted.
        const status = (response as Response | undefined)?.status;

        // Only the server saying "not you" ends the session. Everything else — unreachable server, 500,
        // a WAF answering 403 — is transient, so throw and let TanStack keep the stale user.
        if (status !== 401) {
          throw error;
        }
        return null;
      }
      return me ?? null;
    },
    enabled: hasTokens,
    initialData: initialUser ?? undefined,
    retry: false,
    meta: {suppressGlobalError: true},
  });

  // Both events are raised by this tab when it stores or drops tokens, and re-raised locally by
  // authChannel when another tab does — so a login, a refresh or a logout anywhere lands here.
  useEffect(() => {
    const handler = () => {
      // Cancel first: clear() leaves live observers to refetch immediately, and those requests would go
      // out unauthenticated before the guard has a chance to unmount the tree.
      void queryClient.cancelQueries();
      queryClient.clear();
      queryClient.setQueryData(ME_QUERY_KEY, null);
      setHasTokens(false);
    };
    window.addEventListener("auth:clear", handler);
    return () => window.removeEventListener("auth:clear", handler);
  }, [queryClient]);

  useEffect(() => {
    const handler = () => {
      setHasTokens(true);
      void queryClient.invalidateQueries({queryKey: ME_QUERY_KEY});
    };
    window.addEventListener("auth:tokens", handler);
    return () => window.removeEventListener("auth:tokens", handler);
  }, [queryClient]);

  const login = useCallback(
    async (username: string, password: string) => {
      const {data: tokens, error} = await authLogin({body: {username, password}});
      if (error || !tokens) throw error ?? new Error("Login failed");
      storeTokens(tokens);
      setHasTokens(true);
      queryClient.setQueryData(ME_QUERY_KEY, parseJwtUser());
      await queryClient.invalidateQueries({queryKey: ME_QUERY_KEY});
    },
    [queryClient],
  );

  const logout = useCallback(async () => {
    const refreshToken = localStorage.getItem("refreshToken") ?? "";
    try {
      // Best effort: revoking the refresh token server-side must not decide whether this device
      // actually logs out. An unreachable server would otherwise leave the user signed in.
      await authLogout({body: {refreshToken}});
    } finally {
      clearTokens();
    }
  }, []);

  const user = (data as MeResponse | null | undefined) ?? null;
  const isLoading = !initialUser && hasTokens && isPending;

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user,
        login,
        logout,
        profileIsLoadError: isError,
        profileLoadError: error,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
