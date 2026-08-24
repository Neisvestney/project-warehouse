import React from "react";
import {alpha, Box, Stack, type StackProps, Typography} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";

type FiltersBarProps = Omit<StackProps, "direction" | "spacing"> & {
  children: React.ReactNode;
  actions?: React.ReactNode;
};

function FiltersBar({children, actions, sx, ...stackProps}: FiltersBarProps) {
  return (
    <Stack
      spacing={1.5}
      direction="row"
      useFlexGap
      sx={[
        (theme) => ({
          alignItems: "center",
          flexWrap: "wrap",
          px: 2,
          py: 1.5,
          borderRadius: 2,
          border: 1,
          borderColor: "divider",
        }),
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
      {...stackProps}
    >
      {/* minHeight keeps the label centered against size="small" controls even with alignItems: flex-start */}
      <Stack
        direction="row"
        spacing={0.75}
        sx={{alignItems: "center", flexShrink: 0, minHeight: 40}}
      >
        <Box
          sx={(theme) => ({
            display: "flex",
            p: 0.5,
            borderRadius: 1,
            color: "primary.main",
            backgroundColor: alpha(theme.palette.primary.main, 0.1),
          })}
        >
          <FilterAltIcon fontSize="small" />
        </Box>
        <Typography
          color="textSecondary"
          variant="subtitle2"
          sx={{display: {xs: "none", sm: "block"}}}
        >
          Фильтры
        </Typography>
      </Stack>

      {children}

      {actions ? (
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{alignItems: "center", flexWrap: "wrap", ml: "auto"}}
        >
          {actions}
        </Stack>
      ) : null}
    </Stack>
  );
}

export default FiltersBar;
