import {useState} from "react";
import {Button, Stack, Typography} from "@mui/material";
import SelectLocationModal from "./SelectLocationModal";
import type {SelectedLocation} from "./SelectLocationModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface LocationFieldProps {
  label: string;
  value: SelectedLocation | null;
  onChange: (location: SelectedLocation) => void;
  disabled?: boolean;
}

function LocationField({label, value, onChange, disabled}: LocationFieldProps) {
  const [open, setOpen] = useState(false);

  const displayText = value
    ? `${value.warehouseName} / ${formatStoragePlaceNodeName(value.nodePath)}`
    : "Не выбрано";

  return (
    <>
      <Stack spacing={0.5}>
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <Typography
            variant="body2"
            sx={{
              flexGrow: 1,
              color: value ? "text.primary" : "text.disabled",
              fontStyle: value ? "normal" : "italic",
            }}
          >
            {displayText}
          </Typography>
          <Button variant="outlined" size="small" onClick={() => setOpen(true)} disabled={disabled}>
            Выбрать
          </Button>
        </Stack>
      </Stack>

      <SelectLocationModal
        open={open}
        onClose={() => setOpen(false)}
        onSelect={(loc) => {
          onChange(loc);
          setOpen(false);
        }}
      />
    </>
  );
}

export default LocationField;
