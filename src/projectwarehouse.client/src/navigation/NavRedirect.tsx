import {Navigate} from "react-router";
import type {PermissionName} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import AccessDenied from "@/components/AccessDenied";
import {firstLeafPath, toNavItems} from "./navSection.ts";
import type {SectionConfig} from "./navSection.ts";

export interface NavRedirectProps {
  sections: SectionConfig[];
  basePath: string;
}

/** Sends a module or group root to the first page the user may open. */
function NavRedirect({sections, basePath}: NavRedirectProps) {
  const {user} = useAuth();
  const target = firstLeafPath(
    toNavItems(sections, (user?.permissions ?? []) as PermissionName[], basePath),
  );
  return target ? <Navigate to={target} replace /> : <AccessDenied />;
}

export default NavRedirect;
