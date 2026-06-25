import { FC, TransitionEvent, useContext, useEffect, useState } from "react";
import { Box, Stack } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStep } from "./deliveryInterfaces.tsx";

export const SLIDE_TRANSITION_MS = 300;

const CarouselViewport = styled(Box)({
  position: "relative",
  flex: 1,
  minWidth: 0,
  overflowX: "clip",
  overflowY: "visible",
});

const CarouselTrack = styled(Stack)(({ theme }) => ({
  position: "relative",
  width: "100%",
  alignItems: "flex-start",
  flexDirection: "row",
  gap: theme.spacing(2),
}));

const CarouselSlide = styled(Stack)({
  flex: "0 0 100%",
  minWidth: 0,
});

export const DeliveryContentCarousel: FC = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const { steps, activeStep, lastCompletedStep } = useContext(DeliveryContext);
  const stepEntries = Array.from(steps.entries());

  const renderContent = (step: DeliveryStep, index: number) => step.content(lastCompletedStep >= index);

  const [liveIndices, setLiveIndices] = useState<Set<number>>(() => new Set([activeStep]));
  const [animate, setAnimate] = useState(false);

  useEffect(() => {
    // Desktop shows a single step without sliding, so keep only the active step mounted.
    if (!isMobile) {
      setLiveIndices(new Set([activeStep]));
      setAnimate(false);
      return;
    }
    // Mark the destination step live while keeping any steps already mounted for the
    // in-flight animation, so the outgoing box slides out and the incoming box slides
    // in with real content. handleTransitionEnd prunes back to the active step once the
    // slide settles, leaving the neighbouring boxes unmounted (no peek) at rest.
    setLiveIndices(prev => new Set(prev).add(activeStep));
    setAnimate(true);
  }, [activeStep, isMobile]);

  const handleTransitionEnd = (event: TransitionEvent<HTMLDivElement>) => {
    if (event.propertyName !== "left" || event.target !== event.currentTarget) return;
    setLiveIndices(new Set([activeStep]));
  };

  // Both layouts render the same viewport/track/slide component types so that crossing the
  // md breakpoint keeps the active slide (matched by key) mounted, preserving its state such
  // as the tree selection. Mobile slides the whole track between steps; desktop only mounts
  // the active step and drops the carousel-specific layout (translate, clipping) so it lays
  // out exactly as a single static panel.
  return (
    <CarouselViewport data-cy="delivery-content-carousel" sx={{ overflowX: isMobile ? "clip" : "visible" }}>
      <CarouselTrack
        onTransitionEnd={handleTransitionEnd}
        style={{
          left: isMobile ? `calc(${activeStep} * (-100% - ${theme.spacing(2)}))` : 0,
          transition: isMobile && animate ? `left ${SLIDE_TRANSITION_MS}ms ease` : "none",
        }}>
        {stepEntries.map(([key, step], index) => {
          if (!isMobile && index !== activeStep) return null;
          return (
            <CarouselSlide key={key} aria-hidden={index !== activeStep}>
              {liveIndices.has(index) ? renderContent(step, index) : null}
            </CarouselSlide>
          );
        })}
      </CarouselTrack>
    </CarouselViewport>
  );
};
