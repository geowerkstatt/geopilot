import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Alert,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { DefinitionSnapshot } from "../../../api/apiInterfaces.ts";
import { TranslationKey } from "../../../appInterfaces";
import { Button } from "../../../components/buttons.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { buildSnapshotSteps, SnapshotConfigOrigin } from "./pipelineSnapshotModel.ts";
import { TechnicalText } from "./technicalText.tsx";
import { TitledList } from "./titledList.tsx";

interface PipelineSnapshotProps {
  jobId: string;
  pipelineId: string;
}

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
  const [technical, setTechnical] = useState(false);

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

  return (
    <Stack sx={{ gap: 2 }} data-cy="pipelineSnapshot">
      <Alert severity="info">{t("pipelineSnapshotSecretsHint")}</Alert>
      <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap" }}>
        <Typography sx={{ flex: "1 1 0" }}>{t("pipelineSnapshotIntro", { pipelineId })}</Typography>
        <Button
          variant="outlined"
          label={technical ? "pipelineSnapshotReadable" : "pipelineSnapshotTechnical"}
          onClick={() => setTechnical(!technical)}
        />
      </Stack>
      {technical ? (
        <Typography
          component="pre"
          variant="body2"
          sx={{ fontFamily: "monospace", whiteSpace: "pre-wrap", overflowWrap: "anywhere", margin: 0 }}>
          {JSON.stringify(snapshot, null, 2)}
        </Typography>
      ) : (
        buildSnapshotSteps(snapshot).map((step, index) => (
          <Stack key={step.id} sx={{ gap: 1, borderLeft: 3, borderColor: "primary.light", paddingLeft: 2 }}>
            <Typography variant="h6" sx={{ margin: 0 }}>
              {`${index + 1} · ${localized(step.displayName, step.id)}`}
            </Typography>
            {step.implementation && (
              <Typography variant="body2" sx={{ color: "text.secondary", overflowWrap: "anywhere" }}>
                <TechnicalText>{step.implementation}</TechnicalText>
              </Typography>
            )}
            {step.config.length > 0 && (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t("configuration")}</TableCell>
                    <TableCell>{t("configOrigin")}</TableCell>
                    <TableCell>{t("value")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {step.config.map(entry => (
                    <TableRow key={entry.key}>
                      <TableCell>
                        <TechnicalText>{entry.key}</TechnicalText>
                      </TableCell>
                      <TableCell>{t(originKey[entry.origin])}</TableCell>
                      <TableCell sx={{ overflowWrap: "anywhere" }}>
                        <TechnicalText>{entry.value}</TechnicalText>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
            {step.inputs.length > 0 && (
              <TitledList title={t("input")}>
                {step.inputs.map(input => (
                  <Typography key={input.key} component="li" variant="body2" sx={{ overflowWrap: "anywhere" }}>
                    <TechnicalText>{`${input.key} ← ${input.expression}`}</TechnicalText>
                  </Typography>
                ))}
              </TitledList>
            )}
            {step.conditionGroups.map(group => (
              <TitledList
                key={`${group.phase}-${group.kind}`}
                title={`${t(conditionKindKeys[group.kind] ?? "conditions")} (${t(conditionPhaseKeys[group.phase])})`}>
                {group.conditions.map((condition, conditionIndex) => (
                  <Typography key={conditionIndex} component="li" variant="body2" sx={{ overflowWrap: "anywhere" }}>
                    {condition.id && (
                      <>
                        <TechnicalText>{condition.id}</TechnicalText>
                        {": "}
                      </>
                    )}
                    <TechnicalText>{condition.expression}</TechnicalText>
                  </Typography>
                ))}
              </TitledList>
            ))}
            {step.outputs.length > 0 && (
              <TitledList title={t("output")}>
                {step.outputs.map(output => (
                  <Typography key={output.property} component="li" variant="body2" sx={{ overflowWrap: "anywhere" }}>
                    <TechnicalText>{output.property}</TechnicalText>
                    {` → ${output.actions.map(actionLabel).join(", ")}`}
                  </Typography>
                ))}
              </TitledList>
            )}
          </Stack>
        ))
      )}
    </Stack>
  );
};
