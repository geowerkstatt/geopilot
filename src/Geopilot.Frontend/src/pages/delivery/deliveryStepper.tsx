import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { MiddleTruncate } from "../../components/middleTruncate";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { useLocalized } from "../../hooks/useLocalized";
import { SLIDE_TRANSITION_MS } from "./deliveryContentCarousel";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryStepStatus } from "./deliveryInterfaces";
import { DeliveryRestartButton } from "./deliveryRestartButton";
import { StepperIcon } from "./stepperIcon";

const StepperViewport = styled(Box)(({ theme }) => ({
  minWidth: 300,
  flex: 0,
  position: "sticky",
  top: "100px",
  zIndex: 10,
  [theme.breakpoints.down("md")]: {
    overflowX: "hidden",
    scrollSnapType: "x",
    scrollbarWidth: "none",
    touchAction: "pan-y",
    overscrollBehaviorX: "contain",
    flex: "0 0 58px",
    alignItems: "flex-start",
    margin: `0 -${pageContentPadding} !important`,
    padding: `0 ${pageContentPadding}`,
  },
}));

const StepperStack = styled(Stack)({
  position: "relative",
});

const DeliveryStepBox = styled(GeopilotBox, {
  shouldForwardProp: prop => prop !== "open" && prop !== "enabled" && prop !== "status",
})<{
  open: boolean;
  status?: DeliveryStepStatus;
  enabled: boolean;
}>(({ open, enabled, status, theme }) => ({
  backgroundColor: open
    ? status === "error" || status === "deliveryRestriction"
      ? theme.palette.error.selected
      : status === "warning"
        ? theme.palette.warning.selected
        : theme.palette.primary.states.selected
    : theme.palette.background.content,
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
  const localized = useLocalized();
  const { steps, lastCompletedStep, activeStep, isLoading, isProcessing, showCompletedOrNextStep } =
    useContext(DeliveryContext);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => lastCompletedStep >= stepIndex;
  const isEnabled = (stepIndex: number) => isCompleted(stepIndex - 1);

  const onStepClick = (index: number) => {
    if (isEnabled(index)) {
      showCompletedOrNextStep(index);
    }
  };

  return (
    <StepperViewport>
      <StepperStack
        direction={{ xs: "row", md: "column" }}
        style={{
          left: isMobile ? `calc(${activeStep} * (-100% - ${theme.spacing(2)}))` : undefined,
          transition: isMobile ? `left ${SLIDE_TRANSITION_MS}ms ease` : undefined,
        }}
        data-cy="delivery-stepper">
        {Array.from(steps.entries()).map(([key, step], index) => (
          <DeliveryStepBox
            key={key}
            data-cy={`${key}-step`}
            direction="row"
            open={isOpen(index)}
            status={step.state}
            enabled={isEnabled(index) && step.state !== "skipped"}
            onClick={step.state === "skipped" ? undefined : () => onStepClick(index)}>
            <StepperIcon
              index={index}
              open={isOpen(index)}
              enabled={isEnabled(index)}
              completed={isCompleted(index)}
              status={step.state}
              isLoading={isLoading || isProcessing}
            />
            <Stack direction={{ xs: "row", md: "column" }} alignItems="baseline" sx={{ minWidth: "0" }}>
              <Typography variant="h4" color={isEnabled(index) ? "textPrimary" : "textSecondary"} m={0}>
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
              {step.message && (
                <Typography
                  variant="body2"
                  sx={{
                    display: { xs: "none", md: "block" },
                  }}
                  color={
                    isOpen(index) || step.state === "skipped"
                      ? "textSecondary"
                      : step.state === "warning"
                        ? "warning.main"
                        : "error"
                  }>
                  {typeof step.message === "string" ? t(step.message) : localized(step.message)}
                </Typography>
              )}
            </Stack>
          </DeliveryStepBox>
        ))}
        <DeliveryRestartButton
          sx={{ alignSelf: "flex-start", display: { xs: "none", md: "block" } }}
          immediate={lastCompletedStep === steps.size - 1}
        />
      </StepperStack>
    </StepperViewport>
  );
};
