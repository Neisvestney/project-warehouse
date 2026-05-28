import React from "react";
import {Stack, type StackProps, Typography} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";

type FiltersBarProps = Omit<StackProps, "direction" | "spacing"> & {
  children: React.ReactNode;
};

function FiltersBar({children, sx, ...stackProps}: FiltersBarProps) {
  return (
    <Stack
      spacing={2}
      direction="row"
      useFlexGap
      sx={[{alignItems: "center", flexWrap: "wrap"}, ...(Array.isArray(sx) ? sx : [sx])]}
      {...stackProps}
    >
      <Typography color="textSecondary">
        <Stack direction="row" sx={{alignItems: "center"}}>
          <FilterAltIcon />
          Фильтры:
        </Stack>
      </Typography>
      {children}
    </Stack>
  );
}

export default FiltersBar;
