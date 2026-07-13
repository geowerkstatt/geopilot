import { FC, PropsWithChildren, ReactNode, useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { styled } from "@mui/system";
import { GeopilotBox } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryRestartButton } from "./deliveryRestartButton";

interface DeliveryContentProps {
  title: string;
  subtitle?: string;
  buttons?: ReactNode;
}

const DeliveryContentGrid = styled(Box)({
  display: "grid",
  flex: 1,
});

const desktopTopDistance = 100; // header and spacing
const mobileTopDistance = desktopTopDistance + 58 + 16; // top distance + stepper + spacing

// place all elements in the same grid cell and add sticky scrolling
const Overlay = styled(Box)(({ theme }) => ({
  gridArea: "1 / 1",
  position: "sticky",
  top: `${desktopTopDistance}px`,
  [theme.breakpoints.down("md")]: {
    top: `${mobileTopDistance}px`,
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
}) => {
  const { t } = useTranslation();
  const { steps, lastCompletedStep } = useContext(DeliveryContext);
  return (
    <DeliveryContentGrid>
      <DeliveryContentBox>
        <GeopilotBox sx={{ overflow: "auto" }}>
          <Typography variant="h3" m={0} sx={{ display: { xs: "none", md: "block" } }}>
            {t(title)}
          </Typography>
          {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
          {children}
        </GeopilotBox>
        <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "space-between" }}>
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
