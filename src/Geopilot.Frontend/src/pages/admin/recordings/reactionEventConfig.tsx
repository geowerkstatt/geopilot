import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack, TableCell, TableRow } from "@mui/material";
import { ConfigOrigin, RecordingConfigValueResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatConfigValue } from "./pipelineSnapshotModel.ts";
import { TechnicalText } from "./technicalText.tsx";

// The same names as the origins of the configuration in the pipeline view.
const configOriginKey: Record<ConfigOrigin, TranslationKey> = {
  [ConfigOrigin.Base]: "configOriginBase",
  [ConfigOrigin.Definition]: "configOriginDefinition",
  [ConfigOrigin.Mandate]: "configOriginMandate",
};

interface ReactionEventConfigProps {
  appVersion: string;
  config?: { [key: string]: RecordingConfigValueResponse } | null;
}

/**
 * The version and the configuration an attempt of a reaction ran with, the configuration in the format of the inputs
 * of a step. An attempt whose channel was unknown has no configuration and shows the version only.
 */
export const ReactionEventConfig: FC<ReactionEventConfigProps> = ({ appVersion, config }) => {
  const { t } = useTranslation();

  return (
    <Stack data-cy="reactionEventConfig">
      <SummaryField label="appVersion">{appVersion}</SummaryField>
      {config && (
        <SummaryField label="configuration">
          <DetailTable
            columns={[
              { label: t("parameter"), width: nameColumnWidth },
              { label: t("value") },
              { label: t("inputSource"), width: "20%" },
              { label: t("sha256"), width: "30%" },
            ]}>
            {/* Sorted, because PostgreSQL returns a jsonb object ordered by the length of its keys. */}
            {Object.entries(config)
              .sort(([left], [right]) => left.localeCompare(right))
              .map(([parameter, entry]) => {
                const value = formatConfigValue(entry.value);
                return (
                  <TableRow key={parameter} data-cy={`reactionEventConfig-${parameter}`}>
                    <TableCell>{parameter}</TableCell>
                    {value ? (
                      <TableCell>{value}</TableCell>
                    ) : (
                      <TableCell sx={{ color: "text.secondary", fontStyle: "italic" }}>
                        {t(value === null ? "configValueNotSet" : "configValueEmpty")}
                      </TableCell>
                    )}
                    <TableCell>{t(configOriginKey[entry.origin])}</TableCell>
                    <TableCell>{entry.sha256 && <TechnicalText>{entry.sha256}</TechnicalText>}</TableCell>
                  </TableRow>
                );
              })}
          </DetailTable>
        </SummaryField>
      )}
    </Stack>
  );
};
