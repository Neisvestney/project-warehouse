import React, {useEffect} from "react";
import {
  AppBar,
  Container,
  Toolbar,
  Box,
  IconButton,
  Menu,
  Avatar,
  Button,
  MenuItem,
  Tooltip,
  Typography,
  ListSubheader,
  ButtonBase,
} from "@mui/material";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import MenuIcon from "@mui/icons-material/Menu";
import SearchIcon from "@mui/icons-material/Search";
import GlobalSearchModal from "@/components/GlobalSearch/GlobalSearchModal";
import {Link, useNavigate} from "react-router";
import {useAuth} from "@/hooks/useAuth";
import type {PermissionName} from "@/api";
import {getSettingsFirstPageUrl, hasSettingsAccess} from "@/pages/SettingsPage/settingsConfig.tsx";
import {getStorageFirstPageUrl} from "@/pages/StoragePage/storageConfig.tsx";
import {getOperationsFirstPageUrl} from "@/pages/OperationsPage/operationsConfig.tsx";
import {extractErrorMessage} from "@/utils/errorUtils.ts";

const pages: {
  name: string;
  url: string | ((permissions: PermissionName[]) => string);
  requiredPermission?: PermissionName | PermissionName[];
  showIf?: (permissions: PermissionName[]) => boolean;
}[] = [
  {
    name: "Склад",
    url: (p) => `/storage/${getStorageFirstPageUrl(p)}`,
    showIf: (p) => p.includes("warehouses.view") || p.includes("warehouses.view_assigned"),
  },
  {name: "Каталог", url: "/catalog", requiredPermission: "catalog.view"},
  {
    name: "Операции",
    url: (p) => `/operations/${getOperationsFirstPageUrl(p)}`,
  },
  {
    name: "Настройки",
    url: (p) => `/settings/${getSettingsFirstPageUrl(p)}`,
    showIf: hasSettingsAccess,
  },
];

export interface AppBarProps {}

function MainAppBar({}: AppBarProps) {
  const [anchorElNav, setAnchorElNav] = React.useState<null | HTMLElement>(null);
  const [anchorElUser, setAnchorElUser] = React.useState<null | HTMLElement>(null);
  const [searchOpen, setSearchOpen] = React.useState(false);
  const {user, logout, profileIsLoadError, profileLoadError} = useAuth();
  const navigate = useNavigate();

  const handleOpenNavMenu = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorElNav(event.currentTarget);
  };
  const handleOpenUserMenu = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorElUser(event.currentTarget);
  };

  const handleCloseNavMenu = () => {
    setAnchorElNav(null);
  };

  const handleCloseUserMenu = () => {
    setAnchorElUser(null);
  };

  const handleLogout = async () => {
    handleCloseUserMenu();
    await logout();
    navigate("/login", {replace: true});
  };

  const handleNavToProfile = async () => {
    handleCloseUserMenu();
    navigate("/profile");
  };

  useEffect(() => {
    let lastShiftTime = 0;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Shift" && !e.ctrlKey && !e.metaKey && !e.altKey) {
        const now = Date.now();
        if (now - lastShiftTime < 500) {
          e.preventDefault();
          setSearchOpen(true);
          lastShiftTime = 0;
        } else {
          lastShiftTime = now;
        }
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, []);

  const avatarLetter = user?.username?.[0]?.toUpperCase() ?? "?";

  const filteredPages = pages
    .filter((page) => {
      const perms = (user?.permissions ?? []) as PermissionName[];
      const hasPermission =
        !page.requiredPermission ||
        (Array.isArray(page.requiredPermission)
          ? page.requiredPermission.some((p) => perms.includes(p))
          : perms.includes(page.requiredPermission));
      return hasPermission && (!page.showIf || page.showIf(perms));
    })
    .map((page) => ({
      ...page,
      url:
        typeof page.url === "string"
          ? page.url
          : page.url((user?.permissions ?? []) as PermissionName[]),
    }));

  return (
    <>
      <AppBar position="static">
        <Container maxWidth="xl">
          <Toolbar disableGutters variant="dense" sx={{minHeight: 50}}>
            <WarehouseIcon sx={{display: {xs: "none", md: "flex"}, mr: 1}} />
            <Typography
              variant="h6"
              noWrap
              component={Link}
              to={"/"}
              sx={{
                mr: 2,
                display: {xs: "none", md: "flex"},
                fontFamily: "monospace",
                fontWeight: 700,
                letterSpacing: "-0.1rem",
                color: "inherit",
                textDecoration: "none",
              }}
            >
              Warehouse
            </Typography>

            <Box sx={{flexGrow: 1, display: {xs: "flex", md: "none"}}}>
              <IconButton
                size="large"
                aria-label="account of current user"
                aria-controls="menu-appbar"
                aria-haspopup="true"
                onClick={handleOpenNavMenu}
                color="inherit"
              >
                <MenuIcon />
              </IconButton>
              <Menu
                id="menu-appbar"
                anchorEl={anchorElNav}
                anchorOrigin={{
                  vertical: "bottom",
                  horizontal: "left",
                }}
                keepMounted
                transformOrigin={{
                  vertical: "top",
                  horizontal: "left",
                }}
                open={Boolean(anchorElNav)}
                onClose={handleCloseNavMenu}
                sx={{display: {xs: "block", md: "none"}}}
              >
                {filteredPages.map((page) => (
                  <MenuItem
                    onClick={handleCloseNavMenu}
                    component={Link}
                    to={page.url}
                    key={page.url}
                  >
                    <Typography sx={{textAlign: "center"}}>{page.name}</Typography>
                  </MenuItem>
                ))}
              </Menu>
            </Box>
            <WarehouseIcon sx={{display: {xs: "flex", md: "none"}, mr: 1}} />
            <Typography
              variant="h5"
              noWrap
              component={Link}
              to={"/"}
              sx={{
                mr: 2,
                display: {xs: "flex", md: "none"},
                flexGrow: 1,
                fontFamily: "monospace",
                fontWeight: 700,
                letterSpacing: "-0.1rem",
                color: "inherit",
                textDecoration: "none",
              }}
            >
              Warehouse
            </Typography>
            <Box sx={{flexGrow: 1, display: {xs: "none", md: "flex"}}}>
              {filteredPages.map((page) => (
                <Button
                  key={page.url}
                  onClick={handleCloseNavMenu}
                  sx={{color: "white", display: "block"}}
                  component={Link}
                  to={page.url}
                >
                  {page.name}
                </Button>
              ))}
            </Box>
            <Box sx={{display: {xs: "none", md: "flex"}, mx: 2, flexShrink: 0}}>
              <ButtonBase
                onClick={() => setSearchOpen(true)}
                aria-label="Открыть поиск (двойной Shift)"
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 1,
                  px: 1.5,
                  py: 0.5,
                  borderRadius: 1.5,
                  border: "1px solid",
                  borderColor: "rgba(255,255,255,0.4)",
                  color: "rgba(255,255,255,0.7)",
                  width: 220,
                  justifyContent: "flex-start",
                  transition: "border-color 0.2s, color 0.2s",
                  "&:hover": {
                    borderColor: "rgba(255,255,255,0.8)",
                    color: "rgba(255,255,255,1)",
                  },
                }}
              >
                <SearchIcon sx={{fontSize: 18}} />
                <Typography variant="body2" sx={{flex: 1, textAlign: "left"}}>
                  Поиск...
                </Typography>
                <Box
                  sx={{
                    border: "1px solid rgba(255,255,255,0.4)",
                    borderRadius: 0.5,
                    px: 0.5,
                    fontSize: 10,
                    lineHeight: "16px",
                    fontFamily: "monospace",
                    color: "rgba(255,255,255,0.6)",
                  }}
                >
                  ⇧⇧
                </Box>
              </ButtonBase>
            </Box>
            <IconButton
              color="inherit"
              onClick={() => setSearchOpen(true)}
              aria-label="Поиск"
              sx={{display: {xs: "flex", md: "none"}}}
            >
              <SearchIcon />
            </IconButton>
            <Box sx={{flexGrow: 0}}>
              <Tooltip title={user?.username ?? ""}>
                <IconButton onClick={handleOpenUserMenu} sx={{p: 0}}>
                  <Avatar sx={{width: 32, height: 32, fontSize: 14}}>{avatarLetter}</Avatar>
                </IconButton>
              </Tooltip>
              <Menu
                sx={{mt: "45px"}}
                id="menu-user"
                anchorEl={anchorElUser}
                anchorOrigin={{
                  vertical: "top",
                  horizontal: "right",
                }}
                keepMounted
                transformOrigin={{
                  vertical: "top",
                  horizontal: "right",
                }}
                open={Boolean(anchorElUser)}
                onClose={handleCloseUserMenu}
              >
                <MenuItem disabled>
                  <Typography variant="body2" color="text.secondary">
                    {user?.username}
                  </Typography>
                </MenuItem>
                {profileIsLoadError && (
                  <ListSubheader sx={{color: "red"}}>
                    {extractErrorMessage(profileLoadError)}
                  </ListSubheader>
                )}
                <MenuItem onClick={handleNavToProfile}>
                  <Typography sx={{textAlign: "center"}}>Профиль</Typography>
                </MenuItem>
                <MenuItem onClick={handleLogout}>
                  <Typography sx={{textAlign: "center"}}>Выйти</Typography>
                </MenuItem>
              </Menu>
            </Box>
          </Toolbar>
        </Container>
      </AppBar>
      <GlobalSearchModal open={searchOpen} onClose={() => setSearchOpen(false)} />
    </>
  );
}

export default MainAppBar;
