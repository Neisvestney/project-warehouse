import {useEffect} from "react";
import {checkForServiceWorkerUpdate} from "@/services/serviceWorkerUpdate";

const PERIODIC_CHECK_MS = 30 * 60_000;

/** Fallback for tabs the reconnect trigger never reaches — one outside the realtime provider, or one
 *  whose stream survived a release. */
export function usePeriodicUpdateCheck() {
  useEffect(() => {
    const timer = window.setInterval(() => void checkForServiceWorkerUpdate(), PERIODIC_CHECK_MS);
    return () => clearInterval(timer);
  }, []);
}
