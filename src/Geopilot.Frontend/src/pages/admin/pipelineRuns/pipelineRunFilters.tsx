import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Stack } from "@mui/material";
import { MachineClient, Mandate, PipelineRunStateFilter, User } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme.ts";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { FormInput, FormSelect, FormValueType } from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { parseJobId, PipelineRunFilterValues } from "./pipelineRunQuery.ts";

interface PipelineRunFiltersProps {
  values: PipelineRunFilterValues;
  onChange: (values: PipelineRunFilterValues) => void;
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

const fieldWidth = { minWidth: px2rem(180), flex: "1 1 0" };

/** The filters of the list of pipeline runs: period, who started the run, mandate and state, plus a job id lookup. */
export const PipelineRunFilters: FC<PipelineRunFiltersProps> = ({ values, onChange, onJobIdEntered }) => {
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

  const update = (field: keyof PipelineRunFilterValues) => (value: string | number) =>
    onChange({ ...values, [field]: String(value) });

  const changeJobId = (text: string) => {
    setJobIdText(text);
    const jobId = parseJobId(text);
    if (jobId) {
      onJobIdEntered(jobId);
    }
  };

  return (
    <Stack direction={{ xs: "column", md: "row" }} sx={{ gap: 2, flexWrap: "wrap" }} data-cy="pipelineRunFilters">
      <FormInput
        label="periodFrom"
        type={FormValueType.Date}
        value={values.from}
        onChange={update("from")}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-from"
      />
      <FormInput
        label="periodTo"
        type={FormValueType.Date}
        value={values.to}
        onChange={update("to")}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-to"
      />
      <FormSelect
        label="processedBy"
        values={declarerValues}
        selected={values.declarer}
        onChange={update("declarer")}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-declarer"
      />
      <FormSelect
        label="mandate"
        values={mandateValues}
        selected={values.mandateId}
        onChange={update("mandateId")}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-mandate"
      />
      <FormSelect
        label="state"
        values={stateValues}
        selected={values.state}
        onChange={update("state")}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-state"
      />
      <FormInput
        label="jobId"
        value={jobIdText}
        onChange={changeJobId}
        sx={fieldWidth}
        dataCy="pipelineRunFilter-jobId"
      />
    </Stack>
  );
};
