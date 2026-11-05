import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { MachineClient, Mandate, PipelineRunStateFilter, User } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { FilterBar } from "../../../components/filterBar.tsx";
import { FormSelect } from "../../../components/form/form.ts";
import { FormDateTimePicker } from "../../../components/form/formDateTimePicker.tsx";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { emptyProcessingRunFilters, parseJobId, ProcessingRunFilterValues } from "./processingRunQuery.ts";

interface ProcessingRunFiltersProps {
  values: ProcessingRunFilterValues;
  onChange: (values: ProcessingRunFilterValues) => void;
  /** Called with a complete job id typed or pasted into the job id field, to open its run. */
  onJobIdEntered: (jobId: string) => void;
}

// The same names as the steps of the delivery wizard; a run without terminal state has none of them.
const stateLabelKey: Record<PipelineRunStateFilter, TranslationKey> = {
  [PipelineRunStateFilter.Success]: "stepStateFinished",
  [PipelineRunStateFilter.Warning]: "stepStateWarning",
  [PipelineRunStateFilter.DeliveryRestriction]: "stepStateDeliveryRestriction",
  [PipelineRunStateFilter.Failed]: "stepStateFailed",
  [PipelineRunStateFilter.Cancelled]: "stepStateCancelled",
  [PipelineRunStateFilter.Open]: "pipelineRunStateOpen",
};

/** The filters of the list of pipeline runs: period, who started the run, mandate and state, plus a job id lookup. */
export const ProcessingRunFilters: FC<ProcessingRunFiltersProps> = ({ values, onChange, onJobIdEntered }) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const [users, setUsers] = useState<User[]>([]);
  const [machineClients, setMachineClients] = useState<MachineClient[]>([]);
  const [mandates, setMandates] = useState<Mandate[]>([]);
  const [jobIdText, setJobIdText] = useState("");

  useEffect(() => {
    // useFetch shows why a list could not be loaded; the filter then offers fewer choices.
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" })
      .then(setUsers)
      .catch(() => undefined);
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" })
      .then(setMandates)
      .catch(() => undefined);
  }, [fetchApi]);

  useEffect(() => {
    // The machine clients only exist where the installation offers machine delivery.
    if (machineDeliveryEnabled) {
      fetchApi<MachineClient[]>("/api/v1/machineclient", { errorMessageLabel: "machineClientsLoadingError" })
        .then(setMachineClients)
        .catch(() => undefined);
    }
  }, [fetchApi, machineDeliveryEnabled]);

  // shortcut: one select with every person and machine client; a search field once installations have many of them.
  const declarerValues: FormSelectValue[] = [
    ...(machineDeliveryEnabled ? [{ key: 0, value: "machineClient", name: t("allMachineClients") }] : []),
    ...users.map((user, index) => ({ key: index + 1, value: `person:${user.id}`, name: user.fullName })),
    ...machineClients.map((client, index) => ({
      key: users.length + index + 1,
      value: `machineClient:${client.id}`,
      name: client.name,
    })),
  ];
  const mandateValues: FormSelectValue[] = mandates.map(mandate => ({
    key: mandate.id,
    value: String(mandate.id),
    name: localized(mandate.name),
  }));
  const stateValues: FormSelectValue[] = Object.values(PipelineRunStateFilter).map((state, index) => ({
    key: index,
    value: state,
    name: t(stateLabelKey[state]),
  }));

  const update = (field: keyof ProcessingRunFilterValues) => (value: string | number) =>
    onChange({ ...values, [field]: String(value) });

  const changeJobId = (text: string) => {
    setJobIdText(text);
    const jobId = parseJobId(text);
    if (jobId) {
      onJobIdEntered(jobId);
    }
  };

  const clearFilters = () => {
    setJobIdText("");
    onChange(emptyProcessingRunFilters);
  };

  return (
    <FilterBar
      searchPlaceholder="pipelineRunJobIdSearch"
      searchQuery={jobIdText}
      onSearchQueryChange={changeJobId}
      activeFilterCount={Object.values(values).filter(Boolean).length}
      onClearFilters={clearFilters}
      columns={5}
      dataCy="pipelineRunFilters">
      <FormDateTimePicker
        label="periodFrom"
        value={values.from}
        onChange={update("from")}
        dataCy="pipelineRunFilter-from"
      />
      <FormDateTimePicker label="periodTo" value={values.to} onChange={update("to")} dataCy="pipelineRunFilter-to" />
      <FormSelect
        label="processedBy"
        values={declarerValues}
        selected={values.declarer}
        onChange={update("declarer")}
        dataCy="pipelineRunFilter-declarer"
      />
      <FormSelect
        label="mandate"
        values={mandateValues}
        selected={values.mandateId}
        onChange={update("mandateId")}
        dataCy="pipelineRunFilter-mandate"
      />
      <FormSelect
        label="processingState"
        values={stateValues}
        selected={values.state}
        onChange={update("state")}
        dataCy="pipelineRunFilter-state"
      />
    </FilterBar>
  );
};
