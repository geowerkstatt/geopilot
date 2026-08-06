import { FC, PropsWithChildren, ReactNode, useCallback, useContext, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, styled, Typography } from "@mui/material";
import { GeopilotBox } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryRestartButton } from "./deliveryRestartButton";
import { mobileTopDistance, smallTopDistance, STICKY_TOP_POSITION_DEFAULT } from "./deliveryUtils";

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

const desktopTopDistance = STICKY_TOP_POSITION_DEFAULT;

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
  const contentRef = useRef<HTMLDivElement | null>(null);
  const topBorderRef = useRef<HTMLDivElement | null>(null);
  const [isTopBorderVisible, setIsTopBorderVisible] = useState(false);

  // The sticky top border fakes the upper edge of a box while its real edge is scrolled
  // out of view. Only show it while a real box actually crosses the border line.
  const updateTopBorderVisibility = useCallback(() => {
    const topBorder = topBorderRef.current;
    const content = contentRef.current;
    if (!topBorder || !content) return;

    const lineY = topBorder.getBoundingClientRect().top;
    const boxes: Element[] = hideBox ? Array.from(content.children) : [content];
    const reachesLine = boxes.some(box => {
      const { top, bottom } = box.getBoundingClientRect();
      return top <= lineY && bottom > lineY;
    });
    setIsTopBorderVisible(reachesLine);
  }, [hideBox]);

  useEffect(() => {
    const content = contentRef.current;
    const observer = new ResizeObserver(updateTopBorderVisibility);
    if (content) observer.observe(content);
    window.addEventListener("scroll", updateTopBorderVisibility, { capture: true, passive: true });
    window.addEventListener("resize", updateTopBorderVisibility);
    return () => {
      observer.disconnect();
      window.removeEventListener("scroll", updateTopBorderVisibility, { capture: true });
      window.removeEventListener("resize", updateTopBorderVisibility);
    };
  }, [updateTopBorderVisibility]);

  useEffect(() => {
    updateTopBorderVisibility();
  });

  return (
    <DeliveryContentGrid>
      <DeliveryContentBox>
        <ContentBox ref={contentRef} sx={{ overflow: "auto" }}>
          <Typography variant="h4" m={0} sx={{ display: { xs: "none", md: hideBox ? "none" : "block" } }}>
            {t(title)}
          </Typography>
          {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
          {children}
        </ContentBox>
        <Stack
          direction={{ xs: "column-reverse", sm: "row" }}
          sx={{ alignItems: { xs: "stretch", sm: "flex-start" }, flexWrap: "wrap", justifyContent: "space-between" }}>
          <DeliveryRestartButton
            sx={{ display: { xs: "block", md: "none" }, alignSelf: { xs: "center", sm: "flex-start" } }}
            immediate={lastCompletedStep === steps.size - 1}
          />
          <Stack
            direction={{ xs: "column-reverse", sm: "row" }}
            sx={{
              alignItems: { xs: "stretch", sm: "center" },
              flexWrap: "wrap",
              flex: { xs: "0 1 auto", sm: 1 },
              justifyContent: "flex-end",
            }}>
            {buttons}
          </Stack>
        </Stack>
      </DeliveryContentBox>
      <ScrollContentOverlay />
      <ContainerTopBorderOverlay sx={{ visibility: isTopBorderVisible ? "visible" : "hidden" }} />
      <ContainerTopBorder ref={topBorderRef} sx={{ visibility: isTopBorderVisible ? "visible" : "hidden" }} />
    </DeliveryContentGrid>
  );
};
