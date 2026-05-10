import type React from "react";
import type {
  AlertOptions,
  ConfirmOptions,
  ModalComponentProps,
  ModalServiceAPI,
  ShowModalOptions,
} from "@/contexts/ModalContext";

let _ref: ModalServiceAPI | null = null;

/**
 * Module-level singleton that proxies to the mounted ModalProvider.
 * Safe to call from outside the React tree (QueryCache, MutationCache, apiClient interceptors).
 * Returns no-op defaults if ModalProvider is not yet mounted.
 */
export const modalService = {
  setRef(ref: ModalServiceAPI | null) {
    _ref = ref;
  },
  showAlert(options: AlertOptions): Promise<void> {
    return _ref?.showAlert(options) ?? Promise.resolve();
  },
  showConfirm(options: ConfirmOptions): Promise<boolean> {
    return _ref?.showConfirm(options) ?? Promise.resolve(false);
  },
  showModal<T>(
    component: React.ComponentType<ModalComponentProps<T> & Record<string, unknown>>,
    props?: Record<string, unknown>,
    options?: ShowModalOptions,
  ): Promise<T | null> {
    return _ref?.showModal(component, props, options) ?? Promise.resolve(null);
  },
};
