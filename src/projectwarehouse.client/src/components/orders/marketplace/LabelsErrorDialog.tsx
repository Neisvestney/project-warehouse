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
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {formatPostingNumber} from "@/utils/postingNumberUtils";
import type {LabelsError} from "./useDownloadLabels";

interface LabelsErrorDialogProps {
  error: LabelsError | null;
  onClose: () => void;
}

function LabelsErrorDialog({error, onClose}: LabelsErrorDialogProps) {
  const [shown, releaseShown] = useRetainedValue(error);

  useBackClosable(error !== null, onClose);

  return (
    <Dialog
      open={error !== null}
      onClose={onClose}
      fullWidth
      maxWidth="xs"
      slotProps={{transition: {onExited: releaseShown}}}
    >
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
