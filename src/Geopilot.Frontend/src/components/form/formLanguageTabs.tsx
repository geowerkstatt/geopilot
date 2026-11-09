import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Box, styled, ToggleButton, ToggleButtonGroup } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
import { Language, TranslationKey } from "../../appInterfaces";

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
  /** Prefix of the `data-cy` attributes, `<prefix>-tabs` on the row and `<prefix>-tab-<language>` on each button. */
  dataCy?: string;
  /** The accessible name of the row, which tells it apart from other language rows on the same page. */
  label?: TranslationKey;
}

/**
 * A row of language buttons that selects which language is currently being edited.
 * Controlled: the caller owns the active language and shares it across the localized inputs it drives.
 */
export const FormLanguageTabs: FC<FormLanguageTabsProps> = ({
  language,
  onLanguageChange,
  dataCy = "language",
  label = "inputLanguage",
}) => {
  const { t } = useTranslation();

  return (
    <StyledLanguageTabs
      exclusive
      value={language}
      onChange={(_, value: Language | null) => {
        if (value) {
          onLanguageChange(value);
        }
      }}
      aria-label={t(label)}
      data-cy={`${dataCy}-tabs`}>
      {Object.values(Language).map(lang => (
        <ToggleButton key={lang} value={lang} data-cy={`${dataCy}-tab-${lang}`} size="small">
          <Box sx={{ borderBottom: lang === language ? "1px solid" : "1px solid transparent" }}>
            {lang.toUpperCase()}
          </Box>
        </ToggleButton>
      ))}
    </StyledLanguageTabs>
  );
};
