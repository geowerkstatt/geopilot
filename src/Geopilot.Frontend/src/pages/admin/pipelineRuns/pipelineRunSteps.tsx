import { FC } from "react";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Stack,
  TableCell,
  TableRow,
  Typography,
} from "@mui/material";
import {
  ArtifactKind,
  ConditionKind,
  ConditionPhase,
  PipelineRunStepResponse,
  StepState,
} from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { StepIcon } from "../../../components/stepIcon.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatDuration } from "./pipelineRunFormat.ts";
import { TechnicalText } from "./technicalText.tsx";

interface PipelineRunStepsProps {
  steps: PipelineRunStepResponse[];
}

// The same names as the steps of the delivery wizard.
const stepStateKey: Record<StepState, TranslationKey> = {
  [StepState.Pending]: "stepStatePending",
  [StepState.Running]: "stepStateRunning",
  [StepState.Skipped]: "stepStateSkipped",
  [StepState.Success]: "stepStateFinished",
  [StepState.Error]: "stepStateFailed",
  [StepState.Cancelled]: "stepStateCancelled",
  [StepState.Warning]: "stepStateWarning",
  [StepState.DeliveryRestriction]: "stepStateDeliveryRestriction",
};

const conditionKindKey: Record<ConditionKind, TranslationKey> = {
  [ConditionKind.Skip]: "conditionKindSkip",
  [ConditionKind.Fail]: "conditionKindFail",
  [ConditionKind.Warn]: "conditionKindWarn",
  [ConditionKind.RestrictDelivery]: "conditionKindRestrictDelivery",
};

const conditionPhaseKey: Record<ConditionPhase, TranslationKey> = {
  [ConditionPhase.Pre]: "conditionPhasePre",
  [ConditionPhase.Post]: "conditionPhasePost",
};

// An artifact is named after the output action that produced it, as in the pipeline view.
const artifactKindKey: Record<ArtifactKind, TranslationKey> = {
  [ArtifactKind.Download]: "outputActionDownload",
  [ArtifactKind.Visualization]: "outputActionVisualization",
  [ArtifactKind.Delivery]: "outputActionDelivery",
};

/** The steps of a run in pipeline order, each with its state, duration, messages, conditions and results. */
export const PipelineRunSteps: FC<PipelineRunStepsProps> = ({ steps }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();

  return (
    <Stack data-cy="pipelineRunSteps">
      <Alert severity="info">{t("inputsNotRecorded")}</Alert>
      <Box>
        {steps.map((step, index) => {
          // A skipped step has times too, but they only span the check of its skip conditions.
          const duration = step.state !== StepState.Skipped && formatDuration(step.startedAt, step.finishedAt);
          return (
            <Accordion key={step.stepId} defaultExpanded disableGutters data-cy={`pipelineRunStep-${step.stepId}`}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Stack
                  direction="row"
                  sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
                  <StepIcon step={index + 1} state={step.state} variant="outlined" />
                  <Typography sx={{ flex: "1 1 0" }}>{localized(step.displayName, step.stepId)}</Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {duration
                      ? t("stepStateAfterDuration", { state: t(stepStateKey[step.state]), duration })
                      : t(stepStateKey[step.state])}
                  </Typography>
                </Stack>
              </AccordionSummary>
              <AccordionDetails>
                <Stack>
                  <SummaryField labelVariant="h5" label="process">
                    {step.processImplementation}
                    {step.processAssemblyVersion && (
                      <Typography
                        component="span"
                        variant="body1"
                        sx={{ color: "text.secondary", marginLeft: 1, whiteSpace: "nowrap" }}>
                        ( v{step.processAssemblyVersion} )
                      </Typography>
                    )}
                  </SummaryField>
                  {step.statusMessage && (
                    <SummaryField labelVariant="h5" label="message">
                      {localized(step.statusMessage)}
                    </SummaryField>
                  )}
                  {step.conditionMessage && (
                    <SummaryField labelVariant="h5" label="conditionMessage">
                      {localized(step.conditionMessage)}
                    </SummaryField>
                  )}
                  {step.errorMessage && (
                    <SummaryField labelVariant="h5" label="errorMessage">
                      {step.errorMessage}
                    </SummaryField>
                  )}
                  {step.conditions.length > 0 && (
                    <SummaryField labelVariant="h5" label="conditions">
                      <DetailTable
                        columns={[
                          { label: t("conditionId"), width: "16%" },
                          { label: t("conditionPhase"), width: "12%" },
                          { label: t("conditionExpression") },
                          { label: t("evaluatedValues"), width: "20%" },
                          { label: t("conditionType"), width: "14%" },
                          { label: t("result"), width: "12%" },
                        ]}>
                        {step.conditions.map((condition, conditionIndex) => (
                          <TableRow key={conditionIndex}>
                            <TableCell>{condition.conditionId}</TableCell>
                            <TableCell>{t(conditionPhaseKey[condition.phase])}</TableCell>
                            <TableCell>
                              <TechnicalText>{condition.expression}</TechnicalText>
                            </TableCell>
                            <TableCell>
                              {Object.entries(condition.evaluatedValues ?? {}).map(([name, value]) => (
                                <Box key={name}>
                                  <TechnicalText>{`${name} = ${value ?? "null"}`}</TechnicalText>
                                </Box>
                              ))}
                            </TableCell>
                            <TableCell>{t(conditionKindKey[condition.kind])}</TableCell>
                            <TableCell>{t(condition.matched ? "conditionMatched" : "conditionNotMatched")}</TableCell>
                          </TableRow>
                        ))}
                      </DetailTable>
                    </SummaryField>
                  )}
                  {step.artifacts.length > 0 && (
                    <SummaryField labelVariant="h5" label="output">
                      <DetailTable
                        columns={[{ label: t("fileName"), width: nameColumnWidth }, { label: t("outputActions") }]}>
                        {step.artifacts.map(artifact => (
                          <TableRow key={artifact.persistedFileName}>
                            <TableCell>{artifact.originalFileName}</TableCell>
                            <TableCell>{t(artifactKindKey[artifact.kind])}</TableCell>
                          </TableRow>
                        ))}
                      </DetailTable>
                    </SummaryField>
                  )}
                </Stack>
              </AccordionDetails>
            </Accordion>
          );
        })}
      </Box>
    </Stack>
  );
};
