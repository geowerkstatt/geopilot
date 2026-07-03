import { FC, useContext } from "react";
import { ButtonProps } from "@mui/material/Button";
import { Button } from "../../components/buttons";
import { PromptContext } from "../../components/prompt/promptContext";
import { PromptAction } from "../../components/prompt/promptInterfaces";
import { DeliveryContext } from "./deliveryContext";

export const DeliveryRestartButton: FC<Pick<ButtonProps, "sx">> = ({ sx }) => {
  const { resetDelivery, selectedFiles } = useContext(DeliveryContext);
  const { showPrompt } = useContext(PromptContext);

  const promptToRestart = () => {
    const promptActions: PromptAction[] = [
      { label: "cancel", action: () => {} },
      {
        label: "restart",
        variant: "contained",
        action: () => resetDelivery(),
      },
    ];
    showPrompt("restartPrompt", promptActions);
  };

  return selectedFiles.length > 0 && <Button label="restart" variant="text" onClick={promptToRestart} sx={sx} />;
};
