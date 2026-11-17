import { TranslationKey } from "../../appInterfaces";
import { FilterBar } from "../filterBar";
import { FormAutocomplete } from "../form/formAutocomplete";

export type FieldFilters<F extends string> = Partial<Record<F, string[]>>;

/** A filterable field together with its label (translation key) and its distinct value options. */
export interface FilterAttribute<F extends string> {
  field: F;
  label: TranslationKey;
  options: string[];
}

interface AttributeFilterBarProps<F extends string> {
  attributes: FilterAttribute<F>[];
  searchPlaceholder: TranslationKey;
  messageQuery: string;
  onMessageQueryChange: (value: string) => void;
  fieldFilters: FieldFilters<F>;
  onFieldFilterChange: (field: F, selected: string[]) => void;
  onClearFilters: () => void;
  forceMobileView?: boolean;
}

/** The filter bar of a visualization: a message search and, behind a toggle, a choice of values per attribute. */
export const AttributeFilterBar = <F extends string>({
  attributes,
  searchPlaceholder,
  messageQuery,
  onMessageQueryChange,
  fieldFilters,
  onFieldFilterChange,
  onClearFilters,
  forceMobileView,
}: AttributeFilterBarProps<F>) => (
  <FilterBar
    searchPlaceholder={searchPlaceholder}
    searchQuery={messageQuery}
    onSearchQueryChange={onMessageQueryChange}
    activeFilterCount={
      Object.values<string[] | undefined>(fieldFilters).filter(values => (values?.length ?? 0) > 0).length
    }
    onClearFilters={onClearFilters}
    collapsible
    forceMobileView={forceMobileView}>
    {attributes.length > 0 &&
      attributes.map(attribute => (
        <FormAutocomplete
          key={attribute.field}
          label={attribute.label}
          values={attribute.options}
          selected={fieldFilters[attribute.field] ?? []}
          onChange={value => onFieldFilterChange(attribute.field, value as string[])}
          dataCy={`metadata-filter-${attribute.field}`}
          sx={{ minWidth: "0" }}
        />
      ))}
  </FilterBar>
);
