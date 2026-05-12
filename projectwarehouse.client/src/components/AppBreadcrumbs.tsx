import React from "react";
import {Breadcrumbs, Link, Typography} from "@mui/material";
import {Link as RouterLink} from "react-router";

export interface AppBreadcrumbsProps {
  path: AppBreadcrumbsPathPart[];
}

export interface AppBreadcrumbsPathPart {
  name: string;
  link?: string;
}

function AppBreadcrumbs({path}: AppBreadcrumbsProps) {
  return (
    <Breadcrumbs aria-label="breadcrumb">
      {path.map((x, i) =>
        x.link ? (
          <Link component={RouterLink} underline="hover" color="inherit" to={x.link}>
            {x.name}
          </Link>
        ) : (
          <Typography sx={{color: "text.primary"}}>{x.name}</Typography>
        ),
      )}
    </Breadcrumbs>
  );
}

export default AppBreadcrumbs;
