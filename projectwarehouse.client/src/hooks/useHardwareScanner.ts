import {useEffect, useRef} from "react";
import AtolScanner, {type ScanResultEvent} from "@/plugins/atolScanner.ts";
import type {PluginListenerHandle} from "@capacitor/core";

export function useHardwareScanner(onScanResult: (e: ScanResultEvent) => void) {
  const listener = useRef<PluginListenerHandle>(null);
  const onScanResultRef = useRef(onScanResult);

  useEffect(() => {
    onScanResultRef.current = onScanResult;
  }, [onScanResult]);

  useEffect(() => {
    (async () => {
      await listener.current?.remove();
      listener.current = await AtolScanner.addListener("scanResult", (e) => {
        onScanResultRef.current(e);
      });
      await AtolScanner.startListening();
    })();
  }, []);

  useEffect(() => {
    return () => {
      (async () => {
        await listener.current?.remove();
        await AtolScanner.stopListening();
      })();
    };
  }, []);
}
