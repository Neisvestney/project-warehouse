import {createContext} from "react";
import type {MeResponse} from "@/api/types.gen";

export interface AuthContextValue {
  user: MeResponse | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  profileIsLoadError: boolean;
  profileLoadError: Error | null;
}

const AuthContext = createContext<AuthContextValue | null>(null);
export default AuthContext;
