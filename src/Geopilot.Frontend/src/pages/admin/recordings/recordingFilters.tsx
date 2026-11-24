import { FC, useState } from "react";
import { useTranslation } from "react-i18next";
import { Theme, useMediaQuery } from "@mui/material";
import { RecordingStateFilter } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { FilterBar } from "../../../components/filterBar.tsx";
import { FormAutocomplete, FormInput, FormValueType } from "../../../components/form/form.ts";
import { FilterOption, toFilterOptions, useFilterOptions } from "../../../hooks/useFilterOptions.ts";
import { emptyRecordingFilters, parseJobId, RecordingFilterValues } from "./recordingQuery.ts";

interface RecordingFiltersProps {
  values: RecordingFilterValues;
  onChange: (values: RecordingFilterValues) => void;
  /** Called with a complete job id typed or pasted into the job id field, to open its run. */
  onJobIdEntered: (jobId: string) => void;
}

// The same names as the steps of the delivery wizard; a run without terminal state has none of them.
const stateLabelKey: Record<RecordingStateFilter, TranslationKey> = {
  [RecordingStateFilter.Success]: "stepStateFinished",
  [RecordingStateFilter.Warning]: "stepStateWarning",
  [RecordingStateFilter.DeliveryRestriction]: "stepStateDeliveryRestriction",
  [RecordingStateFilter.Failed]: "stepStateFailed",
  [RecordingStateFilter.Cancelled]: "stepStateCancelled",
  [RecordingStateFilter.Open]: "recordingStateOpen",
};

/** The filters of the list of recordings: period, who started the job, mandate and state, plus a job id lookup. */
export const RecordingFilters: FC<RecordingFiltersProps> = ({ values, onChange, onJobIdEntered }) => {
  const { t } = useTranslation();
  const { userOrClientOptions, mandateOptions } = useFilterOptions();
  const [jobIdText, setJobIdText] = useState("");
  const narrow = useMediaQuery((theme: Theme) => theme.breakpoints.down("md"), { noSsr: true });

  const stateOptions = toFilterOptions(
    Object.values(RecordingStateFilter).map(state => ({ value: state, name: t(stateLabelKey[state]) })),
  );

  const changeTime = (field: "from" | "to") => (value: string) => onChange({ ...values, [field]: value });

  const changeJobId = (text: string) => {
    setJobIdText(text);
    const jobId = parseJobId(text);
    if (jobId) {
      onJobIdEntered(jobId);
    }
  };

  const clearFilters = () => {
    setJobIdText("");
    onChange(emptyRecordingFilters);
  };

  const listFilter = (
    field: "startedBy" | "mandateIds" | "states",
    label: TranslationKey,
    options: FilterOption[],
    dataCy: string,
  ) => (
    <FormAutocomplete<FilterOption>
      label={label}
      values={options}
      selected={options.filter(option => values[field].includes(option.value))}
      valueFormatter={option => ({ id: option.id, primaryText: option.name })}
      onChange={selected => onChange({ ...values, [field]: selected.map(option => option.value) })}
      dataCy={dataCy}
      sx={{ minWidth: 0 }}
    />
  );

  return (
    <FilterBar
      searchPlaceholder="recordingJobIdSearch"
      searchQuery={jobIdText}
      onSearchQueryChange={changeJobId}
      activeFilterCount={Object.values(values).filter(value => value.length > 0).length}
      onClearFilters={clearFilters}
      collapsible={narrow}
      columns={5}
      dataCy="recordingFilters">
      <FormInput
        type={FormValueType.DateTime}
        label="periodFrom"
        value={values.from}
        onChange={changeTime("from")}
        dataCy="recordingFilter-from"
      />
      <FormInput
        type={FormValueType.DateTime}
        label="periodTo"
        value={values.to}
        onChange={changeTime("to")}
        dataCy="recordingFilter-to"
      />
      {listFilter("startedBy", "processedBy", userOrClientOptions, "recordingFilter-startedBy")}
      {listFilter("mandateIds", "mandate", mandateOptions, "recordingFilter-mandate")}
      {listFilter("states", "processingState", stateOptions, "recordingFilter-state")}
    </FilterBar>
  );
};
