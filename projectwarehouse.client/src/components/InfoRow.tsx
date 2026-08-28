import React from "react";
import {css, styled, Typography} from "@mui/material";

export interface InfoRowProps {
  label: string;
  value: string | React.ReactNode;
}

function InfoRow({label, value}: InfoRowProps) {
  return (
    <InfoRowUi>
      <InfoRowLabel color="text.secondary">{label}</InfoRowLabel>
      <Typography component="div" sx={{minWidth: 0}}>
        {value}
      </Typography>
    </InfoRowUi>
  );
}

export default InfoRow;

const InfoRowUi = styled("div")(
  ({theme}) => css`
    display: flex;
    align-items: baseline;
    gap: ${theme.spacing(1)};

    ${theme.breakpoints.down("sm")} {
      flex-direction: column;
      align-items: stretch;
      gap: ${theme.spacing(0.25)};
    }
  `,
);

const InfoRowLabel = styled(Typography)(
  ({theme}) => css`
    width: 160px;
    flex-shrink: 0;

    ${theme.breakpoints.down("sm")} {
      width: auto;
      font-size: ${theme.typography.body2.fontSize};
      line-height: ${theme.typography.body2.lineHeight};
      color: ${theme.palette.text.secondary};
    }
  `,
);
