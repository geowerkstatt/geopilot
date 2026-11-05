import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  CircularProgress,
  Stack,
  TableCell,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { DefinitionSnapshot, DeliveryStepState } from "../../../api/apiInterfaces.ts";
import { TranslationKey } from "../../../appInterfaces";
import { CopyButton } from "../../../components/copyButton.tsx";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { StepIcon } from "../../../components/stepIcon.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { buildSnapshotSteps, SnapshotConfigOrigin } from "./pipelineSnapshotModel.ts";
import { TechnicalText } from "./technicalText.tsx";

interface PipelineSnapshotProps {
  jobId: string;
  pipelineId: string;
}

type SnapshotView = "readable" | "json";

const originKey: Record<SnapshotConfigOrigin, TranslationKey> = {
  base: "configOriginBase",
  definition: "configOriginDefinition",
  step: "configOriginStep",
};

// The snapshot names the kinds of conditions by their keys in the definition file.
const conditionKindKeys: Record<string, TranslationKey> = {
  skip_conditions: "conditionKindSkip",
  fail_conditions: "conditionKindFail",
  warn_conditions: "conditionKindWarn",
  restrict_delivery_conditions: "conditionKindRestrictDelivery",
};

const conditionPhaseKeys: Record<"pre" | "post", TranslationKey> = {
  pre: "conditionPhasePre",
  post: "conditionPhasePost",
};

// The snapshot names the output actions as the definition does; one this view does not know stays as written.
const outputActionKeys: Partial<Record<string, TranslationKey>> = {
  Download: "outputActionDownload",
  Delivery: "outputActionDelivery",
  StatusMessage: "outputActionStatusMessage",
  Visualization: "outputActionVisualization",
};

/** The pipeline as it was configured when the run started, readable or as the JSON the protocol keeps. */
export const PipelineSnapshot: FC<PipelineSnapshotProps> = ({ jobId, pipelineId }) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const [loaded, setLoaded] = useState<{ jobId: string; snapshot: DefinitionSnapshot }>();
  const [view, setView] = useState<SnapshotView>("readable");

  useEffect(() => {
    fetchApi<DefinitionSnapshot>(`/api/v1/pipelinerun/${jobId}/definition`, {
      errorMessageLabel: "pipelineSnapshotLoadingError",
    })
      .then(snapshot => setLoaded({ jobId, snapshot }))
      // useFetch has shown why the snapshot could not be loaded.
      .catch(() => undefined);
  }, [fetchApi, jobId]);

  const actionLabel = (action: string) => {
    const key = outputActionKeys[action];
    return key ? t(key) : action;
  };

  const snapshot = loaded?.jobId === jobId ? loaded.snapshot : undefined;
  if (!snapshot) {
    return <CircularProgress />;
  }

  const json = JSON.stringify(snapshot, null, 2);

  return (
    <Stack data-cy="pipelineSnapshot">
      <Alert severity="warning">{t("pipelineSnapshotSecretsHint")}</Alert>
      <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <Typography sx={{ flex: "1 1 0" }}>{t("pipelineSnapshotIntro", { pipelineId })}</Typography>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={view}
          // A click on the selected option would clear the selection, so only a change of view is taken.
          onChange={(_event, value: SnapshotView | null) => value && setView(value)}
          aria-label={t("pipelineSnapshotView")}>
          <ToggleButton value="readable" data-cy="pipelineSnapshotView-readable">
            {t("pipelineSnapshotReadable")}
          </ToggleButton>
          <ToggleButton value="json" data-cy="pipelineSnapshotView-json">
            {t("pipelineSnapshotTechnical")}
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>
      {view === "json" ? (
        <Box sx={{ position: "relative" }}>
          <Typography
            component="pre"
            variant="body2"
            sx={{
              fontFamily: "monospace",
              whiteSpace: "pre",
              overflowX: "auto",
              margin: 0,
              padding: 2,
              backgroundColor: "primary.states.hover",
              border: 1,
              borderColor: "primary.light",
              borderRadius: theme => theme.radius.default,
            }}>
            {json}
          </Typography>
          <CopyButton text={json} size="small" sx={{ position: "absolute", top: 8, right: 8 }} />
        </Box>
      ) : (
        <Box>
          {buildSnapshotSteps(snapshot).map((step, index) => (
            <Accordion key={step.id} defaultExpanded disableGutters data-cy={`pipelineSnapshotStep-${step.id}`}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Stack direction="row" sx={{ alignItems: "center" }}>
                  <StepIcon step={index + 1} state={DeliveryStepState.Enabled} variant="outlined" />
                  <Typography>{localized(step.displayName, step.id)}</Typography>
                </Stack>
              </AccordionSummary>
              <AccordionDetails>
                <Stack>
                  {step.implementation && (
                    <SummaryField labelVariant="h5" label="process">
                      {step.implementation}
                    </SummaryField>
                  )}
                  {step.config.length > 0 && (
                    <SummaryField labelVariant="h5" label="configuration">
                      <DetailTable
                        columns={[
                          { label: t("parameter"), width: nameColumnWidth },
                          { label: t("value") },
                          { label: t("configOrigin"), width: "20%" },
                        ]}>
                        {step.config.map(entry => (
                          <TableRow key={entry.key}>
                            <TableCell>{entry.key}</TableCell>
                            {entry.value ? (
                              <TableCell>{entry.value}</TableCell>
                            ) : (
                              <TableCell sx={{ color: "text.secondary", fontStyle: "italic" }}>
                                {t(entry.value === null ? "configValueNotSet" : "configValueEmpty")}
                              </TableCell>
                            )}
                            <TableCell>{t(originKey[entry.origin])}</TableCell>
                          </TableRow>
                        ))}
                      </DetailTable>
                    </SummaryField>
                  )}
                  {step.inputs.length > 0 && (
                    <SummaryField labelVariant="h5" label="input">
                      <DetailTable
                        columns={[{ label: t("parameter"), width: nameColumnWidth }, { label: t("inputSource") }]}>
                        {step.inputs.map(input => (
                          <TableRow key={input.key}>
                            <TableCell>{input.key}</TableCell>
                            <TableCell>
                              <TechnicalText>{input.expression}</TechnicalText>
                            </TableCell>
                          </TableRow>
                        ))}
                      </DetailTable>
                    </SummaryField>
                  )}
                  {step.outputs.length > 0 && (
                    <SummaryField labelVariant="h5" label="output">
                      <DetailTable
                        columns={[{ label: t("result"), width: nameColumnWidth }, { label: t("outputActions") }]}>
                        {step.outputs.map(output => (
                          <TableRow key={output.property}>
                            <TableCell>{output.property}</TableCell>
                            <TableCell>{output.actions.map(actionLabel).join(", ")}</TableCell>
                          </TableRow>
                        ))}
                      </DetailTable>
                    </SummaryField>
                  )}
                  {step.conditionGroups.length > 0 && (
                    <SummaryField labelVariant="h5" label="conditions">
                      <DetailTable
                        columns={[
                          { label: t("conditionId"), width: nameColumnWidth },
                          { label: t("conditionPhase"), width: "12%" },
                          { label: t("conditionExpression") },
                          { label: t("conditionType"), width: "20%" },
                        ]}>
                        {step.conditionGroups.flatMap(group =>
                          group.conditions.map((condition, conditionIndex) => (
                            <TableRow key={`${group.phase}-${group.kind}-${conditionIndex}`}>
                              <TableCell>{condition.id}</TableCell>
                              <TableCell>{t(conditionPhaseKeys[group.phase])}</TableCell>
                              <TableCell>
                                <TechnicalText>{condition.expression}</TechnicalText>
                              </TableCell>
                              <TableCell>{t(conditionKindKeys[group.kind] ?? "conditions")}</TableCell>
                            </TableRow>
                          )),
                        )}
                      </DetailTable>
                    </SummaryField>
                  )}
                </Stack>
              </AccordionDetails>
            </Accordion>
          ))}
        </Box>
      )}
    </Stack>
  );
};
