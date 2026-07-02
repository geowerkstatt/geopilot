import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Autocomplete, TextField } from "@mui/material";
import { OverflowChips } from "./overflowChips";
import { MetadataAttribute } from "./treeNode";

interface MetadataFilterProps {
  attribute: MetadataAttribute;
  selected: string[];
  onChange: (key: string, selected: string[]) => void;
}

/**
 * A metadata filter: a multi-select autocomplete matching the app's form autocomplete styling. The
 * built-in type-ahead and clear control replace a bespoke option search and clear button.
 */
export const MetadataFilter = ({ attribute, selected, onChange }: MetadataFilterProps) => (
  <Autocomplete
    multiple
    size="small"
    disableCloseOnSelect
    options={attribute.options}
    value={selected}
    onChange={(_, value) => onChange(attribute.key, value)}
    popupIcon={<ExpandMoreIcon />}
    // Keep the control a single, fixed-height row that never changes with focus.
    sx={{
      width: "100%",
      "& .MuiAutocomplete-inputRoot": { flexWrap: "nowrap", overflow: "hidden" },
    }}
    // Show as many chips as fit on the row and collapse only the overflow into a "+N" chip (width-measured),
    // independent of focus so the field never grows or jumps while selecting.
    renderTags={(value, getTagProps) => <OverflowChips value={value} getTagProps={getTagProps} />}
    renderInput={params => <TextField {...params} label={attribute.key} />}
    data-cy={`metadata-filter-${attribute.key}`}
  />
);
