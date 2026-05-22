import {useState} from "react";
import {Button, Stack} from "@mui/material";
import CameraAltIcon from "@mui/icons-material/CameraAlt";
import CameraAltOutlinedIcon from "@mui/icons-material/CameraAltOutlined";
import ScannerBlock from "@/components/ScannerBlock/ScannerBlock.tsx";

interface CameraScannerBlockProps {
  onNodeScanned: (nodeId: string) => void;
}

function CameraScannerBlock({onNodeScanned}: CameraScannerBlockProps) {
  const [showCamera, setShowCamera] = useState(false);

  const handleScanned = (data: string) => {
    setShowCamera(false);
    onNodeScanned(data);
  };

  return (
    <Stack spacing={1.5}>
      <Button
        variant={showCamera ? "contained" : "outlined"}
        startIcon={showCamera ? <CameraAltIcon /> : <CameraAltOutlinedIcon />}
        onClick={() => setShowCamera((v) => !v)}
        fullWidth
      >
        {showCamera ? "Скрыть камеру" : "Сканировать камерой"}
      </Button>
      {showCamera && <ScannerBlock onScanned={handleScanned} />}
    </Stack>
  );
}

export default CameraScannerBlock;
