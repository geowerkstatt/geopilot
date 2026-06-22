import { FC, useEffect } from "react";
import { PipelineSummary } from "../../../api/apiInterfaces";
import { FormSelect } from "../../../components/form/form";
import { FormSelectValue } from "../../../components/form/formSelect";
import { useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { findPipeline, getLocalisedPipelineName } from "./pipelineDisplay";

interface PipelineFormSelectProps {
  pipelines?: PipelineSummary[];
  selected?: string;
}

const getPipelineSelectMenuItems = (
  pipelines: PipelineSummary[] | undefined,
  language: string,
  t: (key: string) => string,
): FormSelectValue[] =>
  pipelines?.map((pipeline, idx) => ({
    key: idx,
    value: pipeline.id,
    name: `${getLocalisedPipelineName(pipeline, language)} (${t("id")}: ${pipeline.id})`,
  })) ?? [];

export const PipelineFormSelect: FC<PipelineFormSelectProps> = ({ pipelines, selected }) => {
  const { t, i18n } = useTranslation();
  const { trigger } = useFormContext();

  // The pipeline a mandate was configured with may have been removed from the definition. Re-validate once
  // the available pipelines are known so a now-missing reference is flagged on load, not only on submit.
  useEffect(() => {
    if (pipelines !== undefined && selected) {
      trigger("pipelineId");
    }
  }, [pipelines, selected, trigger]);

  const menuItems = getPipelineSelectMenuItems(pipelines, i18n.language, t);

  return (
    <FormSelect
      fieldName={"pipelineId"}
      label={"pipeline"}
      required={true}
      values={menuItems}
      selected={selected}
      validate={value =>
        !value || pipelines === undefined || findPipeline(pipelines, value as string) !== undefined
          ? true
          : t("pipelineNotKnown", { pipelineId: value })
      }
    />
  );
};

export default PipelineFormSelect;
