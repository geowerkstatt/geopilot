import { Accordion, AccordionDetails, AccordionSummary, Typography } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { FlexBox, FlexRowBox, FlexRowEndBox } from "../../../components/styledComponents";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { DeliveryContext } from "../deliveryContext";
import { SyntheticEvent, useContext, useEffect, useMemo, useRef, useState } from "react";
import i18next from "i18next";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { isProcessingDeliverable } from "../deliveryUtils";
import { StepResult, StepState } from "../../../api/apiInterfaces";
import { ProcessingStepIcon } from "./processingStepIcon";

const localized = (entries?: Record<string, string>) =>
  entries?.[i18next.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "";

const stepHasContent = (step: StepResult) => Boolean(step.statusMessage) || step.downloads.length > 0;

const TERMINAL_STATES: ReadonlySet<StepState> = new Set([
  StepState.Success,
  StepState.Error,
  StepState.Cancelled,
  StepState.Skipped,
]);

export const DeliveryProcessingResults = () => {
  const { processingResponse, resetDelivery, isProcessing } = useContext(DeliveryContext);
  const [expandedStepIds, setExpandedStepIds] = useState<Set<string>>(new Set());
  const autoExpandedIds = useRef<Set<string>>(new Set());

  const steps = useMemo(() => processingResponse?.steps ?? [], [processingResponse?.steps]);

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
  }, [steps]);

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

  return (
    <FlexBox>
      {processingResponse?.deliveryRestrictionMessage && (
        <Typography variant="body1" color="error">
          {localized(processingResponse.deliveryRestrictionMessage)}
        </Typography>
      )}
      {steps.map((step, index) => {
        const isExpandable = step.state !== StepState.Pending && stepHasContent(step);
        return (
          <Accordion
            key={step.id}
            expanded={isExpandable && expandedStepIds.has(step.id)}
            onChange={isExpandable ? handleAccordionChange(step.id) : undefined}
            disableGutters
            data-cy={`processing-step-${step.id}`}>
            <AccordionSummary
              expandIcon={isExpandable ? <ExpandMoreIcon /> : null}
              sx={isExpandable ? undefined : { cursor: "default", "&:hover": { backgroundColor: "transparent" } }}>
              <FlexRowBox sx={{ alignItems: "center", gap: 2 }}>
                <ProcessingStepIcon state={step.state} index={index} />
                <Typography variant="h5" sx={{ margin: 0 }}>
                  {localized(step.name)}
                </Typography>
              </FlexRowBox>
            </AccordionSummary>
            <AccordionDetails>
              <FlexBox>
                {step.statusMessage && <Typography variant="body1">{localized(step.statusMessage)}</Typography>}
                {step.downloads.length > 0 && (
                  <FlexRowBox>
                    {step.downloads.map(d => (
                      <BaseButton
                        key={d.originalFileName}
                        variant="outlined"
                        onClick={() => download(d.url, d.originalFileName)}
                        icon={<FileDownloadIcon />}
                        label={d.originalFileName}
                      />
                    ))}
                  </FlexRowBox>
                )}
              </FlexBox>
            </AccordionDetails>
          </Accordion>
        );
      })}
      {!isProcessing && !isProcessingDeliverable(processingResponse) && (
        <FlexRowEndBox>
          <CancelButton onClick={resetDelivery} />
        </FlexRowEndBox>
      )}
    </FlexBox>
  );
};
