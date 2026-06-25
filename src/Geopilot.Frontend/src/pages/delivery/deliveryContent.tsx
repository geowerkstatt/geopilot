import { FC, PropsWithChildren } from "react";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { styled } from "@mui/system";
import { FlexBox, FlexRowBox, GeopilotBox } from "../../components/styledComponents";
import { DeliveryRestartButton } from "./deliveryRestartButton";

interface DeliveryContentProps {
  title: string;
  subtitle?: string;
  buttons?: React.ReactNode;
}

const DeliveryContentGrid = styled("div")({
  display: "grid",
  flex: 1,
});

const desktopTopDistance = 100; // header and spacing
const mobileTopDistance = desktopTopDistance + 58 + 16; // top distance + stepper + spacing

// place all elements in the same grid cell and add sticky scrolling
const Overlay = styled("div")(({ theme }) => ({
  gridArea: "1 / 1",
  position: "sticky",
  top: `${desktopTopDistance}px`,
  [theme.breakpoints.down("md")]: {
    top: `${mobileTopDistance}px`,
  },
}));

// hide the scrolled content
const ScrollContentOverlay = styled(Overlay)(({ theme }) => ({
  background: theme.palette.primary.background,
  height: `${desktopTopDistance}px`,
  transform: "translateY(-100%)",
  margin: `0 -${theme.spacing(1)}`,
  [theme.breakpoints.down("md")]: {
    height: `${mobileTopDistance}px`,
  },
}));

// add a fixed top border to the scrolled content
const ContainerTopBorder = styled(Overlay)(({ theme }) => ({
  height: theme.shape.borderRadius,
  border: `1px solid ${theme.palette.primary.light}`,
  borderBottom: "none",
  borderTopLeftRadius: theme.shape.borderRadius,
  borderTopRightRadius: theme.shape.borderRadius,
}));

// hide the border of the scrolled content
const ContainerTopBorderOverlay = styled(Overlay)(({ theme }) => ({
  height: theme.shape.borderRadius,
  borderLeft: `1px solid ${theme.palette.primary.background}`,
  borderRight: `1px solid ${theme.palette.primary.background}`,
}));

const DeliveryContentBox = styled(FlexBox)({
  gridArea: "1 / 1",
  minHeight: "0",
  maxHeight: "100%",
  flex: 1,
});

const MainContentBox = styled(GeopilotBox)({
  overflow: "auto",
});

export const DeliveryContent: FC<PropsWithChildren<DeliveryContentProps>> = ({
  children,
  title,
  subtitle,
  buttons,
}) => {
  const { t } = useTranslation();

  return (
    <DeliveryContentGrid>
      <DeliveryContentBox>
        <MainContentBox>
          <Typography variant="h3" m={0} sx={{ display: { xs: "none", md: "block" } }}>
            {t(title)}
          </Typography>
          {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
          {children}
        </MainContentBox>
        <FlexRowBox sx={{ justifyContent: "space-between" }}>
          <DeliveryRestartButton sx={{ display: { xs: "block", md: "none" } }} />
          <FlexRowBox sx={{ flex: 1, justifyContent: "flex-end" }}>{buttons}</FlexRowBox>
        </FlexRowBox>
      </DeliveryContentBox>
      <ScrollContentOverlay />
      <ContainerTopBorderOverlay />
      <ContainerTopBorder />
    </DeliveryContentGrid>
  );
};
