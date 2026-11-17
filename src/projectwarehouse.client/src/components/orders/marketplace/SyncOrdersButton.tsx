import {useState} from "react";
import {Button} from "@mui/material";
import SyncIcon from "@mui/icons-material/Sync";
import SyncOrdersDialog from "./SyncOrdersDialog";

function SyncOrdersButton({statusesOnly = false}: {statusesOnly?: boolean}) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="outlined" size="small" endIcon={<SyncIcon />} onClick={() => setOpen(true)}>
        {statusesOnly ? "Обновить статусы" : "Синхронизировать заказы"}
      </Button>
      <SyncOrdersDialog open={open} onClose={() => setOpen(false)} statusesOnly={statusesOnly} />
    </>
  );
}

export default SyncOrdersButton;
