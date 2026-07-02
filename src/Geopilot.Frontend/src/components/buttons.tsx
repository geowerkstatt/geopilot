import { forwardRef, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button, IconButton as MuiIconButton, Tooltip } from "@mui/material";
import { ButtonProps as MuiButtonProps } from "@mui/material/Button";
import { IconButtonProps as MuiIconButtonProps } from "@mui/material/IconButton";

export interface ButtonProps extends MuiButtonProps {
  onClick: () => void;
  label?: string;
  icon?: ReactNode;
}

export const BaseButton = forwardRef<HTMLButtonElement, ButtonProps>((props, ref) => {
  const { t } = useTranslation();
  return (
    <Button
      ref={ref}
      {...props}
      variant={props.variant ?? "outlined"}
      data-cy={props.label + "-button"}
      startIcon={props.icon}>
      {props.label && t(props.label)}
    </Button>
  );
});

BaseButton.displayName = "BaseButton";

export interface IconButtonProps extends MuiIconButtonProps {
  label?: string;
}

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(({ label, ...props }, ref) => {
  const { t } = useTranslation();
  const ariaLabel = label ? t(label) : props["aria-label"];
  const button = (
    <MuiIconButton ref={ref} data-cy={label ? `${label}-button` : undefined} {...props} aria-label={ariaLabel} />
  );

  if (!label) {
    return button;
  }

  return (
    <Tooltip title={t(label)}>
      {props.disabled ? <span style={{ display: "inline-flex" }}>{button}</span> : button}
    </Tooltip>
  );
});

IconButton.displayName = "IconButton";
