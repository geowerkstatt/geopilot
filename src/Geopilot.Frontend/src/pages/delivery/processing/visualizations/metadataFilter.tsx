import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Autocomplete, Chip, TextField } from "@mui/material";
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
    // Always collapse to "first chip + N", even while focused, so the field never grows or jumps while
    // selecting. MUI's built-in limitTags only collapses on blur, which caused the expand/collapse jump.
    renderTags={(value, getTagProps) => {
      const [first, ...rest] = value;
      const { key, ...firstProps } = getTagProps({ index: 0 });
      return (
        <>
          <Chip key={key} size="small" label={first} sx={{ maxWidth: 180 }} {...firstProps} />
          {rest.length > 0 && <Chip key="more" size="small" label={`+${rest.length}`} />}
        </>
      );
    }}
    renderInput={params => <TextField {...params} label={attribute.key} />}
    data-cy={`metadata-filter-${attribute.key}`}
  />
);
