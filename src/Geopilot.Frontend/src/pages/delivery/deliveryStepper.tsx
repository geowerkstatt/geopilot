import { Stack, Typography } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { useCallback, useContext, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { MiddleTruncate } from "../../components/middleTruncate";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext";
import { StepperIcon } from "./stepperIcon";

const StepperStack = styled(Stack)(({ theme }) => ({
  minWidth: 300,
  flex: 0,
  position: "sticky",
  top: "100px",
  zIndex: 10,
  [theme.breakpoints.down("md")]: {
    overflowX: "hidden",
    scrollSnapType: "x",
    scrollbarWidth: "none",
    flex: "0 0 58px",
    alignItems: "flex-start",
    margin: `0 -${pageContentPadding} !important`,
    padding: `0 ${pageContentPadding}`,
  },
}));

const DeliveryStepBox = styled(GeopilotBox, {
  shouldForwardProp: prop => prop !== "open" && prop !== "enabled" && prop !== "error",
})<{
  open: boolean;
  error: boolean;
  enabled: boolean;
}>(({ open, enabled, error, theme }) => ({
  backgroundColor: open ? (error ? theme.palette.error.selected : theme.palette.primary.selected) : "white",
  alignItems: "flex-start",
  cursor: enabled ? "pointer" : "default",
  [theme.breakpoints.down("md")]: {
    scrollSnapAlign: "center",
    width: "100%",
    flexShrink: 0,
  },
}));

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const { steps, lastCompletedStep, activeStep, isLoading, isProcessing, showCompletedOrNextStep } =
    useContext(DeliveryContext);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => lastCompletedStep >= stepIndex;
  const isEnabled = (stepIndex: number) => isCompleted(stepIndex - 1);

  const stepRefs = useRef<Array<HTMLDivElement | null>>([]);

  const showStep = useCallback(
    (stepIndex: number, behavior: ScrollBehavior) => {
      stepRefs.current[stepIndex]?.scrollIntoView({ behavior, block: "nearest", inline: "center" });
    },
    [stepRefs],
  );

  const onStepClick = (index: number) => {
    if (isEnabled(index)) {
      showStep(index, "smooth");
      showCompletedOrNextStep(index);
    }
  };

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
          error={!!step.error}
          enabled={isEnabled(index)}
          onClick={() => onStepClick(index)}>
          <StepperIcon
            index={index}
            open={isOpen(index)}
            enabled={isEnabled(index)}
            completed={isCompleted(index)}
            error={!!step.error}
            isLoading={isLoading || isProcessing}
          />
          <Stack spacing={1} direction={{ xs: "row", md: "column" }} alignItems="baseline" sx={{ minWidth: "0" }}>
            <Typography variant="h4" color={isEnabled(index) ? "textPrimary" : "textSecondary"}>
              {t(step.label)}
            </Typography>
            {step.labelAddition && (
              <Typography
                variant="body2"
                sx={{
                  display: { xs: "none", md: "block" },
                  color: theme => theme.palette.primary.main,
                  maxWidth: "100%",
                }}>
                {t(step.labelAddition)
                  .split("\n")
                  .map((line, idx) => (
                    <MiddleTruncate key={idx} text={line} endLength={10} />
                  ))}
              </Typography>
            )}
            {step.error && (
              <Typography variant="body2" color={isOpen(index) ? "textSecondary" : "error"}>
                {t(step.error)}
              </Typography>
            )}
          </Stack>
        </DeliveryStepBox>
      ))}
    </StepperStack>
  );
};
