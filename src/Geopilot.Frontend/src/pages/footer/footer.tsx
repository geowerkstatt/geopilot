import { Stack, useMediaQuery, useTheme } from "@mui/material";
import { Button } from "../../components/buttons";
import { useControlledNavigate } from "../../components/controlledNavigate";

const Footer = () => {
  const { navigateTo } = useControlledNavigate();
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <Stack
      direction="row"
      pb={1}
      px={3}
      sx={{
        alignItems: "center",
        justifyContent: "center",
        flexWrap: "wrap",
        marginLeft: { xs: "0", md: marginLeft },
      }}
      className="footer">
      <Button
        size={isXs ? "small" : "medium"}
        variant="text"
        data-cy="home-nav"
        label="geopilot"
        onClick={() => navigateTo("/")}
      />
      <Button
        size={isXs ? "small" : "medium"}
        variant="text"
        data-cy="privacy-policy-nav"
        label="privacyPolicy"
        onClick={() => navigateTo("/privacy-policy")}
      />
      <Button
        size={isXs ? "small" : "medium"}
        variant="text"
        data-cy="imprint-nav"
        label="imprint"
        onClick={() => navigateTo("/imprint")}
      />
      <Button
        size={isXs ? "small" : "medium"}
        variant="text"
        data-cy="about-nav"
        label="about"
        onClick={() => navigateTo("/about")}
      />
    </Stack>
  );
};

export default Footer;
