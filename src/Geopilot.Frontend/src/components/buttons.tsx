import { forwardRef } from "react";
import { useTranslation } from "react-i18next";
import { Button as MuiButton, IconButton as MuiIconButton, Tooltip } from "@mui/material";
import { ButtonProps as MuiButtonProps } from "@mui/material/Button";
import { IconButtonProps as MuiIconButtonProps } from "@mui/material/IconButton";

export interface ButtonProps extends MuiButtonProps {
  label?: string;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(({ label, ...props }, ref) => {
  const { t } = useTranslation();
  return (
    <MuiButton ref={ref} data-cy={label ? `${label}-button` : undefined} {...props}>
      {label && t(label)}
    </MuiButton>
  );
});

Button.displayName = "Button";

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
