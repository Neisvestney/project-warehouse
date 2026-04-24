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
import {Link} from "react-router";

const pages = [
  {
    name: "Сканер",
    url: "/scanner",
  },
];
const settings = ["Profile", "Account", "Dashboard", "Logout"];

export interface AppBarProps {}

function MainAppBar({}: AppBarProps) {
  const [anchorElNav, setAnchorElNav] = React.useState<null | HTMLElement>(null);
  const [anchorElUser, setAnchorElUser] = React.useState<null | HTMLElement>(null);

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

  return (
    <AppBar position="static">
      <Container maxWidth="xl">
        <Toolbar disableGutters>
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
              {pages.map((page) => (
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
            {pages.map((page) => (
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
            {/*<Tooltip title="Open settings">*/}
            {/*  <IconButton onClick={handleOpenUserMenu} sx={{p: 0}}>*/}
            {/*    <Avatar alt="Remy Sharp" src="/static/images/avatar/2.jpg" />*/}
            {/*  </IconButton>*/}
            {/*</Tooltip>*/}
            <Menu
              sx={{mt: "45px"}}
              id="menu-appbar"
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
              {settings.map((setting) => (
                <MenuItem key={setting} onClick={handleCloseUserMenu}>
                  <Typography sx={{textAlign: "center"}}>{setting}</Typography>
                </MenuItem>
              ))}
            </Menu>
          </Box>
        </Toolbar>
      </Container>
    </AppBar>
  );
}

export default MainAppBar;
