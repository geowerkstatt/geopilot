import { FC } from "react";
import { Box, styled, ToggleButton, ToggleButtonGroup } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
import { Language } from "../../appInterfaces";

const StyledLanguageTabs = styled(ToggleButtonGroup)(({ theme }) => ({
  gap: theme.spacing(1),
  [`& .${toggleButtonClasses.root}`]: {
    padding: theme.spacing(1),
    paddingBottom: "0",
    paddingTop: "0",
    border: "none",
    borderRadius: theme.radius.default,
    [`&.${toggleButtonClasses.selected}, &.${toggleButtonClasses.selected}:hover`]: {
      backgroundColor: "transparent",
    },
  },
}));

interface FormLanguageTabsProps {
  language: Language;
  onLanguageChange: (language: Language) => void;
}

/**
 * A row of language buttons that selects which language is currently being edited.
 * Controlled: the caller owns the active language and shares it across the localized inputs it drives.
 */
export const FormLanguageTabs: FC<FormLanguageTabsProps> = ({ language, onLanguageChange }) => (
  <StyledLanguageTabs
    exclusive
    value={language}
    onChange={(_, value: Language | null) => {
      if (value) {
        onLanguageChange(value);
      }
    }}
    data-cy="language-tabs">
    {Object.values(Language).map(lang => (
      <ToggleButton key={lang} value={lang} data-cy={`language-tab-${lang}`} size="small">
        <Box sx={{ borderBottom: lang === language ? "1px solid" : "1px solid transparent" }}>{lang.toUpperCase()}</Box>
      </ToggleButton>
    ))}
  </StyledLanguageTabs>
);
