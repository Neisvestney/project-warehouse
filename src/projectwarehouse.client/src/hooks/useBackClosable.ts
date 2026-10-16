import {useEffect, useId, useRef} from "react";

const OVERLAY_KEY = "__overlay";

/** The ids of the overlays holding a history entry, outermost first. */
const overlayStack = (state: unknown): string[] => {
  if (!state || typeof state !== "object") return [];
  const held = (state as Record<string, unknown>)[OVERLAY_KEY];
  return Array.isArray(held) ? (held as string[]) : [];
};

const isOverlayEntry = (state: unknown) => overlayStack(state).length > 0;

/**
 * Walks back off the entries a reload froze into the stack — the overlays that held them are gone
 * after a cold start, so the entries would otherwise eat one Back press each before the user
 * actually leaves the page. Each entry is stripped of its marker before being left behind, so a
 * Forward press cannot land on one still claiming to be held. Call before React mounts.
 */
export function dropOverlayHistoryEntries() {
  if (!isOverlayEntry(window.history.state)) return;

  const step = () => {
    const state = window.history.state;
    if (!isOverlayEntry(state)) {
      window.removeEventListener("popstate", step);
      return;
    }

    const next = {...state};
    delete next[OVERLAY_KEY];
    window.history.replaceState(next, "");
    window.history.back();
  };

  // Stacked overlays leave one entry each; every `back()` lands through `popstate`.
  window.addEventListener("popstate", step);
  step();
}

// Overlays whose entry is still on the stack but whose holder has closed, waiting to be walked off.
const closing = new Set<string>();
let unwindScheduled = false;

function unwind() {
  const top = overlayStack(window.history.state).at(-1);
  if (top === undefined || !closing.has(top)) return;

  closing.delete(top);
  const onPop = () => {
    window.removeEventListener("popstate", onPop);
    unwind();
  };
  window.addEventListener("popstate", onPop);
  window.history.back();
}

// Deferred, so overlays closing in the same commit are all marked before the first `back()` goes out.
function scheduleUnwind() {
  if (unwindScheduled) return;
  unwindScheduled = true;
  setTimeout(() => {
    unwindScheduled = false;
    unwind();
  });
}

let sweeperAttached = false;

/**
 * An overlay opening before the unwinder reached the entry of one that just closed pushes over it, and
 * the walk stops at a top it does not own. The stranded entry surfaces again once the overlays above it
 * are gone, so every landing is a chance to resume the walk.
 */
function attachSweeper() {
  if (sweeperAttached) return;
  sweeperAttached = true;
  window.addEventListener("popstate", () => {
    if (closing.size > 0) scheduleUnwind();
  });
}

export interface UseBackClosableOptions {
  /**
   * Back no longer closes the overlay: the popped entry is pushed straight back, so the overlay
   * keeps its place in the stack and only its own controls can close it.
   */
  blockBack?: boolean;
}

/**
 * An open overlay occupies its own history entry, so Back — the hardware button on the handheld
 * included — closes it instead of leaving the page.
 *
 * Every entry carries the ids of all overlays open under it, so nesting stays independent: a
 * `popstate` reaches every listener, but only the overlays no longer named by the landed entry
 * close. Router state is carried over, `idx` included — react-router reads it back on `popstate`,
 * and leaving it untouched keeps the extra entries invisible to the router.
 *
 * Two invariants the callers owe this hook:
 * - links inside the overlay navigate with `replace` (`<Link replace>` /
 *   `navigate(to, {replace: true})`), so the destination takes over the held entry instead of
 *   leaving a duplicate that swallows one Back press;
 * - that navigation is synchronous, which holds for the declarative router used in `main.tsx`.
 *   Under a data router with async loaders `onClose` would run before the entry is replaced, and
 *   the cleanup would back over a navigation still in flight.
 */
export function useBackClosable(
  open: boolean,
  onClose: () => void,
  options?: UseBackClosableOptions,
) {
  const id = useId();
  const onCloseRef = useRef(onClose);
  const blockBackRef = useRef(options?.blockBack ?? false);

  // Both are read through refs only: a dependency here would restart the effect below and give up
  // the held entry on nothing more than a changed callback identity or a flipped flag.
  useEffect(() => {
    onCloseRef.current = onClose;
    blockBackRef.current = options?.blockBack ?? false;
  });

  useEffect(() => {
    if (!open) return;
    attachSweeper();

    if (closing.has(id)) {
      // The unwinder has not reached our entry yet, so it is still ours to reuse.
      closing.delete(id);
    } else {
      const state = window.history.state;
      window.history.pushState({...state, [OVERLAY_KEY]: [...overlayStack(state), id]}, "");
    }

    const handlePop = () => {
      if (overlayStack(window.history.state).includes(id)) return;

      if (blockBackRef.current) {
        // The entry is pushed back exactly as it was taken, so Back is a no-op while the overlay lives.
        const state = window.history.state;
        window.history.pushState({...state, [OVERLAY_KEY]: [...overlayStack(state), id]}, "");
        return;
      }

      onCloseRef.current();
    };
    window.addEventListener("popstate", handlePop);

    return () => {
      window.removeEventListener("popstate", handlePop);
      // Only our own entry is ours to drop — Back or a replace navigation may already have.
      if (!overlayStack(window.history.state).includes(id)) return;

      closing.add(id);
      scheduleUnwind();
    };
  }, [open, id]);
}
