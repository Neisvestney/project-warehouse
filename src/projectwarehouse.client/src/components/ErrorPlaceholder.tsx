import {Box, Button, Typography} from "@mui/material";
import type {SvgIconProps} from "@mui/material";
import type React from "react";
import {useNavigate} from "react-router";

interface ErrorPlaceholderProps {
  icon: React.ComponentType<SvgIconProps>;
  title: string;
  description?: string;
}

function ErrorPlaceholder({icon: Icon, title, description}: ErrorPlaceholderProps) {
  const navigate = useNavigate();
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "60vh",
        gap: 2,
      }}
    >
      <Icon sx={{fontSize: 64, color: "text.secondary"}} />
      <Typography variant="h5">{title}</Typography>
      {description !== undefined && (
        <Typography variant="body2" color="text.secondary">
          {description}
        </Typography>
      )}
      <Button variant="outlined" onClick={() => navigate("/")}>
        Вернуться назад
      </Button>
    </Box>
  );
}

export default ErrorPlaceholder;
