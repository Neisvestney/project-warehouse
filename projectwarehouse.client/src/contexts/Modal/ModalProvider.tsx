import React, {useCallback, useEffect, useRef, useState} from "react";
import ModalContext from "./ModalContext";
import type {
  AlertOptions,
  ConfirmOptions,
  ModalComponentProps,
  ModalServiceAPI,
  ShowModalOptions,
} from "./ModalContext";
import AlertModal from "@/components/modals/AlertModal";
import ConfirmModal from "@/components/modals/ConfirmModal";
import {modalService} from "@/services/modalService";

// Stored with type-erased props — each entry may have a different generic T
type AnyModalComponent = React.ComponentType<Record<string, unknown>>;

interface ModalEntry {
  id: string;
  component: AnyModalComponent;
  props: Record<string, unknown>;
  open: boolean;
}

// MUI Dialog default Fade duration + buffer
const CLOSE_ANIMATION_MS = 300;

export function ModalProvider({children}: {children: React.ReactNode}) {
  const [entries, setEntries] = useState<ModalEntry[]>([]);
  const resolversRef = useRef(new Map<string, (value: unknown) => void>());
  // Mirror of entries state for synchronous reads without side effects in updaters
  const entriesRef = useRef<ModalEntry[]>([]);
  // Tracks pending close timeouts for cleanup on unmount
  const timeoutsRef = useRef(new Set<ReturnType<typeof setTimeout>>());

  useEffect(() => {
    entriesRef.current = entries;
  }, [entries]);

  const handleClose = useCallback((id: string, value: unknown) => {
    if (!resolversRef.current.has(id)) return;

    const resolve = resolversRef.current.get(id)!;
    resolversRef.current.delete(id);

    // Resolve the caller's promise immediately — no need to wait for animation
    resolve(value ?? null);

    // Animate the dialog out, then remove the entry
    setEntries((prev) => prev.map((e) => (e.id === id ? {...e, open: false} : e)));

    const tid = setTimeout(() => {
      timeoutsRef.current.delete(tid);
      setEntries((prev) => prev.filter((e) => e.id !== id));
    }, CLOSE_ANIMATION_MS);
    timeoutsRef.current.add(tid);
  }, []);

  const addModal = useCallback(
    <T, P extends Record<string, unknown> = Record<string, unknown>>(
      component: React.ComponentType<ModalComponentProps<T> & P>,
      props: P,
      options?: ShowModalOptions,
    ): Promise<T | null> => {
      const id = options?.id ?? crypto.randomUUID();

      // Singleton: check current entries via ref (no side effects inside updater)
      if (options?.id && entriesRef.current.some((e) => e.id === id)) {
        return Promise.resolve(null);
      }

      return new Promise<T | null>((resolve) => {
        // Set resolver before state update so updater stays pure for StrictMode
        resolversRef.current.set(id, resolve as (v: unknown) => void);
        setEntries((prev) => [
          ...prev,
          {id, component: component as unknown as AnyModalComponent, props, open: true},
        ]);
      });
    },
    [],
  );

  const showAlert = useCallback(
    (options: AlertOptions): Promise<void> =>
      addModal(
        AlertModal as unknown as React.ComponentType<
          ModalComponentProps<void> & Record<string, unknown>
        >,
        options as unknown as Record<string, unknown>,
      ).then(() => undefined),
    [addModal],
  );

  const showConfirm = useCallback(
    (options: ConfirmOptions): Promise<boolean> =>
      addModal<boolean>(
        ConfirmModal as unknown as React.ComponentType<
          ModalComponentProps<boolean> & Record<string, unknown>
        >,
        options as unknown as Record<string, unknown>,
      ).then((v) => v ?? false),
    [addModal],
  );

  const showModal = useCallback(
    <T, P extends Record<string, unknown> = Record<never, never>>(
      component: React.ComponentType<ModalComponentProps<T> & P>,
      props?: P,
      options?: ShowModalOptions,
    ): Promise<T | null> => addModal<T, P>(component, (props ?? {}) as P, options),
    [addModal],
  );

  useEffect(() => {
    const api: ModalServiceAPI = {showAlert, showConfirm, showModal};
    modalService.setRef(api);
    return () => modalService.setRef(null);
  }, [showAlert, showConfirm, showModal]);

  useEffect(() => {
    const resolvers = resolversRef.current;
    const timeouts = timeoutsRef.current;
    return () => {
      resolvers.forEach((resolve) => resolve(null));
      timeouts.forEach(clearTimeout);
    };
  }, []);

  return (
    <ModalContext.Provider value={{showAlert, showConfirm, showModal}}>
      {children}
      {entries.map((entry) => {
        const C = entry.component;
        return (
          <C
            key={entry.id}
            {...entry.props}
            open={entry.open}
            onClose={(value: unknown) => handleClose(entry.id, value)}
          />
        );
      })}
    </ModalContext.Provider>
  );
}
