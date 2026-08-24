import { Link as RouterLink } from "react-router-dom";
import { Stack, useMediaQuery, useTheme } from "@mui/material";
import { Button } from "../../components/buttons";
import { useControlledLinkClick } from "../../components/controlledNavigate";

const Footer = () => {
  const linkClick = useControlledLinkClick();
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <Stack sx={{ paddingLeft: "calc(100vw - 100%)" }}>
      <Stack
        direction="row"
        className="footer"
        sx={{
          pb: 1,
          px: 3,
          alignItems: "center",
          justifyContent: "center",
          flexWrap: "wrap",
          marginLeft: { xs: "0", md: marginLeft },
        }}>
        <Button
          size={isXs ? "small" : "medium"}
          variant="text"
          data-cy="home-nav"
          label="geopilot"
          component={RouterLink}
          to="/"
          onClick={linkClick("/")}
        />
        <Button
          size={isXs ? "small" : "medium"}
          variant="text"
          data-cy="privacy-policy-nav"
          label="privacyPolicy"
          component={RouterLink}
          to="/privacy-policy"
          onClick={linkClick("/privacy-policy")}
        />
        <Button
          size={isXs ? "small" : "medium"}
          variant="text"
          data-cy="imprint-nav"
          label="imprint"
          component={RouterLink}
          to="/imprint"
          onClick={linkClick("/imprint")}
        />
        <Button
          size={isXs ? "small" : "medium"}
          variant="text"
          data-cy="about-nav"
          label="about"
          component={RouterLink}
          to="/about"
          onClick={linkClick("/about")}
        />
      </Stack>
    </Stack>
  );
};

export default Footer;
