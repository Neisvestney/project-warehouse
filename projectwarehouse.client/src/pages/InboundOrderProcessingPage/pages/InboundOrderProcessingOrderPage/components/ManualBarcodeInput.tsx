import {useRef, useState} from "react";
import {
  CircularProgress,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import SendIcon from "@mui/icons-material/Send";

interface ManualBarcodeInputProps {
  onNodeScanned: (nodeId: string) => void;
  isLookupLoading?: boolean;
  lookupError?: string | null;
}

function ManualBarcodeInput({
  onNodeScanned,
  isLookupLoading,
  lookupError,
}: ManualBarcodeInputProps) {
  const [inputValue, setInputValue] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const handleScan = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return;
    setInputValue("");
    onNodeScanned(trimmed);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") handleScan(inputValue);
  };

  return (
    <Stack spacing={1.5}>
      <Typography variant="subtitle2">Ввод ШК вручную</Typography>
      <TextField
        inputRef={inputRef}
        fullWidth
        label="Введите ШК ячейки"
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={isLookupLoading}
        slotProps={{
          input: {
            endAdornment: (
              <InputAdornment position="end">
                {isLookupLoading ? (
                  <CircularProgress size={20} />
                ) : (
                  <IconButton
                    edge="end"
                    onClick={() => handleScan(inputValue)}
                    disabled={!inputValue.trim()}
                  >
                    <SendIcon />
                  </IconButton>
                )}
              </InputAdornment>
            ),
          },
        }}
      />
      {/*{lookupError && <Alert severity="error">{lookupError}</Alert>}*/}
    </Stack>
  );
}

export default ManualBarcodeInput;
