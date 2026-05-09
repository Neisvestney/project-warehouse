import type {ReactElement} from "react";
import type {RouteProps} from "react-router";
import type {PermissionName} from "@/api/types.gen";
import {PROTECTED_ROUTE_MARKER} from "./_protectedRouteMarker";

export interface ProtectedRouteProps extends Omit<RouteProps, "element"> {
  element?: ReactElement;
  requiredPermission?: PermissionName | PermissionName[];
  permissionMode?: "any" | "all";
}

// Marker component — never rendered directly.
// ProtectedRoutes converts it into a <Route> with an <AuthGuard> wrapper.
function ProtectedRoute(_props: ProtectedRouteProps): null {
  return null;
}

(ProtectedRoute as unknown as Record<symbol, symbol>)[PROTECTED_ROUTE_MARKER] =
  PROTECTED_ROUTE_MARKER;

export default ProtectedRoute;
