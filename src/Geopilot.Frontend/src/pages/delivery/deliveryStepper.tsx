import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { StepState } from "../../api/apiInterfaces";
import { MiddleTruncate } from "../../components/middleTruncate";
import { StepIcon } from "../../components/stepIcon";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { useLocalized } from "../../hooks/useLocalized";
import { SLIDE_TRANSITION_MS } from "./deliveryContentCarousel";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryRestartButton } from "./deliveryRestartButton";
import { STEPPER_HEIGHT, STICKY_TOP_POSITION_DEFAULT, STICKY_TOP_POSITION_XS } from "./deliveryUtils";

const StepperViewport = styled(Box)(({ theme }) => ({
  minWidth: 300,
  flex: 0,
  position: "sticky",
  top: `${STICKY_TOP_POSITION_DEFAULT}px`,
  zIndex: 10,
  [theme.breakpoints.down("md")]: {
    overflowX: "hidden",
    scrollSnapType: "x",
    scrollbarWidth: "none",
    touchAction: "pan-y",
    overscrollBehaviorX: "contain",
    flex: `0 0 ${STEPPER_HEIGHT}px`,
    alignItems: "flex-start",
    margin: `0 -${pageContentPadding.default} !important`,
    padding: `0 ${pageContentPadding.default}`,
  },
  [theme.breakpoints.down("sm")]: {
    top: `${STICKY_TOP_POSITION_XS}px`,
    margin: `0 -${pageContentPadding.xs} !important`,
    padding: `0 ${pageContentPadding.xs}`,
  },
}));

const StepperStack = styled(Stack)({
  position: "relative",
});

const StepDetailTypography = styled(Typography)(({ theme }) => ({
  display: "none",
  paddingLeft: theme.spacing(5.5),
  maxWidth: "100%",
  [theme.breakpoints.up("md")]: {
    display: "block",
  },
}));
StepDetailTypography.defaultProps = { variant: "body2" };

const DeliveryStepBox = styled(GeopilotBox, {
  shouldForwardProp: prop => prop !== "open" && prop !== "enabled" && prop !== "status",
})<{
  open: boolean;
  status?: StepState;
  enabled: boolean;
}>(({ open, enabled, status, theme }) => ({
  backgroundColor: open
    ? status === StepState.Error || status === StepState.DeliveryRestriction
      ? theme.palette.error.selected
      : status === StepState.Warning
        ? theme.palette.warning.selected
        : theme.palette.primary.states.selected
    : theme.palette.background.content,
  alignItems: "flex-start",
  cursor: enabled ? "pointer" : "default",
  [theme.breakpoints.down("md")]: {
    scrollSnapAlign: "center",
    width: "100%",
    height: `${STEPPER_HEIGHT}px`,
    flexShrink: 0,
  },
}));

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const localized = useLocalized();
  const { steps, lastCompletedStep, activeStep, isLoading, isProcessing, showCompletedOrNextStep } =
    useContext(DeliveryContext);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => lastCompletedStep >= stepIndex;
  const isEnabled = (stepIndex: number) => isCompleted(stepIndex - 1);

  const onStepClick = (index: number) => {
    if (isEnabled(index)) {
      showCompletedOrNextStep(index);
    }
  };

  const stepperStackGap = isXs ? 1 : 2;

  return (
    <StepperViewport>
      <StepperStack
        direction={{ xs: "row", md: "column" }}
        gap={stepperStackGap}
        style={{
          left: isMobile ? `calc(${activeStep} * (-100% - ${theme.spacing(stepperStackGap)}))` : undefined,
          transition: isMobile ? `left ${SLIDE_TRANSITION_MS}ms ease` : undefined,
        }}
        data-cy="delivery-stepper">
        {Array.from(steps.entries()).map(([key, step], index) => {
          const completed = isCompleted(index);
          const enabled = isEnabled(index);
          const isSkipped = step.state === StepState.Skipped;
          const stepState =
            step.state ??
            (completed
              ? StepState.Success
              : enabled && (isLoading || isProcessing)
                ? StepState.Running
                : StepState.Pending);

          return (
            <DeliveryStepBox
              key={key}
              data-cy={`${key}-step`}
              open={isOpen(index)}
              status={step.state}
              enabled={enabled && !isSkipped}
              aria-current={isOpen(index) ? "step" : undefined}
              onClick={isSkipped ? undefined : () => onStepClick(index)}>
              <Stack direction="row" sx={{ alignItems: "center" }}>
                <StepIcon step={index + 1} state={stepState} variant="contained" />
                <Typography variant="h4" color={isEnabled(index) ? "text.primary" : "text.secondary"} m={0}>
                  {t(step.label)}
                </Typography>
              </Stack>
              {step.labelAddition && (
                <StepDetailTypography color="primary.main">
                  {t(step.labelAddition)
                    .split("\n")
                    .map((line, idx) => (
                      <MiddleTruncate key={idx} text={line} endLength={10} />
                    ))}
                </StepDetailTypography>
              )}
              {step.message && (
                <StepDetailTypography
                  color={
                    isOpen(index) || isSkipped
                      ? "text.secondary"
                      : step.state === StepState.Warning
                        ? "warning.main"
                        : "error"
                  }>
                  {typeof step.message === "string" ? t(step.message) : localized(step.message)}
                </StepDetailTypography>
              )}
            </DeliveryStepBox>
          );
        })}
        <DeliveryRestartButton
          sx={{ alignSelf: "flex-start", display: { xs: "none", md: "block" } }}
          immediate={lastCompletedStep === steps.size - 1}
        />
      </StepperStack>
    </StepperViewport>
  );
};
