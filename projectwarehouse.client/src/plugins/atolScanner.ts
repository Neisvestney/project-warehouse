import {registerPlugin} from "@capacitor/core";
import type {PluginListenerHandle} from "@capacitor/core";

export interface ScanResultEvent {
  barcode: string;
}

export interface AtolScannerPlugin {
  startListening(): Promise<void>;
  stopListening(): Promise<void>;
  addListener(
    eventName: "scanResult",
    listenerFunc: (data: ScanResultEvent) => void,
  ): Promise<PluginListenerHandle>;
}

const AtolScanner = registerPlugin<AtolScannerPlugin>("AtolScanner");

export default AtolScanner;
