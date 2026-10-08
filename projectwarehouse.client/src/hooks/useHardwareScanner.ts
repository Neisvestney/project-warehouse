import {useEffect, useRef} from "react";
import {Capacitor} from "@capacitor/core";
import AtolScanner, {type ScanResultEvent} from "@/plugins/atolScanner.ts";

// startListening/stopListening are global to the plugin: only the first subscriber starts and the last one
// stops, and the calls are chained so a page leaving cannot stop the scanner after the next page started it.
let subscribers = 0;
let queue: Promise<void> = Promise.resolve();
// Only the most recently mounted subscriber gets a scan: an overlay or a focused field takes it over from the page
// underneath instead of both handling it.
const stack: object[] = [];

function enqueue(action: () => Promise<void>) {
  queue = queue.then(action).catch((e) => console.warn("Hardware scanner call failed", e));
}

export function useHardwareScanner(onScanResult: (e: ScanResultEvent) => void) {
  const onScanResultRef = useRef(onScanResult);

  useEffect(() => {
    onScanResultRef.current = onScanResult;
  }, [onScanResult]);

  useEffect(() => {
    if (!Capacitor.isPluginAvailable("AtolScanner")) return;

    // Removal goes through the native bridge, so a scan can still arrive after unmount
    let active = true;
    const token = {};
    stack.push(token);
    const handle = AtolScanner.addListener("scanResult", (e) => {
      if (active && stack[stack.length - 1] === token) onScanResultRef.current(e);
    });
    handle.catch((e) => console.warn("Hardware scanner listener registration failed", e));
    if (subscribers++ === 0) enqueue(() => AtolScanner.startListening());

    return () => {
      active = false;
      stack.splice(stack.indexOf(token), 1);
      handle
        .then((h) => h.remove())
        .catch((e) => console.warn("Hardware scanner listener removal failed", e));
      if (--subscribers === 0) enqueue(() => AtolScanner.stopListening());
    };
  }, []);
}
