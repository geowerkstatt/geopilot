import { useContext } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogContentText } from "@mui/material";
import { PromptContext } from "./promptContext";
import { useTranslation } from "react-i18next";

export const Prompt = () => {
  const { t } = useTranslation();
  const { promptIsOpen, message, actions, closePrompt } = useContext(PromptContext);
  return (
    <Dialog open={promptIsOpen} data-cy="prompt">
      <DialogContent>
        <DialogContentText>{t(message as string)}</DialogContentText>
      </DialogContent>
      <DialogActions>
        {actions?.map((action, index) => (
          <Button
            key={index}
            onClick={() => {
              !!action.action && action.action();
              closePrompt();
            }}
            startIcon={action.icon}
            color={action.color ? action.color : "primary"}
            variant={action.variant ? action.variant : "outlined"}
            disabled={action.disabled === true}
            data-cy={"prompt-button-" + action.label}>
            {t(action.label)}
          </Button>
        ))}
      </DialogActions>
    </Dialog>
  );
};
