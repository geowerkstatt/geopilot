import { Stack, Typography } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { useCallback, useContext, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext";
import { StepperIcon } from "./stepperIcon";

const StepperStack = styled(Stack)(({ theme }) => ({
  minWidth: 300,
  flex: 0,
  [theme.breakpoints.down("md")]: {
    overflowX: "auto",
    scrollSnapType: "x",
    scrollbarWidth: "none",
    minHeight: "max-content",
    alignItems: "flex-start",
    margin: `0 -${pageContentPadding} !important`,
    padding: `0 ${pageContentPadding}`,
  },
}));

const DeliveryStepBox = styled(GeopilotBox, { shouldForwardProp: prop => prop !== "open" })<{ open: boolean }>(
  ({ open, theme }) => ({
    backgroundColor: open ? theme.palette.primary.selected : "white",
    alignItems: "flex-start",
    boxSizing: "border-box",
    [theme.breakpoints.down("md")]: {
      scrollSnapAlign: "center",
      width: "100%",
      flexShrink: 0,
    },
  }),
);

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const { steps, activeStep, isLoading, isProcessing } = useContext(DeliveryContext);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => activeStep > stepIndex;

  const stepRefs = useRef<Array<HTMLDivElement | null>>([]);

  const showStep = useCallback(
    (stepIndex: number, behavior: ScrollBehavior) => {
      stepRefs.current[stepIndex]?.scrollIntoView({ behavior, block: "nearest", inline: "center" });
    },
    [stepRefs],
  );

  useEffect(() => {
    showStep(activeStep, "smooth");
  }, [activeStep, showStep]);

  useEffect(() => {
    showStep(activeStep, "instant");
    // Instantly scroll to the current step when switching to mobile view (missing dependency on activeStep)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showStep, isMobile]);

  return (
    <StepperStack spacing={2} direction={{ xs: "row", md: "column" }} data-cy="delivery-stepper">
      {Array.from(steps.entries()).map(([key, step], index) => (
        <DeliveryStepBox
          key={key}
          ref={el => (stepRefs.current[index] = el)}
          data-cy={`${key}-step`}
          direction="row"
          open={isOpen(index)}
          onClick={() => showStep(index, "smooth")}>
          <StepperIcon
            index={index}
            active={isOpen(index)}
            completed={isCompleted(index)}
            error={!!step.error}
            isLoading={isOpen(index) && (isLoading || isProcessing)}
          />
          <Stack spacing={1}>
            <Typography variant="h3" color={isOpen(index) || isCompleted(index) ? "textPrimary" : "textSecondary"}>
              {t(step.label)}
            </Typography>
            {step.labelAddition && (
              <Typography
                variant="body2"
                sx={{ display: { xs: "none", md: "block" }, color: theme => theme.palette.primary.main }}>
                {t(step.labelAddition)}
              </Typography>
            )}
            {step.error && (
              <Typography variant="body2" color="error">
                {t(step.error)}
              </Typography>
            )}
          </Stack>
        </DeliveryStepBox>
      ))}
    </StepperStack>
  );
};
