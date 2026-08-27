import {useEffect, useRef} from "react";
import {useLocation} from "react-router";
import {setCurrentPage} from "@/services/currentPage";
import {logClientEvent} from "@/services/telemetryLogs";

/** Records router transitions: without them an error is not tied to the screen it happened on. */
export default function TelemetryRouteLogger() {
  const location = useLocation();
  const previous = useRef<string | null>(null);

  // During render, not in the effect: the effects of the screen being opened run before those of
  // this component, and a query fired from one of them would otherwise be tagged with the previous
  // page.
  setCurrentPage(location.pathname);

  useEffect(() => {
    const to = location.pathname + location.search;
    if (previous.current === to) return;

    // Where the user went is already in app.page, which every record carries; only where they came
    // from is new information.
    logClientEvent("route.change", previous.current ? {"app.route.from": previous.current} : {});
    previous.current = to;
  }, [location.pathname, location.search]);

  return null;
}
