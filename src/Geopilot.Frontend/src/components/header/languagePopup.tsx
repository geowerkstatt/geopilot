import { MouseEvent, useCallback, useEffect, useState } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { ListItemIcon, ListItemText, Menu, MenuItem } from "@mui/material";
import { Language } from "../../appInterfaces";
import { geopilotTheme } from "../../appTheme.ts";
import i18n from "../../i18n";
import { Button } from "../buttons";

/** Mirrors fallbackLng in i18n.js, so the button never announces a language the UI is not rendering. */
const fallbackLanguage = Language.EN;
const languages: string[] = Object.values(Language);

const isSupportedLanguage = (language: string): language is Language => languages.includes(language);

/** The language the UI is rendered in, known from the first render on through initAsync: false in i18n.js. */
const activeLanguage = (): Language => {
  const active = i18n.resolvedLanguage ?? i18n.language;
  return isSupportedLanguage(active) ? active : fallbackLanguage;
};

export function LanguagePopup() {
  const [selectedLanguage, setSelectedLanguage] = useState<Language>(activeLanguage);
  const [anchorEl, setAnchorEl] = useState<HTMLButtonElement>();
  const open = Boolean(anchorEl);

  const handleClick = useCallback((event: MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  }, []);

  const handleClose = useCallback(() => {
    setAnchorEl(undefined);
  }, []);

  useEffect(() => {
    const handleLanguageChange = () => setSelectedLanguage(activeLanguage());

    handleLanguageChange();
    i18n.on("languageChanged", handleLanguageChange);

    return () => {
      i18n.off("languageChanged", handleLanguageChange);
    };
  }, []);

  const onLanguageChanged = useCallback(
    (language: string) => {
      i18n.changeLanguage(language);
      handleClose();
    },
    [handleClose],
  );

  return (
    <>
      <Button
        variant="text"
        label={selectedLanguage.toUpperCase()}
        onClick={handleClick}
        endIcon={anchorEl ? <ExpandLessIcon /> : <ExpandMoreIcon />}
        sx={{ ...(open && { backgroundColor: geopilotTheme.palette.primary.states.hover }) }}
        data-cy="language-selector"
      />
      <Menu
        anchorEl={anchorEl}
        open={open}
        onClose={handleClose}
        sx={{ mt: 0.5 }}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ list: { sx: { py: 0 } } }}>
        {languages.map(language => {
          const isSelected = selectedLanguage === language;
          return (
            <MenuItem
              key={language}
              role="menuitemradio"
              aria-checked={isSelected}
              onClick={() => onLanguageChanged(language)}
              data-cy={`language-${language}`}
              sx={{ "&:hover": { backgroundColor: geopilotTheme.palette.primary.states.hover } }}>
              <ListItemIcon sx={{ minWidth: "20px" }}>{isSelected && <CheckIcon fontSize="small" />}</ListItemIcon>
              <ListItemText sx={{ textAlign: "right" }} slotProps={{ primary: { variant: "body2" } }}>
                {language.toUpperCase()}
              </ListItemText>
            </MenuItem>
          );
        })}
      </Menu>
    </>
  );
}
