import { FC } from "react";
import { useTranslation } from "react-i18next";
import { DateTimePicker } from "@mui/x-date-pickers";
import { isValid } from "date-fns";
import { TranslationKey } from "../../appInterfaces";
import { toLocalDateTime } from "./localDateTime.ts";

interface FormDateTimePickerProps {
  label: TranslationKey;
  /** The local date and time as yyyy-mm-ddThh:mm, empty for none. */
  value: string;
  /** Called with the new local date and time, or an empty string once the field is cleared. */
  onChange: (value: string) => void;
  dataCy?: string;
}

/** A date and time in the format of the language of the application, with a calendar to pick it from. */
export const FormDateTimePicker: FC<FormDateTimePickerProps> = ({ label, value, onChange, dataCy }) => {
  const { t } = useTranslation();
  const date = value ? new Date(value) : null;
  // The slot props declare no data attributes, though the field passes them on to its element.
  const testProps = { "data-cy": dataCy };

  return (
    <DateTimePicker
      label={t(label)}
      value={date && isValid(date) ? date : null}
      // A half typed date keeps the last complete one.
      onChange={changed => {
        const localDateTime = toLocalDateTime(changed);
        if (localDateTime !== undefined) {
          onChange(localDateTime);
        }
      }}
      ampm={false}
      slotProps={{ field: { clearable: true }, textField: { ...testProps } }}
    />
  );
};
