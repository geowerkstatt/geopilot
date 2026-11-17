import { format, isValid, parseISO } from "date-fns";

/** What a form holds for a date that is only partly typed, so that its check fails instead of the date going missing. */
export const partlyTypedDate = "partly typed";

/** The date a local date (yyyy-mm-dd) or local date and time (yyyy-mm-ddThh:mm) stands for, or null for none. */
export const parseLocalDate = (value: string): Date | null => {
  const date = parseISO(value);
  return isValid(date) ? date : null;
};

/**
 * A picked date as the local date yyyy-mm-dd, or with its time as yyyy-mm-ddThh:mm, an empty string for a cleared
 * field, or undefined for a date that is only partly typed.
 */
export const formatPickedDate = (date: Date | null, withTime: boolean): string | undefined => {
  if (date === null) {
    return "";
  }
  return isValid(date) ? format(date, withTime ? "yyyy-MM-dd'T'HH:mm" : "yyyy-MM-dd") : undefined;
};
