import React from "react";
import {Box, css, Stack, styled, Typography} from "@mui/material";

export interface PageGenericHeaderProps {
  title: React.ReactNode;
  children?: React.ReactNode;
  right?: React.ReactNode;
}

function PageGenericHeader({title, children, right}: PageGenericHeaderProps) {
  return (
    <PageGenericHeaderUi>
      <Typography
        variant="h5"
        sx={{
          flexBasis: 300,
          flexShrink: 1,
        }}
      >
        {title}
      </Typography>
      <Box sx={{flexGrow: 1, flexShrink: 0}}>
        <MiddleContentWrapper>{children ?? <span></span>}</MiddleContentWrapper>
      </Box>
      {right && (
        <Stack spacing={1} direction={"row"} sx={{flexShrink: 99}}>
          {right}
        </Stack>
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
