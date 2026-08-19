import {Alert} from "@mui/material";
import type {StaleActor} from "@/hooks/useStaleData";

interface EditLockBannerProps {
  heldBy: StaleActor | null;
}

/**
 * A warning, not a guard: fields and the save button stay enabled. The user decides what to do with
 * the information.
 */
function EditLockBanner({heldBy}: EditLockBannerProps) {
  if (!heldBy) return null;

  return (
    <Alert severity="warning">
      {heldBy.userName} сейчас редактирует этот объект — ваши изменения могут перезаписать его
      правки
    </Alert>
  );
}

export default EditLockBanner;
