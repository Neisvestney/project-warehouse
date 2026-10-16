import {useEffect} from "react";
import {checkForServiceWorkerUpdate} from "@/services/serviceWorkerUpdate";

const PERIODIC_CHECK_MS = 30 * 60_000;

/**
 * Fallback for tabs the reconnect trigger never reaches — one outside the realtime provider, or one
 * whose stream survived a release.
 *
 * The interval alone would not do: a hidden tab has its timers throttled, a frozen one runs none, and
 * a machine coming out of suspend collapses every missed period into a single late firing. Becoming
 * visible is the event that actually marks "this tab is being used again", so it checks too — the
 * shared throttle keeps the pair from doubling up.
 */
export function usePeriodicUpdateCheck() {
  useEffect(() => {
    const check = () => void checkForServiceWorkerUpdate();

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") check();
    };

    const timer = window.setInterval(check, PERIODIC_CHECK_MS);
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, []);
}
