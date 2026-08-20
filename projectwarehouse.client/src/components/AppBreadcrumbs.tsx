import React from "react";
import {Box, Breadcrumbs, Link, Stack, Typography} from "@mui/material";
import {Link as RouterLink} from "react-router";
import type {AppEntityType} from "@/api/types.gen";
import EntityViewers from "@/components/EntityViewers";

export interface AppBreadcrumbsProps {
  path: AppBreadcrumbsPathPart[];
  right?: React.ReactNode;
  /** Shows who else is looking at the object. The page still has to be subscribed to the stream. */
  viewersOf?: {entityType: AppEntityType; entityId: string | null | undefined};
}

export interface AppBreadcrumbsPathPart {
  name: string;
  link?: string;
}

function AppBreadcrumbs({path, right, viewersOf}: AppBreadcrumbsProps) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center", minHeight: 32}}>
      <Breadcrumbs aria-label="breadcrumb" sx={{minWidth: 0}}>
        {path.map((x, i) =>
          x.link ? (
            <Link key={i} component={RouterLink} underline="hover" color="inherit" to={x.link}>
              {x.name}
            </Link>
          ) : (
            <Typography key={i} sx={{color: "text.primary"}}>
              {x.name}
            </Typography>
          ),
        )}
      </Breadcrumbs>
      {viewersOf && <EntityViewers {...viewersOf} />}
      <Box sx={{flexGrow: 1}} />
      {right}
    </Stack>
  );
}

export default AppBreadcrumbs;
