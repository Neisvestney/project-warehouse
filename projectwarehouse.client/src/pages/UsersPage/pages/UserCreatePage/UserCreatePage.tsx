import {useState} from "react";
import {useNavigate} from "react-router";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  TextField,
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import {Controller, useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {usersCreateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";

type CreateFormValues = {
  username: string;
  password: string;
  email: string;
  firstName: string;
  lastName: string;
};

function UserCreatePage() {
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);

  const form = useForm<CreateFormValues>({
    defaultValues: {username: "", password: "", email: "", firstName: "", lastName: ""},
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...usersCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/users/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      body: {
        username: values.username,
        password: values.password,
        email: values.email || null,
        firstName: values.firstName || null,
        lastName: values.lastName || null,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Пользователи", link: "/users"}, {name: "Создать"}]} />
      <PageGenericHeader title="Создать пользователя" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <FormTextField
              control={form.control}
              name="username"
              label="Логин"
              rules={{required: "Обязательное поле"}}
              autoComplete="username"
              autoFocus
              disabled={mutation.isPending}
              fullWidth
            />
            <Controller
              control={form.control}
              name="password"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <TextField
                  {...field}
                  label="Пароль"
                  type={showPassword ? "text" : "password"}
                  autoComplete="new-password"
                  disabled={mutation.isPending}
                  fullWidth
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            onClick={() => setShowPassword((v) => !v)}
                            edge="end"
                            aria-label={showPassword ? "Скрыть пароль" : "Показать пароль"}
                          >
                            {showPassword ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </InputAdornment>
                      ),
                    },
                  }}
                />
              )}
            />
            <FormTextField
              control={form.control}
              name="email"
              label="Email"
              type="email"
              autoComplete="email"
              disabled={mutation.isPending}
              fullWidth
            />
            <FormTextField
              control={form.control}
              name="firstName"
              label="Имя"
              autoComplete="given-name"
              disabled={mutation.isPending}
              fullWidth
            />
            <FormTextField
              control={form.control}
              name="lastName"
              label="Фамилия"
              autoComplete="family-name"
              disabled={mutation.isPending}
              fullWidth
            />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button onClick={() => navigate("/users")} disabled={mutation.isPending}>
                Отмена
              </Button>
              <Button type="submit" variant="contained" disabled={mutation.isPending}>
                {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Создать"}
              </Button>
            </Stack>
          </Stack>
        </Box>
      </Paper>
    </Stack>
  );
}

export default UserCreatePage;
