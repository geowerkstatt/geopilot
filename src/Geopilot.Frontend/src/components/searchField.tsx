import { FC } from "react";
import { useTranslation } from "react-i18next";
import CloseIcon from "@mui/icons-material/Close";
import SearchIcon from "@mui/icons-material/Search";
import { InputAdornment, SxProps, TextField } from "@mui/material";
import { IconButton } from "./buttons";

export interface SearchFieldProps {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  sx?: SxProps;
  dataCy?: string;
}

export const SearchField: FC<SearchFieldProps> = ({ value, onChange, placeholder, sx, dataCy }) => {
  const { t } = useTranslation();

  return (
    <TextField
      placeholder={t(placeholder)}
      value={value}
      onChange={event => onChange(event.target.value)}
      sx={sx}
      inputProps={{ "aria-label": t(placeholder) }}
      InputProps={{
        startAdornment: (
          <InputAdornment position="start">
            <SearchIcon fontSize="small" sx={{ color: "primary.main" }} />
          </InputAdornment>
        ),
        endAdornment: value ? (
          <InputAdornment position="end">
            <IconButton size="small" edge="end" label="clear" onClick={() => onChange("")}>
              <CloseIcon fontSize="small" />
            </IconButton>
          </InputAdornment>
        ) : undefined,
      }}
      data-cy={dataCy}
    />
  );
};
