import { FC, PropsWithChildren, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { styled } from "@mui/system";
import { GeopilotBox } from "../../components/styledComponents";
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

// The mask and the content share this grid cell so the mask can sit over the content
// while it scrolls behind the sticky stepper.
const Overlay = styled(Box)({
  gridArea: "1 / 1",
  position: "sticky",
  top: `${mobileTopDistance}px`,
});

// On mobile the stepper sticks below the page header, so content scrolling up would show
// beside it. This opaque band masks that content so it disappears behind the stepper. On
// desktop the stepper sits next to the content, so no mask is needed and the content
// flows all the way up behind the fixed page header.
const ScrollContentOverlay = styled(Overlay)(({ theme }) => ({
  background: theme.palette.background.base,
  height: `${mobileTopDistance}px`,
  transform: "translateY(-100%)",
  margin: `0 -${theme.spacing(1)}`,
  zIndex: 7,
  display: "none",
  [theme.breakpoints.down("md")]: {
    display: "block",
  },
}));

// Draws the fixed top border the mobile content scrolls against, below the sticky
// stepper. Hidden on desktop, where the content flows up behind the page header instead.
const ContainerTopBorder = styled(Overlay)(({ theme }) => ({
  height: theme.shape.borderRadius,
  border: `1px solid ${theme.palette.primary.light}`,
  borderBottom: "none",
  borderTopLeftRadius: theme.shape.borderRadius,
  borderTopRightRadius: theme.shape.borderRadius,
  display: "none",
  [theme.breakpoints.down("md")]: {
    display: "block",
  },
}));

// Hides the content box's own side borders in the top corners so only the fixed top
// border above shows. Mobile only, matching the border it complements.
const ContainerTopBorderOverlay = styled(Overlay)(({ theme }) => ({
  height: theme.shape.borderRadius,
  borderLeft: `1px solid ${theme.palette.background.base}`,
  borderRight: `1px solid ${theme.palette.background.base}`,
  display: "none",
  [theme.breakpoints.down("md")]: {
    display: "block",
  },
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
          <DeliveryRestartButton sx={{ display: { xs: "block", md: "none" } }} />
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
