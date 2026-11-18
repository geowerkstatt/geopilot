import { FC, ReactNode, Ref, useMemo, useState } from "react";
import { SxProps, useTheme } from "@mui/material";
import { DatePicker, DateTimePicker } from "@mui/x-date-pickers";
import { formatPickedDate, parseLocalDate } from "./localDateTime.ts";

interface DatePickerFieldProps {
  withTime: boolean;
  label: ReactNode;
  /** The local date as yyyy-mm-dd, or with its time as yyyy-mm-ddThh:mm; empty for none. */
  value: string;
  /** Called with the new value in the form of `value`, or an empty string once the field is cleared. */
  onChange: (value: string) => void;
  /** What the value becomes while a date is only partly typed; without it, the last complete date stays. */
  partlyTypedValue?: string;
  onBlur?: () => void;
  inputRef?: Ref<HTMLInputElement>;
  required?: boolean;
  disabled?: boolean;
  /** Overrides the error state the picker derives from its own checks. */
  error?: boolean;
  helperText?: ReactNode;
  sx?: SxProps;
  dataCy?: string;
}

/** A date, or a date and time, in the format of the language of the application, with a calendar to pick it from. */
export const DatePickerField: FC<DatePickerFieldProps> = ({
  withTime,
  label,
  value,
  onChange,
  partlyTypedValue,
  onBlur,
  inputRef,
  required,
  disabled,
  error,
  helperText,
  sx,
  dataCy,
}) => {
  const theme = useTheme();
  // The picker drops what is being typed whenever it gets another date object, so it gets back the one it picked
  // until the value changes from outside. The value may follow a pick a render late, as when it lives in the address.
  const [picked, setPicked] = useState<{ date: Date | null; value: string }>();
  const [lastValue, setLastValue] = useState(value);
  if (value !== lastValue) {
    setLastValue(value);
    if (value !== picked?.value) {
      setPicked(undefined);
    }
  }
  const valueDate = useMemo(() => parseLocalDate(value), [value]);
  const date = picked ? picked.date : valueDate;

  const change = (changed: Date | null) => {
    const changedValue = formatPickedDate(changed, withTime) ?? partlyTypedValue ?? value;
    setPicked({ date: changed, value: changedValue });
    if (changedValue !== value) {
      onChange(changedValue);
    }
  };

  // The slot props declare no data attributes, though the field passes them on to its element.
  const testProps = { "data-cy": dataCy };
  const pickerProps = {
    label,
    value: date,
    onChange: change,
    disabled,
    inputRef,
    // Below the sm breakpoint the calendar and the time do not fit side by side, so the picker opens as a dialog.
    desktopModeMediaQuery: `${theme.breakpoints.up("sm")} and (pointer: fine)`,
    slotProps: {
      // The dialogs of the app pad their paper, which leaves a narrow screen too little room for the calendar.
      mobilePaper: { sx: { padding: 0 } },
      field: { clearable: true },
      textField: { required, helperText, sx, onBlur, ...(error === undefined ? {} : { error }), ...testProps },
    },
  };

  return withTime ? <DateTimePicker {...pickerProps} ampm={false} /> : <DatePicker {...pickerProps} />;
};
