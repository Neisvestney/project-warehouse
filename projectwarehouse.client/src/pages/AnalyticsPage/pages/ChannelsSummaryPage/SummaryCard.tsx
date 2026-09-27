import type {ReactNode} from "react";
import {Box, LinearProgress, Paper, Stack, Typography} from "@mui/material";

interface SummaryCardProps {
  title: string;
  subtitle?: ReactNode;
  actions?: ReactNode;
  isFetching?: boolean;
  children: ReactNode;
}

function SummaryCard({title, subtitle, actions, isFetching, children}: SummaryCardProps) {
  return (
    <Paper variant="outlined" sx={{display: "flex", flexDirection: "column", minWidth: 0}}>
      <Stack
        direction="row"
        useFlexGap
        sx={{alignItems: "center", flexWrap: "wrap", gap: 1, px: 2, py: 1.5}}
      >
        <Typography variant="subtitle1" sx={{fontWeight: 600}}>
          {title}
        </Typography>
        {subtitle && (
          <Typography variant="body2" color="text.secondary">
            {subtitle}
          </Typography>
        )}
        {actions && (
          <Stack direction="row" spacing={1} sx={{ml: "auto", alignItems: "center"}}>
            {actions}
          </Stack>
        )}
      </Stack>
      <Box sx={{height: 2}}>{isFetching && <LinearProgress sx={{height: 2}} />}</Box>
      <Box sx={{p: 2, pt: 1.5, flexGrow: 1, minWidth: 0}}>{children}</Box>
    </Paper>
  );
}

export default SummaryCard;
