import { FC, useEffect } from "react";
import { useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { PipelineSummary } from "../../../api/apiInterfaces";
import { FormSelect } from "../../../components/form/form";
import { FormSelectValue } from "../../../components/form/formSelect";
import { findPipeline, getLocalisedPipelineName } from "./pipelineDisplay";

interface PipelineFormSelectProps {
  pipelines?: PipelineSummary[];
  selected?: string;
}

const getPipelineSelectMenuItems = (
  pipelines: PipelineSummary[] | undefined,
  selected: string | undefined,
  language: string,
  t: (key: string) => string,
): FormSelectValue[] => {
  const items: FormSelectValue[] =
    pipelines?.map((pipeline, idx) => {
      const localisedName = getLocalisedPipelineName(pipeline, language);
      return {
        key: idx,
        value: pipeline.id,
        name: localisedName === pipeline.id ? pipeline.id : `${localisedName} (${t("id")}: ${pipeline.id})`,
      };
    }) ?? [];

  // A mandate may reference a pipeline that was removed from the definition. Keep its id as the current
  // value so the user sees what is configured, but hide it from the dropdown options. It stays out of the
  // available pipelines, so validation still flags it as unknown.
  if (selected && pipelines !== undefined && findPipeline(pipelines, selected) === undefined) {
    items.push({ key: items.length, value: selected, name: selected, hidden: true });
  }

  return items;
};

const PipelineFormSelect: FC<PipelineFormSelectProps> = ({ pipelines, selected }) => {
  const { t, i18n } = useTranslation();
  const { trigger } = useFormContext();

  // The pipeline a mandate was configured with may have been removed from the definition. Re-validate once
  // the available pipelines are known so a now-missing reference is flagged on load, not only on submit.
  useEffect(() => {
    if (pipelines !== undefined && selected) {
      trigger("pipelineId");
    }
  }, [pipelines, selected, trigger]);

  const menuItems = getPipelineSelectMenuItems(pipelines, selected, i18n.language, t);

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
