import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Dialog, DialogActions, DialogContent, DialogContentText } from "@mui/material";
import { Button } from "../buttons";
import { PromptContext } from "./promptContext";

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
            label={action.label}
            onClick={() => {
              !!action.action && action.action();
              closePrompt();
            }}
            startIcon={action.icon}
            color={action.color}
            variant={action.variant}
            disabled={action.disabled === true}
            data-cy={"prompt-button-" + action.label}
          />
        ))}
      </DialogActions>
    </Dialog>
  );
};
