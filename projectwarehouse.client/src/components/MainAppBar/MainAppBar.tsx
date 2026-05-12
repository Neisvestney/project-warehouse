import React from "react";
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
} from "@mui/material";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import MenuIcon from "@mui/icons-material/Menu";
import {Link, useNavigate} from "react-router";
import {useAuth} from "@/hooks/useAuth";
import type {PermissionName} from "@/api";

const pages: {name: string; url: string; requiredPermission?: PermissionName}[] = [
  {
    name: "Сканер",
    url: "/scanner",
  },
  {
    name: "Пользователи",
    url: "/users",
    requiredPermission: "users.view",
  },
];

export interface AppBarProps {}

function MainAppBar({}: AppBarProps) {
  const [anchorElNav, setAnchorElNav] = React.useState<null | HTMLElement>(null);
  const [anchorElUser, setAnchorElUser] = React.useState<null | HTMLElement>(null);
  const {user, logout} = useAuth();
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

  const avatarLetter = user?.username?.[0]?.toUpperCase() ?? "?";

  const filteredPages = pages.filter(
    (page) => !page.requiredPermission || user?.permissions.includes(page.requiredPermission),
  );

  return (
    <AppBar position="static">
      <Container maxWidth="xl">
        <Toolbar disableGutters variant={"dense"}>
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
                sx={{my: 2, color: "white", display: "block"}}
                component={Link}
                to={page.url}
              >
                {page.name}
              </Button>
            ))}
          </Box>
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
              <MenuItem onClick={handleLogout}>
                <Typography sx={{textAlign: "center"}}>Выйти</Typography>
              </MenuItem>
            </Menu>
          </Box>
        </Toolbar>
      </Container>
    </AppBar>
  );
}

export default MainAppBar;
