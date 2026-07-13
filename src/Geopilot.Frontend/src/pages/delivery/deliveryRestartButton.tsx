import { forwardRef, useContext } from "react";
import { ButtonProps as MuiButtonProps } from "@mui/material/Button/Button";
import { Button } from "../../components/buttons";
import { PromptContext } from "../../components/prompt/promptContext";
import { PromptAction } from "../../components/prompt/promptInterfaces";
import { DeliveryContext } from "./deliveryContext";

interface DeliveryRestartButtonProps extends MuiButtonProps {
  immediate?: boolean;
}

export const DeliveryRestartButton = forwardRef<HTMLButtonElement, DeliveryRestartButtonProps>(
  ({ immediate, ...props }, ref) => {
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

    return (
      selectedFiles.length > 0 && (
        <Button
          ref={ref}
          label="restart"
          variant="text"
          onClick={immediate ? resetDelivery : promptToRestart}
          {...props}
        />
      )
    );
  },
);
