import React from "react";
import {Box, css, Stack, styled, Typography} from "@mui/material";
import type {AppEntityType} from "@/api/types.gen";
import EntityViewers from "@/components/EntityViewers";

export interface PageGenericHeaderProps {
  title: React.ReactNode;
  children?: React.ReactNode;
  /** Action buttons of the header. Below `md` they stretch to fill the row. */
  actions?: React.ReactNode;
  /** Refresh action. From `md` up it leads the actions group, below `md` it sits in the title row. */
  refresh?: React.ReactNode;
  /** Shows who else is looking at the object. The page still has to be subscribed to the stream. */
  viewersOf?: {entityType: AppEntityType; entityId: string | null | undefined};
}

function PageGenericHeader({title, children, actions, refresh, viewersOf}: PageGenericHeaderProps) {
  return (
    <PageGenericHeaderUi>
      <Stack
        direction="row"
        spacing={1}
        sx={{
          alignItems: "center",
          ...(children
            ? {
                flexBasis: {
                  md: 300,
                },
                flexShrink: 1,
              }
            : {
                flexGrow: 1,
              }),
        }}
      >
        <Typography variant="h5" sx={{minWidth: 0}}>
          {title}
        </Typography>
        {viewersOf && <EntityViewers {...viewersOf} />}
        {refresh && (
          <Box sx={{display: {xs: "flex", md: "none"}, ml: "auto", alignItems: "center"}}>
            {refresh}
          </Box>
        )}
      </Stack>
      {children && (
        <Box sx={{flexGrow: 1, flexShrink: 0}}>
          <MiddleContentWrapper>{children ?? <span></span>}</MiddleContentWrapper>
        </Box>
      )}
      {(actions || refresh) && (
        <ActionsStack
          spacing={1}
          direction={"row"}
          useFlexGap
          sx={{display: actions ? "flex" : {xs: "none", md: "flex"}}}
        >
          {refresh && <Box sx={{display: {xs: "none", md: "flex"}}}>{refresh}</Box>}
          {actions}
        </ActionsStack>
      )}
    </PageGenericHeaderUi>
  );
}

export default PageGenericHeader;

const PageGenericHeaderUi = styled("div")(
  ({theme}) => css`
    display: flex;
    align-items: center;
    // grid-template-columns: minmax(auto, 300px) minmax(100px, 1fr) auto;
    gap: ${theme.spacing(1)};
    ${theme.breakpoints.down("md")} {
      flex-direction: column;
      align-items: stretch;
      & > * {
        flex: 1;
      }
    }
  `,
);

const ActionsStack = styled(Stack)(
  ({theme}) => css`
    flex-shrink: 99;
    align-items: center;
    flex-wrap: wrap;
    justify-content: flex-end;

    ${theme.breakpoints.down("md")} {
      justify-content: flex-end;

      & > * {
        flex: 1 1 auto;
      }

      & > .MuiIconButton-root {
        flex: 0 0 auto;
      }
    }
  `,
);

const MiddleContentWrapper = styled("div")(
  ({theme}) => css`
    max-width: 600px;
    display: flex;
    gap: ${theme.spacing(1)};

    ${theme.breakpoints.down("md")} {
      max-width: unset;
    }

    flex: 1;

    & > * {
      flex: 1;
    }
  `,
);
