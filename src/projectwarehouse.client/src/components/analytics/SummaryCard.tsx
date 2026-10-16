import type {ReactNode} from "react";
import {Box, LinearProgress, Paper, Stack, Tooltip, Typography} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";

interface SummaryCardProps {
  title: string;
  /** How to read or use the card; shown in a tooltip of an icon beside the title. */
  hint?: ReactNode;
  /** The card's headline figures. */
  subtitle?: ReactNode;
  actions?: ReactNode;
  isFetching?: boolean;
  children: ReactNode;
}

function SummaryCard({title, hint, subtitle, actions, isFetching, children}: SummaryCardProps) {
  return (
    <Paper variant="outlined" sx={{display: "flex", flexDirection: "column", minWidth: 0}}>
      <Stack
        direction="row"
        useFlexGap
        sx={{alignItems: "center", flexWrap: "wrap", gap: 1, px: 2, py: 1.5}}
      >
        <Typography variant="subtitle1" sx={{fontWeight: 600}}>
          {title}
          {hint && (
            <Tooltip title={hint}>
              <InfoOutlinedIcon
                sx={{fontSize: "1.1em", verticalAlign: "middle", ml: 0.5, color: "text.secondary"}}
              />
            </Tooltip>
          )}
        </Typography>
        {subtitle && (
          <Typography variant="body2" color="text.secondary">
            {subtitle}
          </Typography>
        )}
        {actions && (
          <Stack
            direction="row"
            useFlexGap
            sx={{
              ml: "auto",
              alignItems: "center",
              flexWrap: "wrap",
              justifyContent: "flex-end",
              gap: 1,
            }}
          >
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
