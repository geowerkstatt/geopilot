import { FC } from "react";
import { useTranslation } from "react-i18next";
import { TableCell, TableRow } from "@mui/material";
import { ConfigOrigin, RecordingConfigValueResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { formatConfigValue } from "./pipelineSnapshotModel.ts";
import { TechnicalText } from "./technicalText.tsx";

// The same names as the origins of the configuration in the pipeline view.
const configOriginKey: Record<ConfigOrigin, TranslationKey> = {
  [ConfigOrigin.Base]: "configOriginBase",
  [ConfigOrigin.Definition]: "configOriginDefinition",
  [ConfigOrigin.Mandate]: "configOriginMandate",
};

interface ReactionEventConfigProps {
  config: { [key: string]: RecordingConfigValueResponse };
}

/** The configuration an attempt of a reaction ran with, in the format of the inputs of a step. */
export const ReactionEventConfig: FC<ReactionEventConfigProps> = ({ config }) => {
  const { t } = useTranslation();

  return (
    <DetailTable
      dataCy="reactionEventConfig"
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
  );
};
