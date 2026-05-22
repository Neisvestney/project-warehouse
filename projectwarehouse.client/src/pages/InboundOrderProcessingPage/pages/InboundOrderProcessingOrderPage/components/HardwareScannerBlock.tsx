import React from "react";
import {useHardwareScanner} from "@/hooks/useHardwareScanner.ts";
import {Alert} from "@mui/material";
import QrCodeIcon from "@mui/icons-material/QrCode";
import {Capacitor} from "@capacitor/core";

export interface HardwareScannerBlockProps {
  onNodeScanned: (nodeId: string) => void;
}

function HardwareScannerBlock({onNodeScanned}: HardwareScannerBlockProps) {
  useHardwareScanner((e) => {
    onNodeScanned(e.barcode);
  });

  if (!Capacitor.isNativePlatform()) return null;

  return (
    <Alert severity={"info"} icon={<QrCodeIcon />}>
      Отсканируете код ячейки
    </Alert>
  );
}

export default HardwareScannerBlock;
