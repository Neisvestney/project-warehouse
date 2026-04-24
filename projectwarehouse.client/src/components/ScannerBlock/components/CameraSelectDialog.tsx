import React from "react";
import {
  Avatar,
  Dialog,
  DialogTitle,
  List,
  ListItem,
  ListItemAvatar,
  ListItemButton,
  ListItemText,
} from "@mui/material";
import CameraAltIcon from "@mui/icons-material/CameraAlt";
import {blue} from "@mui/material/colors";

export interface CameraSelectDialogProps {
  open: boolean;
  setOpen: (open: boolean) => void;
  devicesOptions: {value: string; label: string}[];
  selectDeviceId: (deviceId: string) => void;
  selectedDeviceId?: string;
}

function CameraSelectDialog({
  open,
  setOpen,
  devicesOptions,
  selectDeviceId,
  selectedDeviceId,
}: CameraSelectDialogProps) {
  const handleClose = () => {
    setOpen(false);
  };

  const handleListItemClick = (deviceId: string) => {
    handleClose();
    selectDeviceId(deviceId);
  };

  return (
    <>
      <Dialog onClose={handleClose} open={open}>
        <DialogTitle>Выберете камеру</DialogTitle>
        <List sx={{pt: 0}}>
          {devicesOptions.map(({label, value}) => (
            <ListItem disablePadding key={value}>
              <ListItemButton
                selected={value == selectedDeviceId}
                onClick={() => handleListItemClick(value)}
              >
                <ListItemAvatar>
                  <Avatar sx={{bgcolor: blue[100], color: blue[600]}}>
                    <CameraAltIcon />
                  </Avatar>
                </ListItemAvatar>
                <ListItemText primary={label} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      </Dialog>
    </>
  );
}

export default CameraSelectDialog;
