import React from "react";
import {Navigate, Outlet, Route, Routes, useLocation} from "react-router";
import RouteFallback from "@/components/RouteFallback.tsx";
import type {PermissionName} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import {useHasPermission} from "@/hooks/usePermission";
import AccessDenied from "@/components/AccessDenied";
import type {ProtectedRouteProps} from "./ProtectedRoute";
import {PROTECTED_ROUTE_MARKER} from "./_protectedRouteMarker";

interface AuthGuardProps {
  requiredPermission?: PermissionName | PermissionName[];
  permissionMode?: "any" | "all";
  children?: React.ReactNode;
}

function AuthGuard({requiredPermission, permissionMode = "any", children}: AuthGuardProps) {
  const {isAuthenticated, isLoading} = useAuth();
  const location = useLocation();
  const allowed = useHasPermission(requiredPermission, permissionMode);

  if (isLoading) {
    return <RouteFallback />;
  }

  if (!isAuthenticated) {
    const from = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate to="/login" state={{from}} replace />;
  }

  if (!allowed) {
    return <AccessDenied />;
  }

  return <>{children ?? <Outlet />}</>;
}

function processRoutes(children: React.ReactNode): React.ReactNode {
  return React.Children.map(children, (child) => {
    if (!React.isValidElement(child)) return child;

    if (child.type === React.Fragment) {
      const fragmentProps = child.props as {children?: React.ReactNode};
      return <React.Fragment>{processRoutes(fragmentProps.children)}</React.Fragment>;
    }

    const isProtectedRoute = !!(child.type as unknown as Record<symbol, unknown>)[
      PROTECTED_ROUTE_MARKER
    ];

    if (isProtectedRoute) {
      const {
        element,
        requiredPermission,
        permissionMode,
        children: routeChildren,
        ...rest
      } = child.props as ProtectedRouteProps;

      return (
        <Route
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          {...(rest as any)}
          element={
            <AuthGuard requiredPermission={requiredPermission} permissionMode={permissionMode}>
              {element}
            </AuthGuard>
          }
        >
          {routeChildren ? processRoutes(routeChildren) : undefined}
        </Route>
      );
    }

    const childProps = child.props as {children?: React.ReactNode};
    if (child.type === Route && childProps.children) {
      return React.cloneElement(child as React.ReactElement<{children?: React.ReactNode}>, {
        children: processRoutes(childProps.children),
      });
    }

    return child;
  });
}

export function ProtectedRoutes({children}: {children?: React.ReactNode}) {
  return <Routes>{processRoutes(children)}</Routes>;
}
