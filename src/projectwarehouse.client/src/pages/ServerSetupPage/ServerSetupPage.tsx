import React, {useState} from "react";
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import {useSnackbar} from "notistack";
import {
  PREDEFINED_SERVERS,
  SELECTED_SERVER_KEY,
  type ServerConfig,
} from "@/configuration/servers.ts";
import {fetchWithTimeout} from "@/utils/fetchWithTimeout.ts";
import {useBackClosable} from "@/hooks/useBackClosable";

const CUSTOM_SERVERS_KEY = "custom_servers";

function getCustomServers(): ServerConfig[] {
  try {
    return JSON.parse(localStorage.getItem(CUSTOM_SERVERS_KEY) ?? "[]");
  } catch {
    return [];
  }
}

function saveCustomServers(servers: ServerConfig[]) {
  localStorage.setItem(CUSTOM_SERVERS_KEY, JSON.stringify(servers));
}

function ServerSetupPage() {
  const {enqueueSnackbar} = useSnackbar();
  const [customServers, setCustomServers] = useState<ServerConfig[]>(getCustomServers);
  const [connectingUrl, setConnectingUrl] = useState<string | null>(null);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [newName, setNewName] = useState("");
  const [newUrl, setNewUrl] = useState("");
  const [urlError, setUrlError] = useState("");

  const handleConnect = async (server: ServerConfig) => {
    setConnectingUrl(server.url);
    try {
      const healthUrl = server.url.replace(/\/$/, "") + "/health";
      const res = await fetchWithTimeout(healthUrl, 5000);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      localStorage.setItem(SELECTED_SERVER_KEY, server.url);
      window.location.href = server.url;
    } catch (e) {
      console.error(e);
      enqueueSnackbar(`Сервер недоступен: ${server.name}`, {variant: "error"});
      setConnectingUrl(null);
    }
  };

  const handleDeleteCustom = (url: string) => {
    const updated = customServers.filter((s) => s.url !== url);
    setCustomServers(updated);
    saveCustomServers(updated);
  };

  const handleAddServer = () => {
    const trimmedUrl = newUrl.trim().replace(/\/$/, "");
    const trimmedName = newName.trim();
    if (!trimmedName || !trimmedUrl) return;
    try {
      const parsed = new URL(trimmedUrl);
      if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
        setUrlError("Только http:// или https://");
        return;
      }
    } catch {
      setUrlError("Неверный формат URL");
      return;
    }
    const newServer: ServerConfig = {name: trimmedName, url: trimmedUrl};
    const updated = [...customServers, newServer];
    setCustomServers(updated);
    saveCustomServers(updated);
    setNewName("");
    setNewUrl("");
    setUrlError("");
    setAddDialogOpen(false);
  };

  const allServers = [...PREDEFINED_SERVERS, ...customServers];

  useBackClosable(addDialogOpen, () => setAddDialogOpen(false));

  return (
    <Box
      sx={{
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        minHeight: "100vh",
        p: 2,
      }}
    >
      <Card sx={{width: "100%", maxWidth: 480}} elevation={3}>
        <CardContent sx={{p: 3, "&:last-child": {pb: 3}}}>
          <Stack spacing={3}>
            <Box sx={{display: "flex", alignItems: "center", gap: 1.5}}>
              <WarehouseIcon sx={{fontSize: 32}} color="primary" />
              <Typography variant="h5">Выбор сервера</Typography>
            </Box>

            {allServers.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{textAlign: "center", py: 2}}>
                Нет сохранённых серверов. Добавьте первый.
              </Typography>
            ) : (
              <List disablePadding>
                {allServers.map((server, index) => {
                  const isPredefined = index < PREDEFINED_SERVERS.length;
                  const isConnecting = connectingUrl === server.url;
                  return (
                    <React.Fragment key={server.url}>
                      {index > 0 && <Divider />}
                      <ListItem
                        disablePadding
                        secondaryAction={
                          !isPredefined ? (
                            <IconButton
                              edge="end"
                              size="small"
                              disabled={!!connectingUrl}
                              onClick={() => handleDeleteCustom(server.url)}
                            >
                              <DeleteIcon fontSize="small" />
                            </IconButton>
                          ) : null
                        }
                      >
                        <ListItemButton
                          disabled={!!connectingUrl}
                          onClick={() => handleConnect(server)}
                          sx={{pr: isPredefined ? 2 : 6}}
                        >
                          <ListItemText
                            primary={server.name}
                            secondary={server.url}
                            slotProps={{secondary: {sx: {wordBreak: "break-all"}}}}
                          />
                          {isConnecting && (
                            <CircularProgress size={20} sx={{ml: 1, flexShrink: 0}} />
                          )}
                        </ListItemButton>
                      </ListItem>
                    </React.Fragment>
                  );
                })}
              </List>
            )}

            <Button
              variant="outlined"
              startIcon={<AddIcon />}
              onClick={() => setAddDialogOpen(true)}
              disabled={!!connectingUrl}
            >
              Добавить сервер
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <Dialog
        open={addDialogOpen}
        onClose={() => {
          setAddDialogOpen(false);
          setUrlError("");
        }}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>Новый сервер</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{pt: 1}}>
            <TextField
              label="Название"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              fullWidth
              autoFocus
              placeholder="Основной склад"
            />
            <TextField
              label="Адрес"
              value={newUrl}
              onChange={(e) => {
                setNewUrl(e.target.value);
                setUrlError("");
              }}
              fullWidth
              placeholder="http://192.168.1.100:7095"
              error={!!urlError}
              helperText={urlError}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              setAddDialogOpen(false);
              setUrlError("");
            }}
          >
            Отмена
          </Button>
          <Button
            variant="contained"
            onClick={handleAddServer}
            disabled={!newName.trim() || !newUrl.trim()}
          >
            Добавить
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

export default ServerSetupPage;
