import { FC, PropsWithChildren, ReactNode, useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, styled, Typography } from "@mui/material";
import { GeopilotBox } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryRestartButton } from "./deliveryRestartButton";

interface DeliveryContentProps {
  title: string;
  subtitle?: string;
  buttons?: ReactNode;
  hideBox?: boolean;
}

const DeliveryContentGrid = styled(Box)({
  display: "grid",
  flex: 1,
});

const APP_HEADER_HEIGHT = 60;
const STEPPER_HEIGHT = 58;

export const STICKY_TOP_POSITION_DEFAULT = APP_HEADER_HEIGHT + 40;
export const STICKY_TOP_POSITION_XS = APP_HEADER_HEIGHT + 8;

const desktopTopDistance = STICKY_TOP_POSITION_DEFAULT;
const mobileTopDistance = STICKY_TOP_POSITION_DEFAULT + STEPPER_HEIGHT + 16; // top distance + stepper + spacing
const smallTopDistance = STICKY_TOP_POSITION_XS + STEPPER_HEIGHT + 8; // small top distance + stepper + spacing

// place all elements in the same grid cell and add sticky scrolling
const Overlay = styled(Box)(({ theme }) => ({
  gridArea: "1 / 1",
  position: "sticky",
  top: `${desktopTopDistance}px`,
  [theme.breakpoints.down("md")]: {
    top: `${mobileTopDistance}px`,
  },
  [theme.breakpoints.down("sm")]: {
    top: `${smallTopDistance}px`,
  },
}));

// hide the scrolled content
const ScrollContentOverlay = styled(Overlay)(({ theme }) => ({
  background: theme.palette.background.base,
  height: `${desktopTopDistance}px`,
  transform: "translateY(-100%)",
  margin: `0 -${theme.spacing(1)}`,
  zIndex: 7,
  [theme.breakpoints.down("md")]: {
    height: `${mobileTopDistance}px`,
  },
  [theme.breakpoints.down("sm")]: {
    height: `${smallTopDistance}px`,
  },
}));

// add a fixed top border to the scrolled content
const ContainerTopBorder = styled(Overlay)(({ theme }) => ({
  height: theme.radius.default,
  border: `1px solid ${theme.palette.primary.light}`,
  borderBottom: "none",
  borderTopLeftRadius: theme.radius.default,
  borderTopRightRadius: theme.radius.default,
}));

// hide the border of the scrolled content
const ContainerTopBorderOverlay = styled(Overlay)(({ theme }) => ({
  height: theme.radius.default,
  borderLeft: `1px solid ${theme.palette.background.base}`,
  borderRight: `1px solid ${theme.palette.background.base}`,
}));

const DeliveryContentBox = styled(Stack)({
  gridArea: "1 / 1",
  minHeight: "0",
  maxHeight: "100%",
  flex: 1,
});

export const DeliveryContent: FC<PropsWithChildren<DeliveryContentProps>> = ({
  children,
  title,
  subtitle,
  buttons,
  hideBox,
}) => {
  const { t } = useTranslation();
  const { steps, lastCompletedStep } = useContext(DeliveryContext);

  const ContentBox = hideBox ? Box : GeopilotBox;

  return (
    <DeliveryContentGrid>
      <DeliveryContentBox>
        <ContentBox sx={{ overflow: "auto" }}>
          <Typography variant="h3" m={0} sx={{ display: { xs: "none", md: "block" } }}>
            {t(title)}
          </Typography>
          {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
          {children}
        </ContentBox>
        <Stack direction="row" sx={{ alignItems: "flex-start", flexWrap: "wrap", justifyContent: "space-between" }}>
          <DeliveryRestartButton
            sx={{ display: { xs: "block", md: "none" } }}
            immediate={lastCompletedStep === steps.size - 1}
          />
          <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", flex: 1, justifyContent: "flex-end" }}>
            {buttons}
          </Stack>
        </Stack>
      </DeliveryContentBox>
      <ScrollContentOverlay />
      <ContainerTopBorderOverlay />
      <ContainerTopBorder />
    </DeliveryContentGrid>
  );
};
