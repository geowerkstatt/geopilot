import { SyntheticEvent, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import BlockIcon from "@mui/icons-material/Block";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { Accordion, AccordionDetails, AccordionSummary, Alert, Stack, Typography } from "@mui/material";
import { StepResultResponse, StepState } from "../../api/generated";
import { Button } from "../../components/buttons.tsx";
import { StepIcon } from "../../components/stepIcon.tsx";
import { VisualizationLoader } from "../../components/visualizations/visualizationLoader.tsx";
import { useLocalized } from "../../hooks/useLocalized.ts";
import { DeliveryBackButton, DeliveryContinueButton } from "./deliveryButtons.tsx";
import { DeliveryContent } from "./deliveryContent.tsx";
import { DeliveryContext } from "./deliveryContext.tsx";
import { isProcessingDeliverable } from "./deliveryUtils.tsx";

const stepHasContent = (step: StepResultResponse) =>
  Boolean(step.conditionMessage) ||
  Boolean(step.statusMessage) ||
  step.downloads.length > 0 ||
  (step.visualizations?.length ?? 0) > 0;

const stepIsExpandable = (step: StepResultResponse) => step && step.state !== StepState.Pending && stepHasContent(step);

const TERMINAL_STATES: ReadonlySet<StepState> = new Set<StepState>([
  StepState.Success,
  StepState.Error,
  StepState.Cancelled,
  StepState.Skipped,
  StepState.Warning,
  StepState.DeliveryRestriction,
]);

export const DeliveryProcessing = () => {
  const { t } = useTranslation();
  const { localized } = useLocalized();

  const { isProcessing, processingResponse } = useContext(DeliveryContext);
  const [expandedStepIds, setExpandedStepIds] = useState<Set<string>>(new Set());
  const autoExpandedIds = useRef<Set<string>>(new Set());

  const steps = useMemo(() => processingResponse?.steps ?? [], [processingResponse?.steps]);
  const stepRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const [scrollToStep, setScrollToStep] = useState<StepResultResponse | null>(null);

  // Auto-expand each step once it has reached a terminal state and has displayable
  // content. State and content can arrive in separate polls, so we re-evaluate on
  // every update and only auto-expand each step once — manual collapses afterward
  // are respected because we track which ids we've already auto-expanded.
  useEffect(() => {
    if (steps.length === 0) return;

    const newlyExpanded: string[] = [];
    for (const step of steps) {
      if (autoExpandedIds.current.has(step.id)) continue;
      if (!TERMINAL_STATES.has(step.state)) continue;
      if (!stepHasContent(step)) continue;

      autoExpandedIds.current.add(step.id);
      newlyExpanded.push(step.id);
    }

    if (newlyExpanded.length === 0) return;
    setExpandedStepIds(prev => {
      const next = new Set(prev);
      for (const id of newlyExpanded) next.add(id);
      return next;
    });
    setScrollToStep(steps.find(s => s.id === newlyExpanded[newlyExpanded.length - 1]) ?? null);
  }, [steps]);

  useEffect(() => {
    if (!scrollToStep) return;
    // Scroll immediately if the step is not expandable
    if (!stepIsExpandable(scrollToStep)) {
      stepRefs.current[scrollToStep.id]?.scrollIntoView({ behavior: "smooth", block: "center" });
      setScrollToStep(null);
    }
  }, [scrollToStep]);

  // Scroll after the accordion is expanded
  const handleStepExpanded = (stepId: string) => () => {
    if (scrollToStep?.id !== stepId) return;
    stepRefs.current[stepId]?.scrollIntoView({ behavior: "smooth", block: "center" });
    setScrollToStep(null);
  };

  const download = (url: string, fileName: string) => {
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
  };

  const handleAccordionChange = (stepId: string) => (_event: SyntheticEvent, isExpanded: boolean) => {
    setExpandedStepIds(prev => {
      const next = new Set(prev);
      if (isExpanded) {
        next.add(stepId);
      } else {
        next.delete(stepId);
      }
      return next;
    });
  };

  const buttons = (
    <>
      <DeliveryBackButton />
      <DeliveryContinueButton disabled={isProcessing || !isProcessingDeliverable(processingResponse)} />
    </>
  );

  return (
    <DeliveryContent title="processing" buttons={buttons} hideBox={true}>
      {steps.map((step, index) => {
        const isExpandable = stepIsExpandable(step);
        const isExpanded = isExpandable && expandedStepIds.has(step.id);

        return (
          <Accordion
            key={step.id}
            ref={el => {
              stepRefs.current[step.id] = el;
            }}
            expanded={isExpanded}
            onChange={isExpandable ? handleAccordionChange(step.id) : undefined}
            slotProps={{ transition: { onEntered: handleStepExpanded(step.id) } }}
            sx={{ position: "relative" }}
            data-cy={`processing-step-${step.id}`}>
            <AccordionSummary expandIcon={isExpandable ? <ExpandMoreIcon /> : null}>
              <Stack direction="row" sx={{ alignItems: "center", flexWrap: "nowrap" }}>
                <StepIcon step={index + 1} state={step.state} variant={"outlined"} />
                <Typography variant="h4" sx={{ margin: 0 }}>
                  {localized(step.name)}
                </Typography>
              </Stack>
            </AccordionSummary>
            <AccordionDetails>
              <Stack>
                {step.conditionMessage &&
                  (step.state === StepState.DeliveryRestriction ? (
                    <Alert severity="error" icon={<BlockIcon fontSize="inherit" />}>
                      {t("deliveryNotPossible")}: {localized(step.conditionMessage)}
                    </Alert>
                  ) : (
                    <Alert
                      severity={
                        step.state === StepState.Skipped
                          ? "info"
                          : step.state === StepState.Warning
                            ? "warning"
                            : "error"
                      }>
                      {localized(step.conditionMessage)}
                    </Alert>
                  ))}
                {step.statusMessage && <Typography variant="body1">{localized(step.statusMessage)}</Typography>}
                {step.downloads.length > 0 && (
                  <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap" }}>
                    {step.downloads.map(d => (
                      <Button
                        key={d.originalFileName}
                        onClick={() => download(d.url, d.originalFileName)}
                        startIcon={<FileDownloadIcon />}
                        label={d.originalFileName}
                      />
                    ))}
                  </Stack>
                )}
                {isExpanded && step.visualizations?.map(v => <VisualizationLoader key={v.url} url={v.url} />)}
              </Stack>
            </AccordionDetails>
          </Accordion>
        );
      })}
    </DeliveryContent>
  );
};
