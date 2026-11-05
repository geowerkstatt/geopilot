import { format, isValid } from "date-fns";

/**
 * A picked date as the local date and time yyyy-mm-ddThh:mm, an empty string for a cleared field, or undefined for a
 * date that is only partly typed.
 */
export const toLocalDateTime = (date: Date | null): string | undefined => {
  if (date === null) {
    return "";
  }
  return isValid(date) ? format(date, "yyyy-MM-dd'T'HH:mm") : undefined;
};
