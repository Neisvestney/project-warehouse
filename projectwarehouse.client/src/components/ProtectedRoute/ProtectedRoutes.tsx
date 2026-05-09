import React from 'react';
import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router';
import { Box, CircularProgress } from '@mui/material';
import type { PermissionName } from '@/api/types.gen';
import { useAuth } from '@/hooks/useAuth';
import { useHasPermission } from '@/hooks/usePermission';
import AccessDenied from '@/components/AccessDenied/AccessDenied';
import ProtectedRoute, { type ProtectedRouteProps } from './ProtectedRoute';
import { PROTECTED_ROUTE_MARKER } from './_protectedRouteMarker';

interface AuthGuardProps {
  requiredPermission?: PermissionName | PermissionName[];
  permissionMode?: 'any' | 'all';
  children?: React.ReactNode;
}

function AuthGuard({ requiredPermission, permissionMode = 'any', children }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();
  const hasPermission = useHasPermission(
    requiredPermission ?? [],
    permissionMode,
  );

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (requiredPermission && !hasPermission) {
    return <AccessDenied />;
  }

  return <>{children ?? <Outlet />}</>;
}

function processRoutes(children: React.ReactNode): React.ReactNode {
  return React.Children.map(children, (child) => {
    if (!React.isValidElement(child)) return child;

    if (child.type === React.Fragment) {
      return <React.Fragment>{processRoutes(child.props.children)}</React.Fragment>;
    }

    const isProtectedRoute =
      child.type === ProtectedRoute ||
      !!(child.type as Record<symbol, unknown>)[PROTECTED_ROUTE_MARKER];

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
          {...rest}
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

    if (child.type === Route && child.props.children) {
      return React.cloneElement(child as React.ReactElement<{ children?: React.ReactNode }>, {
        children: processRoutes(child.props.children),
      });
    }

    return child;
  });
}

export function ProtectedRoutes({ children }: { children?: React.ReactNode }) {
  return <Routes>{processRoutes(children)}</Routes>;
}
