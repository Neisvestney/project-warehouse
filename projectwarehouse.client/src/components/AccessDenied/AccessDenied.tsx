import {Box, Button, Typography} from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import {useNavigate} from "react-router";

function AccessDenied() {
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
      <LockOutlinedIcon sx={{fontSize: 64, color: "text.secondary"}} />
      <Typography variant="h5" fontWeight={600}>
        Доступ запрещён
      </Typography>
      <Typography variant="body2" color="text.secondary">
        У вас нет прав для просмотра этой страницы
      </Typography>
      <Button variant="outlined" onClick={() => navigate(-1)}>
        Вернуться назад
      </Button>
    </Box>
  );
}

export default AccessDenied;
