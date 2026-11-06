import { FC } from "react";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
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
import { StepIcon } from "../../../components/stepIcon.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatDuration } from "./pipelineRunFormat.ts";
import { TechnicalText } from "./technicalText.tsx";
import { TitledList } from "./titledList.tsx";

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
    <Stack sx={{ gap: 1 }} data-cy="pipelineRunSteps">
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {t("inputsNotRecorded")}
      </Typography>
      {steps.map((step, index) => {
        const duration = formatDuration(step.startedAt, step.finishedAt);
        return (
          <Accordion key={step.stepId} disableGutters data-cy={`pipelineRunStep-${step.stepId}`}>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%" }}>
                <StepIcon step={index + 1} state={step.state} variant="outlined" />
                <Typography sx={{ flex: "1 1 0" }}>{localized(step.displayName, step.stepId)}</Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {duration ? `${t(stepStateKey[step.state])} · ${duration}` : t(stepStateKey[step.state])}
                </Typography>
              </Stack>
            </AccordionSummary>
            <AccordionDetails>
              <Stack sx={{ gap: 2 }}>
                <Typography variant="body2" sx={{ color: "text.secondary", overflowWrap: "anywhere" }}>
                  <TechnicalText>
                    {[step.processImplementation, step.processAssemblyVersion].filter(Boolean).join(" ")}
                  </TechnicalText>
                </Typography>
                {step.statusMessage && <SummaryField label="message">{localized(step.statusMessage)}</SummaryField>}
                {step.conditionMessage && (
                  <SummaryField label="conditionMessage">{localized(step.conditionMessage)}</SummaryField>
                )}
                {step.errorMessage && <SummaryField label="errorMessage">{step.errorMessage}</SummaryField>}
                {step.conditions.length > 0 && (
                  <SummaryField label="conditions">
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>{t("conditionId")}</TableCell>
                          <TableCell>{t("conditionType")}</TableCell>
                          <TableCell>{t("expression")}</TableCell>
                          <TableCell>{t("result")}</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {step.conditions.map((condition, conditionIndex) => (
                          <TableRow key={conditionIndex}>
                            <TableCell>
                              <TechnicalText>{condition.conditionId}</TechnicalText>
                            </TableCell>
                            <TableCell>
                              {`${t(conditionPhaseKey[condition.phase])} · ${t(conditionKindKey[condition.kind])}`}
                            </TableCell>
                            <TableCell sx={{ overflowWrap: "anywhere" }}>
                              <TechnicalText>{condition.expression}</TechnicalText>
                              {Object.entries(condition.evaluatedValues ?? {}).map(([name, value]) => (
                                <Typography key={name} variant="body2" sx={{ color: "text.secondary" }}>
                                  <TechnicalText>{`${name} = ${value ?? "null"}`}</TechnicalText>
                                </Typography>
                              ))}
                            </TableCell>
                            <TableCell>{t(condition.matched ? "conditionMatched" : "conditionNotMatched")}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </SummaryField>
                )}
                {Object.values(ArtifactKind).map(kind => {
                  const artifacts = step.artifacts.filter(artifact => artifact.kind === kind);
                  return (
                    artifacts.length > 0 && (
                      <TitledList key={kind} title={t(artifactKindKey[kind])}>
                        {artifacts.map(artifact => (
                          <Typography
                            key={artifact.persistedFileName}
                            component="li"
                            variant="body2"
                            sx={{ overflowWrap: "anywhere" }}>
                            <TechnicalText>{artifact.originalFileName}</TechnicalText>
                          </Typography>
                        ))}
                      </TitledList>
                    )
                  );
                })}
              </Stack>
            </AccordionDetails>
          </Accordion>
        );
      })}
    </Stack>
  );
};
