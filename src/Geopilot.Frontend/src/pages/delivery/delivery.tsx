import { FC, MutableRefObject, useContext, useEffect } from "react";
import { Stack, Typography } from "@mui/material";
import { styled } from "@mui/system";
import { CenteredContent } from "../../components/styledComponents.ts";
import { useApplicationTitle } from "../../hooks/useApplicationTitle.ts";
import { StepSwipeHandlers, useStepSwipe } from "../../hooks/useStepSwipe.ts";
import { DeliveryContentCarousel, SLIDE_TRANSITION_MS } from "./deliveryContentCarousel.tsx";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepper } from "./deliveryStepper.tsx";

const DeliveryContainer = styled(Stack)(({ theme }) => ({
  flex: 1,
  alignItems: "flex-start",
  [theme.breakpoints.down("md")]: {
    alignItems: "stretch",
  },
}));

interface DeliveryProps {
  stepSwipeRef: MutableRefObject<StepSwipeHandlers | null>;
}

const Delivery: FC<DeliveryProps> = ({ stepSwipeRef }) => {
  const { steps, activeStep, showCompletedOrNextStep } = useContext(DeliveryContext);
  const applicationTitle = useApplicationTitle();

  const swipeHandlers = useStepSwipe({
    activeStep,
    stepCount: steps.size,
    cooldownMs: SLIDE_TRANSITION_MS,
    onNavigate: showCompletedOrNextStep,
  });

  useEffect(() => {
    stepSwipeRef.current = swipeHandlers;
    return () => {
      stepSwipeRef.current = null;
    };
  }, [stepSwipeRef, swipeHandlers]);

  return (
    <CenteredContent data-cy="delivery" maxWidth="1400px">
      {applicationTitle && (
        <Typography
          variant="h1"
          data-cy="delivery-title"
          sx={{ position: "relative", zIndex: 11, transform: "translateZ(0)" }}>
          {applicationTitle}
        </Typography>
      )}
      <DeliveryContainer direction={{ xs: "column", md: "row" }} sx={{ m: { xs: 0, md: 0 } }}>
        <DeliveryStepper />
        <DeliveryContentCarousel />
      </DeliveryContainer>
    </CenteredContent>
  );
};

export default Delivery;
