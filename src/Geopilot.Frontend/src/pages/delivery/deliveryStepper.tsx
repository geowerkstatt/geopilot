import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, styled, Typography } from "@mui/material";
import { useMediaQuery, useTheme } from "@mui/system";
import { DeliveryStepState } from "../../api/apiInterfaces";
import { themePalette } from "../../appPalette.ts";
import { px2rem } from "../../appTheme.ts";
import { MiddleTruncate } from "../../components/middleTruncate";
import { StepIcon } from "../../components/stepIcon";
import { GeopilotBox, pageContentPadding } from "../../components/styledComponents";
import { useLocalized } from "../../hooks/useLocalized";
import { SLIDE_TRANSITION_MS } from "./deliveryContentCarousel";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryRestartButton } from "./deliveryRestartButton";
import { STEPPER_HEIGHT, stepperTopPosition } from "./deliveryUtils";

const getStateColors = (state: DeliveryStepState, active: boolean) => {
  switch (state) {
    case DeliveryStepState.DeliveryRestriction:
    case DeliveryStepState.Error:
      return {
        backgroundColor: active ? themePalette.error.background : themePalette.background.content,
        borderColor: active ? themePalette.error.dark : themePalette.error.light,
        hoverBackgroundColor: themePalette.error.hover,
        hoverBorderColor: themePalette.error.main,
        messageColor: themePalette.error.contrastText,
      };
    case DeliveryStepState.Warning:
      return {
        backgroundColor: active ? themePalette.warning.background : themePalette.background.content,
        borderColor: active ? themePalette.warning.dark : themePalette.warning.light,
        hoverBackgroundColor: themePalette.warning.hover,
        hoverBorderColor: themePalette.warning.main,
        messageColor: themePalette.warning.contrastText,
      };
    case DeliveryStepState.Skipped:
      return {
        backgroundColor: themePalette.background.content,
        borderColor: themePalette.primary.light,
        hoverBackgroundColor: themePalette.primary.states.hover,
        hoverBorderColor: themePalette.primary.main,
        messageColor: themePalette.text.secondary,
      };
    default:
      return {
        backgroundColor: active ? themePalette.primary.states.selected : themePalette.background.content,
        borderColor: active ? themePalette.primary.dark : themePalette.primary.light,
        hoverBackgroundColor: themePalette.primary.states.hover,
        hoverBorderColor: themePalette.primary.main,
        messageColor: themePalette.text.primary,
      };
  }
};

const StepperViewport = styled(Box)(({ theme }) => ({
  minWidth: 300,
  flex: 0,
  position: "sticky",
  top: stepperTopPosition.default,
  zIndex: 10,
  [theme.breakpoints.down("md")]: {
    overflowX: "hidden",
    scrollSnapType: "x",
    scrollbarWidth: "none",
    touchAction: "pan-y",
    overscrollBehaviorX: "contain",
    flex: `0 0 ${STEPPER_HEIGHT}`,
    alignItems: "flex-start",
    margin: `0 -${pageContentPadding.default} !important`,
    padding: `0 ${pageContentPadding.default}`,
  },
  [theme.breakpoints.down("sm")]: {
    top: stepperTopPosition.xs,
    margin: `0 -${pageContentPadding.xs} !important`,
    padding: `0 ${pageContentPadding.xs}`,
  },
}));

const StepperStack = styled(Stack)({
  position: "relative",
});

const StepDetail = styled("span")(({ theme }) => ({
  ...theme.typography.body2,
  display: "none",
  paddingLeft: `calc(${px2rem(28)} + ${theme.spacing(2)})`,
  maxWidth: "100%",
  [theme.breakpoints.up("md")]: {
    display: "block",
  },
}));

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const { steps, lastCompletedStep, activeStep, isLoading, isProcessing, openCompletedOrNextStep, canOpenStep } =
    useContext(DeliveryContext);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => lastCompletedStep >= stepIndex;

  const stepperStackGap = isXs ? 1 : 2;

  return (
    <StepperViewport>
      <StepperStack
        direction={{ xs: "row", md: "column" }}
        spacing={stepperStackGap}
        style={{
          left: isMobile ? `calc(${activeStep} * (-100% - ${theme.spacing(stepperStackGap)}))` : undefined,
          transition: isMobile ? `left ${SLIDE_TRANSITION_MS}ms ease` : undefined,
        }}
        data-cy="delivery-stepper">
        {Array.from(steps.entries()).map(([key, step], index) => {
          const completed = isCompleted(index);
          const enabled = canOpenStep(index);
          const open = isOpen(index);
          const skipped = step.state === DeliveryStepState.Skipped;
          const stepState =
            step.state ??
            (completed
              ? DeliveryStepState.Success
              : enabled && (isLoading || isProcessing)
                ? DeliveryStepState.Running
                : enabled
                  ? DeliveryStepState.Enabled
                  : DeliveryStepState.Pending);
          const { backgroundColor, borderColor, hoverBackgroundColor, hoverBorderColor, messageColor } = getStateColors(
            stepState,
            open,
          );
          const selectable = enabled && !skipped;

          return (
            <GeopilotBox
              key={key}
              component="button"
              type="button"
              disabled={!selectable}
              data-cy={`${key}-step`}
              aria-current={open ? "step" : undefined}
              onClick={() => openCompletedOrNextStep(index)}
              sx={{
                gap: 1,
                textAlign: "left",
                backgroundColor: backgroundColor,
                borderColor: borderColor,
                cursor: selectable ? "pointer" : "default",
                ...(selectable &&
                  !open && {
                    "&:hover": { backgroundColor: hoverBackgroundColor, borderColor: hoverBorderColor },
                  }),
                "&:focus-visible": {
                  outline: "none",
                  borderColor: hoverBorderColor,
                  boxShadow: `inset 0 0 0 1px ${hoverBorderColor}`,
                },
                [theme.breakpoints.down("md")]: {
                  scrollSnapAlign: "center",
                  width: "100%",
                  height: STEPPER_HEIGHT,
                  flexShrink: 0,
                  py: 0,
                  justifyContent: "center",
                },
              }}>
              <Stack component="span" direction="row" sx={{ alignItems: "center" }}>
                <StepIcon step={index + 1} state={stepState} variant="contained" />
                <Typography
                  variant="h4"
                  component="span"
                  sx={{ color: enabled ? "text.primary" : "text.secondary", m: 0 }}>
                  {t(step.label)}
                </Typography>
              </Stack>
              {step.labelAddition && (
                <StepDetail sx={{ color: "primary.main" }}>
                  {step.labelAddition.split("\n").map((line, idx) => (
                    <MiddleTruncate key={idx} text={line} endLength={10} />
                  ))}
                </StepDetail>
              )}
              {step.messages && step.messages.length > 0 && (
                <StepDetail sx={{ color: messageColor }}>
                  {step.messages.map((message, idx) => (
                    <Box component="span" key={idx} sx={{ display: "block" }}>
                      {typeof message === "string" ? t(message) : localized(message)}
                    </Box>
                  ))}
                </StepDetail>
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
