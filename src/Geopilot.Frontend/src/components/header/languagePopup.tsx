import { MouseEvent, useCallback, useEffect, useState } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { ListItemIcon, ListItemText, Menu, MenuItem } from "@mui/material";
import { Language } from "../../appInterfaces";
import { geopilotTheme } from "../../appTheme.ts";
import i18n from "../../i18n";
import { Button } from "../buttons";

const defaultLanguage = Language.DE;
const languages: string[] = Object.values(Language);

export function LanguagePopup() {
  const [selectedLanguage, setSelectedLanguage] = useState<Language>(defaultLanguage);
  const [anchorEl, setAnchorEl] = useState<HTMLButtonElement>();
  const open = Boolean(anchorEl);

  const handleClick = useCallback((event: MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  }, []);

  const handleClose = useCallback(() => {
    setAnchorEl(undefined);
  }, []);

  useEffect(() => {
    const handleLanguageChange = () => {
      const languageIndex = languages.indexOf(i18n.language);
      setSelectedLanguage(languageIndex !== -1 ? (languages[languageIndex] as Language) : defaultLanguage);
    };
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
        MenuListProps={{ sx: { py: 0 } }}>
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
              <ListItemText primaryTypographyProps={{ variant: "body2" }} sx={{ textAlign: "right" }}>
                {language.toUpperCase()}
              </ListItemText>
            </MenuItem>
          );
        })}
      </Menu>
    </>
  );
}
