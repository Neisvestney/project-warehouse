import React, {useState} from "react";
import {Navigate, useLocation, useNavigate} from "react-router";
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import {useMutation} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {useAuth} from "@/hooks/useAuth";

function LoginPage() {
  const {login, isAuthenticated} = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const {enqueueSnackbar} = useSnackbar();

  const from = (location.state as {from?: string} | null)?.from ?? "/";

  const [usernameHasValue, setUsernameHasValue] = useState(false);
  const [passwordHasValue, setPasswordHasValue] = useState(false);

  const {mutate, isPending} = useMutation({
    mutationFn: ({username, password}: {username: string; password: string}) =>
      login(username, password),
    onSuccess: () => navigate(from, {replace: true}),
    onError: () => enqueueSnackbar("Неверный логин или пароль", {variant: "error"}),
  });

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  const handleSubmit = (e: React.SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const username = (data.get("username") as string | null) ?? "";
    const password = (data.get("password") as string | null) ?? "";
    if (!username || !password) return;
    mutate({username, password});
  };

  const trackValue = (setter: (v: boolean) => void) => ({
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => setter(!!e.target.value),
    onAnimationStart: (e: React.AnimationEvent<HTMLInputElement>) => {
      if (e.animationName === "mui-auto-fill") setter(true);
      if (e.animationName === "mui-auto-fill-cancel") setter(false);
    },
  });

  return (
    <Box sx={{display: "flex", justifyContent: "center", alignItems: "center", minHeight: "100vh"}}>
      <Card sx={{width: 380}} elevation={3}>
        <CardContent sx={{p: 4, "&:last-child": {pb: 4}}}>
          <Box sx={{display: "flex", flexDirection: "column", alignItems: "center", gap: 4}}>
            <Box sx={{display: "flex", alignItems: "center", gap: 1.5}}>
              <WarehouseIcon sx={{fontSize: 32}} color="primary" />
              <Typography variant="h5">Warehouse</Typography>
            </Box>
            <Box
              component="form"
              onSubmit={handleSubmit}
              sx={{display: "flex", flexDirection: "column", gap: 2.5, width: "100%"}}
            >
              <TextField
                label="Логин"
                name="username"
                autoComplete="username"
                autoFocus
                disabled={isPending}
                fullWidth
                slotProps={{htmlInput: trackValue(setUsernameHasValue)}}
              />
              <TextField
                label="Пароль"
                name="password"
                type="password"
                autoComplete="current-password"
                disabled={isPending}
                fullWidth
                slotProps={{htmlInput: trackValue(setPasswordHasValue)}}
              />
              <Button
                type="submit"
                variant="contained"
                size="large"
                fullWidth
                disabled={isPending || !usernameHasValue || !passwordHasValue}
                sx={{mt: 0.5}}
              >
                {isPending ? (
                  <Stack spacing={1} direction={"row"}>
                    <CircularProgress size={22} color="inherit" />
                    <span>Вход...</span>
                  </Stack>
                ) : (
                  "Войти"
                )}
              </Button>
            </Box>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}

export default LoginPage;
