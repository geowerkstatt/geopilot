import { LocalizedText } from "../../../api/apiInterfaces";
import { DeliveredMetadataField, MetadataFieldType } from "../../../api/generated";

const isString = (value: unknown): value is string => typeof value === "string";
const isNumber = (value: unknown): value is number => typeof value === "number";
const isStringList = (value: unknown): value is string[] => Array.isArray(value) && value.every(isString);

// The date is built from its parts: new Date("2026-11-04") is UTC midnight and shows the day before west of UTC.
const formatDate = (value: string): string => {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day).toLocaleDateString();
};

/** The text a stored metadata value shows as, or undefined when the field was not filled in. */
export const formatMetadataValue = (
  field: DeliveredMetadataField,
  localized: (text: LocalizedText, fallback?: string) => string,
): string | undefined => {
  const value = field.value;
  if (value == null) {
    return undefined;
  }
  switch (field.type) {
    case MetadataFieldType.TextList:
      return isStringList(value) ? value.join(", ") : undefined;
    case MetadataFieldType.Selection: {
      const option = field.options?.find(candidate => candidate.id === value);
      return option ? localized(option.title, option.id) : isString(value) ? value : undefined;
    }
    case MetadataFieldType.Integer:
    case MetadataFieldType.Decimal:
      return isNumber(value) ? value.toLocaleString() : undefined;
    case MetadataFieldType.Date:
      return isString(value) ? formatDate(value) : undefined;
    default:
      return isString(value) ? value : undefined;
  }
};
