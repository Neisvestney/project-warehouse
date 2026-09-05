import React, {useEffect} from "react";
import {
  AppBar,
  Container,
  Toolbar,
  Box,
  IconButton,
  Menu,
  Button,
  MenuItem,
  Tooltip,
  Typography,
  ListSubheader,
  ButtonBase,
  ListItemIcon,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import SearchIcon from "@mui/icons-material/Search";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import LightModeIcon from "@mui/icons-material/LightMode";
import PersonIcon from "@mui/icons-material/Person";
import LogoutIcon from "@mui/icons-material/Logout";
import GlobalSearchModal from "@/components/GlobalSearch/GlobalSearchModal";
import {Link, useNavigate} from "react-router";
import {useAuth} from "@/hooks/useAuth";
import {useResolvedColorScheme} from "@/hooks/useResolvedColorScheme.ts";
import UserAvatar from "@/components/UserAvatar";
import type {PermissionName} from "@/api/types.gen";
import MainNavDrawer from "./MainNavDrawer.tsx";
import MainNavBrand from "./MainNavBrand.tsx";
import {resolveMainNavPages} from "./mainNavConfig.tsx";
import {extractErrorMessage} from "@/utils/errorUtils.ts";

export const MAIN_APP_BAR_HEIGHT = 50;

export interface AppBarProps {}

function MainAppBar({}: AppBarProps) {
  const [navDrawerOpen, setNavDrawerOpen] = React.useState(false);
  const [anchorElUser, setAnchorElUser] = React.useState<null | HTMLElement>(null);
  const [searchOpen, setSearchOpen] = React.useState(false);
  const {user, logout, profileIsLoadError, profileLoadError} = useAuth();
  const {scheme, setMode} = useResolvedColorScheme();
  const navigate = useNavigate();

  const handleOpenUserMenu = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorElUser(event.currentTarget);
  };

  const handleCloseUserMenu = () => {
    setAnchorElUser(null);
  };

  const handleLogout = async () => {
    handleCloseUserMenu();
    await logout();
    navigate("/login", {replace: true});
  };

  const handleToggleColorMode = () => {
    setMode(scheme === "dark" ? "light" : "dark");
  };

  const handleNavToProfile = async () => {
    handleCloseUserMenu();
    navigate("/profile");
  };

  useEffect(() => {
    let lastShiftTime = 0;
    let dirty = false;

    const reset = () => {
      lastShiftTime = 0;
      dirty = false;
    };

    // Count a press on keyup only, and only when nothing else was pressed between down and up —
    // otherwise Shift+Alt (layout switch) and Shift+letter fire false positives.
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Shift") {
        dirty = e.repeat || e.ctrlKey || e.metaKey || e.altKey;
        return;
      }
      lastShiftTime = 0;
      dirty = true;
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.key !== "Shift") return;
      if (dirty || e.ctrlKey || e.metaKey || e.altKey) {
        reset();
        return;
      }
      const now = Date.now();
      if (now - lastShiftTime < 500) {
        setSearchOpen(true);
        lastShiftTime = 0;
      } else {
        lastShiftTime = now;
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    document.addEventListener("keyup", handleKeyUp);
    window.addEventListener("blur", reset);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("keyup", handleKeyUp);
      window.removeEventListener("blur", reset);
    };
  }, []);

  const filteredPages = resolveMainNavPages((user?.permissions ?? []) as PermissionName[]);

  return (
    <>
      <AppBar position="static">
        <Container maxWidth="xl">
          <Toolbar disableGutters variant="dense" sx={{minHeight: MAIN_APP_BAR_HEIGHT}}>
            <Box sx={{display: {xs: "none", md: "flex"}, mr: 2}}>
              <MainNavBrand />
            </Box>

            <Box sx={{flex: 1, display: {xs: "flex", md: "none"}}}>
              <IconButton
                size="large"
                aria-label="Открыть меню навигации"
                onClick={() => setNavDrawerOpen(true)}
                color="inherit"
              >
                <MenuIcon />
              </IconButton>
            </Box>
            <Box sx={{display: {xs: "flex", md: "none"}, alignItems: "center", minWidth: 0}}>
              <MainNavBrand typographyVariant="h5" />
            </Box>
            <Box sx={{flexGrow: 1, display: {xs: "none", md: "flex"}}}>
              {filteredPages.map((page) => (
                <Button
                  key={page.url}
                  sx={{color: "white", display: "block", mt: "5px"}}
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
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "flex-end",
                flex: {xs: 1, md: "0 0 auto"},
              }}
            >
              <IconButton
                color="inherit"
                onClick={() => setSearchOpen(true)}
                aria-label="Поиск"
                sx={{display: {xs: "flex", md: "none"}}}
              >
                <SearchIcon />
              </IconButton>
              <Box sx={{flexGrow: 0}}>
                <Tooltip title={user?.fullName ?? ""}>
                  <IconButton onClick={handleOpenUserMenu} sx={{p: 0}}>
                    <UserAvatar
                      userId={user?.id}
                      name={user?.fullName}
                      sx={{width: 32, height: 32, fontSize: 14}}
                    />
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
                  slotProps={{paper: {sx: {minWidth: 200}}}}
                >
                  <MenuItem disabled>
                    <Typography variant="body2" color="text.secondary">
                      {user?.username}
                    </Typography>
                  </MenuItem>
                  {profileIsLoadError && (
                    <ListSubheader sx={{color: "error.main"}}>
                      {extractErrorMessage(profileLoadError)}
                    </ListSubheader>
                  )}
                  <MenuItem onClick={handleToggleColorMode}>
                    <ListItemIcon>
                      {scheme === "dark" ? (
                        <LightModeIcon fontSize="small" />
                      ) : (
                        <DarkModeIcon fontSize="small" />
                      )}
                    </ListItemIcon>
                    <Typography>{scheme === "dark" ? "Светлая тема" : "Тёмная тема"}</Typography>
                  </MenuItem>
                  <MenuItem onClick={handleNavToProfile}>
                    <ListItemIcon>
                      <PersonIcon fontSize="small" />
                    </ListItemIcon>
                    <Typography>Профиль</Typography>
                  </MenuItem>
                  <MenuItem onClick={handleLogout}>
                    <ListItemIcon>
                      <LogoutIcon fontSize="small" />
                    </ListItemIcon>
                    <Typography>Выйти</Typography>
                  </MenuItem>
                </Menu>
              </Box>
            </Box>
          </Toolbar>
        </Container>
      </AppBar>
      <MainNavDrawer
        open={navDrawerOpen}
        onClose={() => setNavDrawerOpen(false)}
        pages={filteredPages}
      />
      <GlobalSearchModal open={searchOpen} onClose={() => setSearchOpen(false)} />
    </>
  );
}

export default MainAppBar;
