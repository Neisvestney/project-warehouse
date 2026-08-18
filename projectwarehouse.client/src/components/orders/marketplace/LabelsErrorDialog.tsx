import {useState} from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  Typography,
} from "@mui/material";
import {formatPostingNumber} from "@/utils/postingNumberUtils";
import type {LabelsError} from "./useDownloadLabels";

interface LabelsErrorDialogProps {
  error: LabelsError | null;
  onClose: () => void;
}

function LabelsErrorDialog({error, onClose}: LabelsErrorDialogProps) {
  // held over the closing transition, otherwise the dialog empties out while it fades
  const [shown, setShown] = useState<LabelsError | null>(error);
  if (error !== null && error !== shown) setShown(error);

  return (
    <Dialog open={error !== null} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Не удалось скачать этикетки</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2">{shown?.message}</Typography>
        {shown !== null && shown.postingNumbers.length > 0 && (
          <List dense disablePadding sx={{mt: 1}}>
            {shown.postingNumbers.map((postingNumber) => (
              <ListItem key={postingNumber} disableGutters sx={{py: 0.25}}>
                <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                  {formatPostingNumber(postingNumber)}
                </Typography>
              </ListItem>
            ))}
          </List>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Понятно</Button>
      </DialogActions>
    </Dialog>
  );
}

export default LabelsErrorDialog;
