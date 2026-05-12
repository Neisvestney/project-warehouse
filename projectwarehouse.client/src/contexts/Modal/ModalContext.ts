import {createContext} from "react";

export interface AlertOptions {
  title: string;
  message: string;
  severity?: "error" | "warning" | "info" | "success";
  confirmText?: string;
}

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  severity?: "warning" | "error" | "info";
}

export interface ShowModalOptions {
  /** Provide to make this modal a singleton — a second call with the same id is ignored while the modal is open */
  id?: string;
}

/**
 * Contract every component passed to `showModal` must satisfy.
 *
 * @example
 * ```tsx
 * interface MyModalProps extends ModalComponentProps<string> {
 *   items: string[];
 * }
 *
 * function MyModal({ open, onClose, items }: MyModalProps) {
 *   return (
 *     <Dialog open={open} onClose={() => onClose(null)}>
 *       {items.map(item => (
 *         <ListItemButton key={item} onClick={() => onClose(item)}>
 *           {item}
 *         </ListItemButton>
 *       ))}
 *     </Dialog>
 *   );
 * }
 *
 * // Usage:
 * const selected = await showModal<string>(MyModal, { items: ["А", "Б"] });
 * ```
 */
export interface ModalComponentProps<T = unknown> {
  open: boolean;
  /** Call with a value to resolve the showModal promise, or null to cancel. */
  onClose: (value: T | null) => void;
}

export interface ModalServiceAPI {
  showAlert: (options: AlertOptions) => Promise<void>;
  showConfirm: (options: ConfirmOptions) => Promise<boolean>;
  showModal: <T>(
    component: React.ComponentType<ModalComponentProps<T> & Record<string, unknown>>,
    props?: Record<string, unknown>,
    options?: ShowModalOptions,
  ) => Promise<T | null>;
}

const ModalContext = createContext<ModalServiceAPI | null>(null);
export default ModalContext;
