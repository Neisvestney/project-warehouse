// Symbol.for creates a global-registry symbol — survives HMR module reloads,
// so identity checks in ProtectedRoutes remain valid after hot-reload.
export const PROTECTED_ROUTE_MARKER = Symbol.for("ProtectedRoute");
