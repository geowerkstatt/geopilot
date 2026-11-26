import { FC, useState } from "react";
import { useTranslation } from "react-i18next";
import { Theme, useMediaQuery } from "@mui/material";
import { ReactionKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { FilterBar } from "../../../components/filterBar.tsx";
import { FormAutocomplete, FormInput, FormValueType } from "../../../components/form/form.ts";
import { FilterOption, toFilterOptions, useFilterOptions } from "../../../hooks/useFilterOptions.ts";
import { parseAsId } from "../listQuery.ts";
import { DeliveryFilterValues, emptyDeliveryFilters } from "./deliveryQuery.ts";

interface DeliveryFiltersProps {
  values: DeliveryFilterValues;
  /** The beginning of the id the list is narrowed to, or an empty string. */
  idPrefix: string;
  onChange: (values: DeliveryFilterValues, idPrefix: string) => void;
}

type ListField = "deliveredBy" | "mandateIds" | "failed";

/** The filters of the list of deliveries: period, who delivered, mandate and failed reactions, plus a search by id. */
export const DeliveryFilters: FC<DeliveryFiltersProps> = ({ values, idPrefix, onChange }) => {
  const { t } = useTranslation();
  const { userOrClientOptions, mandateOptions } = useFilterOptions();
  // The field shows what was typed, while the list stays narrowed to what that text left in the address; only digits
  // that can begin an id narrow it. Once the address changes otherwise, as through the menu, the field follows it.
  const [typed, setTyped] = useState({ text: idPrefix, idPrefix });
  const searchText = typed.idPrefix === idPrefix ? typed.text : idPrefix;
  const narrow = useMediaQuery((theme: Theme) => theme.breakpoints.down("md"), { noSsr: true });

  // Named like the columns that show the failure.
  const failedOptions = toFilterOptions([
    { value: ReactionKind.Transfer, name: t("forwarding") },
    { value: ReactionKind.Notification, name: t("notifications") },
  ]);

  const changeSearch = (text: string) => {
    // An empty field shows all ids again; other text that is no id leaves the list as it is.
    const searchedIdPrefix = text === "" ? "" : parseAsId.parse(text);
    setTyped({ text, idPrefix: searchedIdPrefix ?? idPrefix });
    if (searchedIdPrefix !== null) {
      onChange(values, searchedIdPrefix);
    }
  };

  const clearFilters = () => {
    setTyped({ text: "", idPrefix: "" });
    onChange(emptyDeliveryFilters, "");
  };

  const changeTime = (field: "from" | "to") => (value: string) => onChange({ ...values, [field]: value }, idPrefix);

  const listFilter = (field: ListField, label: TranslationKey, options: FilterOption[], dataCy: string) => (
    <FormAutocomplete<FilterOption>
      label={label}
      values={options}
      selected={options.filter(option => values[field].includes(option.value))}
      valueFormatter={option => ({ id: option.id, primaryText: option.name })}
      onChange={selected => onChange({ ...values, [field]: selected.map(option => option.value) }, idPrefix)}
      dataCy={dataCy}
      sx={{ minWidth: 0 }}
    />
  );

  return (
    <FilterBar
      searchPlaceholder="deliveryIdSearch"
      searchQuery={searchText}
      onSearchQueryChange={changeSearch}
      activeFilterCount={Object.values(values).filter(value => value.length > 0).length}
      onClearFilters={clearFilters}
      collapsible={narrow}
      columns={5}
      dataCy="deliveryFilters">
      <FormInput
        type={FormValueType.DateTime}
        label="periodFrom"
        value={values.from}
        onChange={changeTime("from")}
        dataCy="deliveryFilter-from"
      />
      <FormInput
        type={FormValueType.DateTime}
        label="periodTo"
        value={values.to}
        onChange={changeTime("to")}
        dataCy="deliveryFilter-to"
      />
      {listFilter("deliveredBy", "deliveredBy", userOrClientOptions, "deliveryFilter-deliveredBy")}
      {listFilter("mandateIds", "mandate", mandateOptions, "deliveryFilter-mandate")}
      {listFilter("failed", "failedReactions", failedOptions, "deliveryFilter-failed")}
    </FilterBar>
  );
};
