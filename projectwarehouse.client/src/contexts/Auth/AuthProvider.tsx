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
      const {data: me, error} = await authMe();
      if (error) {
        // The interceptor may return a status-code string or TypeError at runtime,
        // even though the generated type says AppProblemDetails.
        const e: unknown = error;
        // Transient errors (network failure, 5xx) — throw so TanStack keeps stale data.
        if (e instanceof Error || (typeof e === "string" && e.startsWith("5"))) {
          throw e;
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

  useEffect(() => {
    const handler = () => {
      queryClient.clear();
      queryClient.setQueryData(ME_QUERY_KEY, null);
      setHasTokens(false);
    };
    window.addEventListener("auth:clear", handler);
    return () => window.removeEventListener("auth:clear", handler);
  }, [queryClient]);

  useEffect(() => {
    const handler = () => {
      queryClient.invalidateQueries({queryKey: ME_QUERY_KEY});
    };
    window.addEventListener("auth:refresh", handler);
    return () => window.removeEventListener("auth:refresh", handler);
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
    await authLogout({body: {refreshToken}});
    clearTokens();
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
