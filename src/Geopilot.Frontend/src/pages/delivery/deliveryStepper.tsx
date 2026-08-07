import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { styled, useMediaQuery, useTheme } from "@mui/system";
import { StepState } from "../../api/apiInterfaces";
import { themePalette } from "../../appPalette.ts";
import { MiddleTruncate } from "../../components/middleTruncate";
import { StepIcon } from "../../components/stepIcon";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { useLocalized } from "../../hooks/useLocalized";
import { SLIDE_TRANSITION_MS } from "./deliveryContentCarousel";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryRestartButton } from "./deliveryRestartButton";
import { STEPPER_HEIGHT, STICKY_TOP_POSITION_DEFAULT, STICKY_TOP_POSITION_XS } from "./deliveryUtils";

const getStateColors = (state: StepState, active: boolean) => {
  switch (state) {
    case StepState.DeliveryRestriction:
    case StepState.Cancelled:
    case StepState.Error:
      return {
        backgroundColor: active ? themePalette.error.background : themePalette.background.content,
        borderColor: active ? themePalette.error.dark : themePalette.error.light,
        messageColor: themePalette.error.contrastText,
      };
    case StepState.Warning:
      return {
        backgroundColor: active ? themePalette.warning.background : themePalette.background.content,
        borderColor: active ? themePalette.warning.dark : themePalette.warning.light,
        messageColor: themePalette.warning.contrastText,
      };
    case StepState.Skipped:
      return {
        backgroundColor: themePalette.background.content,
        borderColor: themePalette.primary.light,
        messageColor: themePalette.text.secondary,
      };
    default:
      return {
        backgroundColor: active ? themePalette.primary.states.selected : themePalette.background.content,
        borderColor: active ? themePalette.primary.dark : themePalette.primary.light,
        messageColor: themePalette.text.primary,
      };
  }
};

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

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
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
          const open = isOpen(index);
          const skipped = step.state === StepState.Skipped;
          const stepState =
            step.state ??
            (completed
              ? StepState.Success
              : enabled && (isLoading || isProcessing)
                ? StepState.Running
                : StepState.Pending);
          const { backgroundColor, borderColor, messageColor } = getStateColors(stepState, open);

          return (
            <GeopilotBox
              key={key}
              data-cy={`${key}-step`}
              aria-current={open ? "step" : undefined}
              onClick={skipped ? undefined : () => onStepClick(index)}
              sx={{
                gap: 1,
                backgroundColor: backgroundColor,
                borderColor: borderColor,
                cursor: enabled ? "pointer" : "default",
                [theme.breakpoints.down("md")]: {
                  scrollSnapAlign: "center",
                  width: "100%",
                  height: `${STEPPER_HEIGHT}px`,
                  flexShrink: 0,
                },
              }}>
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
              {step.messages && step.messages.length > 0 && (
                <StepDetailTypography color={messageColor}>
                  {step.messages.map((message, idx) => (
                    <Box component="span" key={idx} sx={{ display: "block" }}>
                      {typeof message === "string" ? t(message) : localized(message)}
                    </Box>
                  ))}
                </StepDetailTypography>
              )}
            </GeopilotBox>
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
