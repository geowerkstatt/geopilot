import { useEffect, useState } from "react";
import { MachineClient, Mandate, User } from "../api/generated";
import { useCapabilities } from "../components/capabilities/capabilitiesInterface.ts";
import useFetch from "./useFetch.ts";
import { useLocalized } from "./useLocalized.ts";

/** A choice in a filter of the administration, with the value the filter puts into the address. */
export interface FilterOption {
  id: number;
  value: string;
  name: string;
}

export const toFilterOptions = (entries: Omit<FilterOption, "id">[]): FilterOption[] =>
  entries.map((entry, index) => ({ ...entry, id: index }));

/**
 * The choices the filters of the administration lists offer for who processed or delivered, as `user:<id>` or
 * `machineClient:<id>`, and for the mandate, by its id. Machine clients only where machine delivery is offered.
 */
export const useFilterOptions = () => {
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const [users, setUsers] = useState<User[]>([]);
  const [machineClients, setMachineClients] = useState<MachineClient[]>([]);
  const [mandates, setMandates] = useState<Mandate[]>([]);

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

  return {
    userOrClientOptions: toFilterOptions([
      ...users.map(user => ({ value: `user:${user.id}`, name: user.fullName })),
      ...machineClients.map(client => ({ value: `machineClient:${client.id}`, name: client.name })),
    ]),
    mandateOptions: toFilterOptions(
      mandates.map(mandate => ({ value: String(mandate.id), name: localized(mandate.name) })),
    ),
  };
};
