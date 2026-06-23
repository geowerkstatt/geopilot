import { ButtonProps as MuiButtonProps } from "@mui/material/Button";
import * as React from "react";
import { forwardRef } from "react";
import { Button } from "@mui/material";
import { useTranslation } from "react-i18next";

export interface ButtonProps extends MuiButtonProps {
  onClick: () => void;
  label?: string;
  icon?: React.ReactNode;
}

export const BaseButton = forwardRef<HTMLButtonElement, ButtonProps>((props, ref) => {
  const { t } = useTranslation();
  return (
    <Button
      ref={ref}
      {...props}
      variant={props.variant ?? "contained"}
      color={props.color ?? "primary"}
      data-cy={props.label + "-button"}
      startIcon={props.icon}>
      {props.label && t(props.label)}
    </Button>
  );
});
