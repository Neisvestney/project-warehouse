import {lazy, Suspense, useRef, useState} from "react";
import type {TextFieldProps} from "@mui/material";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogTitle,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
} from "@mui/material";
import CameraAltIcon from "@mui/icons-material/CameraAlt";
import type {Control, FieldValues, Path, RegisterOptions} from "react-hook-form";
import {Controller} from "react-hook-form";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useHardwareScanner} from "@/hooks/useHardwareScanner";
import {isCameraApiSupported} from "@/utils/camera/cameraUtils";

const ScannerBlock = lazy(() => import("@/components/ScannerBlock/ScannerBlock"));

interface TextFieldWithScannerProps<T extends FieldValues> extends Omit<
  TextFieldProps,
  "name" | "error" | "helperText" | "slotProps"
> {
  control: Control<T>;
  name: Path<T>;
  rules?: RegisterOptions<T, Path<T>>;
  helperText?: string;
  /** Turns a raw scan into the field value; the trimmed scan by default. */
  parseScan?: (raw: string) => string;
}

const trimScan = (raw: string) => raw.trim();

export function TextFieldWithScanner<T extends FieldValues>({
  control,
  name,
  rules,
  helperText,
  parseScan = trimScan,
  disabled,
  ...rest
}: TextFieldWithScannerProps<T>) {
  const [cameraOpen, setCameraOpen] = useState(false);
  const [focused, setFocused] = useState(false);

  return (
    <Controller
      control={control}
      name={name}
      rules={rules}
      render={({field, fieldState}) => {
        const applyScan = (raw: string) => field.onChange(parseScan(raw));
        return (
          <>
            <TextField
              {...rest}
              {...field}
              disabled={disabled}
              error={!!fieldState.error}
              helperText={fieldState.error?.message ?? helperText}
              onFocus={() => setFocused(true)}
              onBlur={() => {
                setFocused(false);
                field.onBlur();
              }}
              slotProps={{
                input: {
                  endAdornment: isCameraApiSupported() && (
                    <InputAdornment position="end">
                      <IconButton
                        edge="end"
                        size="small"
                        disabled={disabled}
                        onClick={() => setCameraOpen(true)}
                        aria-label="Сканировать камерой"
                      >
                        <CameraAltIcon fontSize="small" />
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            {focused && !disabled && <HardwareScanListener onScan={applyScan} />}
            <ScanDialog
              open={cameraOpen}
              onClose={() => setCameraOpen(false)}
              onScanned={(raw) => {
                applyScan(raw);
                setCameraOpen(false);
              }}
            />
          </>
        );
      }}
    />
  );
}

// Mounted only while the field is focused, so a hardware scan lands in the field the user is on.
function HardwareScanListener({onScan}: {onScan: (raw: string) => void}) {
  useHardwareScanner(({barcode}) => onScan(barcode));
  return null;
}

interface ScanDialogProps {
  open: boolean;
  onClose: () => void;
  onScanned: (raw: string) => void;
}

function ScanDialog({open, onClose, onScanned}: ScanDialogProps) {
  useBackClosable(open, onClose);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{paper: {sx: {pointerEvents: open ? undefined : "none"}}}}
    >
      <DialogTitle>Сканирование</DialogTitle>
      <ScanDialogContent onScanned={onScanned} />
      <DialogActions>
        <Button onClick={onClose}>Отмена</Button>
      </DialogActions>
    </Dialog>
  );
}

function ScanDialogContent({onScanned}: {onScanned: (raw: string) => void}) {
  // ScannerBlock keeps reporting the same code every frame until it unmounts
  const handledRef = useRef(false);

  return (
    <Box sx={{height: {xs: "60vh", sm: 360}}}>
      <Suspense
        fallback={
          <Stack sx={{height: "100%", alignItems: "center", justifyContent: "center"}}>
            <CircularProgress size={32} />
          </Stack>
        }
      >
        <ScannerBlock
          onScanned={(raw) => {
            if (handledRef.current) return;
            handledRef.current = true;
            onScanned(raw);
          }}
        />
      </Suspense>
    </Box>
  );
}
