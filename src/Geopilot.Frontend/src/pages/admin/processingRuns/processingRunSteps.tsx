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
  InputKind,
  InputOrigin,
  PipelineRunFileResponse,
  PipelineRunInputResponse,
  PipelineRunResponse,
  StepState,
} from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { StepIcon } from "../../../components/stepIcon.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatDuration, formatInputValue, formatStepState } from "./processingRunFormat.ts";
import { ProcessingRunPreflightStep } from "./processingRunPreflightStep.tsx";
import { StepTitle } from "./stepTitle.tsx";
import { TechnicalText } from "./technicalText.tsx";

const inputOriginLabelKey: Record<InputOrigin, TranslationKey> = {
  [InputOrigin.Definition]: "inputOriginDefinition",
  [InputOrigin.StepOutput]: "inputOriginStepOutput",
  [InputOrigin.Resource]: "inputOriginResource",
  [InputOrigin.Upload]: "inputOriginUpload",
  [InputOrigin.Configuration]: "inputOriginConfiguration",
};

// A delivered file shows the hash of the preparation also where an earlier step passed it on, so a reader sees that a
// step got exactly the delivered file. The protocol records it only at the step that took the file from the upload.
const hashOfFile = (input: PipelineRunInputResponse, files: PipelineRunFileResponse[]) =>
  input.sha256 ?? (input.fromUpload ? files.find(file => file.fileName === input.fileName)?.sha256 : undefined);

// A file a step of the run made never has a hash; any other file lacks one only where it could not be computed.
const missingHashKey = (input: PipelineRunInputResponse): TranslationKey =>
  input.origin === InputOrigin.StepOutput && !input.fromUpload ? "fileCreatedInProcessing" : "hashNotComputed";

interface ProcessingRunStepsProps {
  run: PipelineRunResponse;
}

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

/**
 * The steps of a run as the delivery shows them: the preflight first, then the steps in pipeline order, each with its
 * state, duration, inputs, messages, conditions and results. Why the run failed outside them stands above.
 */
export const ProcessingRunSteps: FC<ProcessingRunStepsProps> = ({ run }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const runFailure = run.outcome.preflightFailed ? undefined : run.failureReason;

  return (
    <Stack data-cy="processingRunSteps">
      {runFailure && (
        <Alert severity="error" data-cy="processingRunFailureReason">
          {runFailure}
        </Alert>
      )}
      <Box>
        <ProcessingRunPreflightStep run={run} />
        {run.steps.map((step, index) => {
          // A skipped step has times too, but they only span the check of its skip conditions.
          const duration = step.state !== StepState.Skipped && formatDuration(step.startedAt, step.finishedAt);
          return (
            <Accordion key={step.stepId} defaultExpanded disableGutters data-cy={`processingRunStep-${step.stepId}`}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Stack
                  direction="row"
                  sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
                  <StepIcon step={index + 2} state={step.state} variant="outlined" />
                  <Typography sx={{ flex: "1 1 0" }}>
                    <StepTitle id={step.stepId} displayName={step.displayName} />
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {duration
                      ? t("stepStateAfterDuration", { state: formatStepState(t, step.state), duration })
                      : formatStepState(t, step.state)}
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
                        sx={{ color: "text.secondary", marginLeft: 1, display: "inline-block" }}>
                        (&nbsp;v{step.processAssemblyVersion}&nbsp;)
                      </Typography>
                    )}
                  </SummaryField>
                  {step.inputs.length > 0 && (
                    <SummaryField labelVariant="h5" label="input">
                      <DetailTable
                        columns={[
                          { label: t("parameter"), width: nameColumnWidth },
                          { label: t("inputSource"), width: "24%" },
                          { label: t("value") },
                          { label: t("sha256"), width: "30%" },
                        ]}>
                        {step.inputs.map((input, inputIndex) => {
                          const hash = hashOfFile(input, run.files);
                          return (
                            <TableRow key={inputIndex}>
                              <TableCell>{input.parameter}</TableCell>
                              <TableCell>
                                {t(inputOriginLabelKey[input.origin])}
                                {input.reference && (
                                  <Box>
                                    <TechnicalText>{input.reference}</TechnicalText>
                                  </Box>
                                )}
                              </TableCell>
                              <TableCell
                                sx={
                                  input.kind === InputKind.Absent
                                    ? { color: "text.secondary", fontStyle: "italic" }
                                    : undefined
                                }>
                                {input.kind === InputKind.Absent && t("stepStateSkipped")}
                                {input.kind === InputKind.Value && (
                                  <TechnicalText>{formatInputValue(input)}</TechnicalText>
                                )}
                                {input.kind === InputKind.File && formatInputValue(input)}
                              </TableCell>
                              <TableCell sx={{ color: hash ? undefined : "text.secondary" }}>
                                {hash ? (
                                  <TechnicalText>{hash}</TechnicalText>
                                ) : (
                                  input.kind === InputKind.File && t(missingHashKey(input))
                                )}
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </DetailTable>
                    </SummaryField>
                  )}
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
                  {/* A cancelled step only holds the message of the cancellation; the run says why it was cancelled. */}
                  {step.errorMessage && step.state !== StepState.Cancelled && (
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
