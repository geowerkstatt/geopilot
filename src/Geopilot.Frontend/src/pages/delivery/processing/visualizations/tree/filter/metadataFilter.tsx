import { KeyboardEvent, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Checkbox,
  FormControl,
  InputLabel,
  ListItemText,
  ListSubheader,
  MenuItem,
  Select,
  SelectChangeEvent,
  TextField,
} from "@mui/material";
import { MetadataAttribute } from "../treeNode";

/** A metadata dropdown shows an inline option search once it holds more than this many distinct values. */
const OPTION_SEARCH_THRESHOLD = 5;

interface MetadataFilterProps {
  attribute: MetadataAttribute;
  selected: string[];
  onChange: (key: string, selected: string[]) => void;
}

export const MetadataFilter = ({ attribute, selected, onChange }: MetadataFilterProps) => {
  const { t } = useTranslation();
  const [optionSearch, setOptionSearch] = useState("");
  const labelId = `metadata-filter-${attribute.key}`;
  const showOptionSearch = attribute.options.length > OPTION_SEARCH_THRESHOLD;

  const visibleOptions = useMemo(() => {
    const query = optionSearch.trim().toLowerCase();
    if (!query) return attribute.options;
    return attribute.options.filter(option => option.toLowerCase().includes(query));
  }, [attribute.options, optionSearch]);

  const handleChange = (event: SelectChangeEvent<string[]>) => {
    const { value } = event.target;
    onChange(attribute.key, typeof value === "string" ? value.split(",") : value);
  };

  return (
    <FormControl size="small" sx={{ minWidth: 200, maxWidth: 280 }}>
      <InputLabel id={labelId}>{attribute.key}</InputLabel>
      <Select
        labelId={labelId}
        multiple
        value={selected}
        label={attribute.key}
        onChange={handleChange}
        onClose={() => setOptionSearch("")}
        renderValue={values => values.join(", ")}
        MenuProps={{ autoFocus: false, PaperProps: { sx: { maxHeight: 360 } } }}
        data-cy={`metadata-filter-${attribute.key}`}>
        {showOptionSearch && (
          <ListSubheader sx={{ p: 1, lineHeight: "normal" }}>
            <TextField
              size="small"
              fullWidth
              autoFocus
              value={optionSearch}
              placeholder={t("treeVisualizationFilterSearch")}
              onChange={event => setOptionSearch(event.target.value)}
              onKeyDown={(event: KeyboardEvent) => {
                // Keep keystrokes inside the search field instead of triggering the Select type ahead.
                if (event.key !== "Escape") event.stopPropagation();
              }}
            />
          </ListSubheader>
        )}
        {visibleOptions.map(option => (
          <MenuItem key={option} value={option}>
            <Checkbox size="small" checked={selected.includes(option)} />
            <ListItemText primary={option} />
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );
};
