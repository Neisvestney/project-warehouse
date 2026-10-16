import {useEffect} from "react";
import {useRealtime} from "@/hooks/useRealtime";
import {checkForServiceWorkerUpdate} from "@/services/serviceWorkerUpdate";

export interface ServiceWorkerUpdateWatcherProps {}

/**
 * Renders nothing. The client is published inside the server image, so a frontend release cannot ship
 * without restarting the server and dropping every stream — a reconnect after an outage is the
 * earliest moment the new bundle is known to be on the wire.
 */
function ServiceWorkerUpdateWatcher({}: ServiceWorkerUpdateWatcherProps) {
  const {onReconnectedAfterOutage} = useRealtime();

  useEffect(
    () => onReconnectedAfterOutage(() => void checkForServiceWorkerUpdate()),
    [onReconnectedAfterOutage],
  );

  return null;
}

export default ServiceWorkerUpdateWatcher;
