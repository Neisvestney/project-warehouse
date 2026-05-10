import {useContext} from "react";
import ModalContext from "@/contexts/ModalContext";
import type {ModalServiceAPI} from "@/contexts/ModalContext";

/**
 * Imperative modal API for use inside React components.
 *
 * Must be called inside `<ModalProvider>`.
 * For use outside the React tree (API interceptors, QueryCache) use `modalService` from
 * `@/services/modalService` instead.
 *
 * ---
 *
 * **showAlert** — информационное/ошибочное сообщение, resolves on dismiss:
 * ```tsx
 * const { showAlert } = useModal();
 * await showAlert({ title: "Ошибка", message: "Что-то пошло не так", severity: "error" });
 * ```
 *
 * **showConfirm** — диалог подтверждения, resolves `true` / `false`:
 * ```tsx
 * const ok = await showConfirm({
 *   title: "Удалить?",
 *   message: "Это нельзя отменить",
 *   severity: "warning",
 *   confirmText: "Удалить",
 * });
 * if (ok) { ... }
 * ```
 *
 * **showModal** — произвольный компонент. Компонент должен принимать `open` и `onClose`
 * (см. {@link ModalComponentProps}). Resolves со значением переданным в `onClose`:
 * ```tsx
 * const selected = await showModal<string>(MyPickerModal, { items: ["А", "Б"] });
 * // null если пользователь закрыл без выбора
 * ```
 *
 * **Singleton** — второй вызов с тем же `id` игнорируется пока первый открыт:
 * ```ts
 * showAlert({ title: "Нет сети", message: "..." }, { id: "network-error" });
 * ```
 *
 * **Opt-out глобальной модалки** (MutationCache/QueryCache) для конкретного запроса:
 * ```ts
 * useMutation({ meta: { suppressGlobalError: true }, onError: (err) => { ... } })
 * ```
 */
export function useModal(): ModalServiceAPI {
  const ctx = useContext(ModalContext);
  if (!ctx) throw new Error("useModal must be used inside ModalProvider");
  return ctx;
}
