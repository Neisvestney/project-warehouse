import React from "react";
import {Stack, Typography} from "@mui/material";

export interface InfoRowProps {
  label: string;
  value: string | React.ReactNode;
}

function InfoRow({label, value}: InfoRowProps) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
      <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
        {label}
      </Typography>
      <Typography component="div">{value}</Typography>
    </Stack>
  );
}

export default InfoRow;
