import { FormAutocompleteValue } from "./formAutocomplete";

export const mapToFormAutocompleteValue = <T extends { id: number }>(
  items: T[] | undefined,
  getDisplayText: (item: T) => string,
  getFullDisplayText: (item: T) => string,
): FormAutocompleteValue[] => {
  return (
    items?.map(item => ({
      id: item.id,
      displayText: getDisplayText(item),
      fullDisplayText: getFullDisplayText(item),
    })) || []
  );
};
