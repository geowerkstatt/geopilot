import { FC } from "react";
import { PipelineSummary } from "../../../api/apiInterfaces";
import { FormSelect } from "../../../components/form/form";
import { FormSelectValue } from "../../../components/form/formSelect";
import { useTranslation } from "react-i18next";

interface PipelineFormSelectProps {
  pipelines?: PipelineSummary[];
  selected?: string;
}

const getLocalisedPipelineName = (pipeline: PipelineSummary, language: string): string => {
  return pipeline.displayName[language] || pipeline.displayName["de"] || pipeline.id;
};

const getPipelineSelectMenuItems = (
  pipelines?: PipelineSummary[],
  language?: string,
  t?: (key: string) => string,
): FormSelectValue[] => {
  return (
    pipelines?.map((pipeline, idx) => ({
      key: idx,
      value: pipeline.id,
      name: `${getLocalisedPipelineName(pipeline, language ?? "de")} (${t ? t("id") : "ID"}: ${pipeline.id})`,
    })) ?? []
  );
};

export const PipelineFormSelect: FC<PipelineFormSelectProps> = ({ pipelines, selected }) => {
  const { t, i18n } = useTranslation();

  return (
    <FormSelect
      fieldName={"pipelineId"}
      label={"pipeline"}
      required={true}
      values={getPipelineSelectMenuItems(pipelines, i18n.language, t)}
      selected={selected}
    />
  );
};

export default PipelineFormSelect;
