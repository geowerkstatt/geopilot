import { cloneElement, forwardRef, ReactElement } from "react";
import { useTranslation } from "react-i18next";
import { Button as MuiButton, IconButton as MuiIconButton, Tooltip } from "@mui/material";
import { ButtonProps as MuiButtonProps } from "@mui/material/Button";
import { IconButtonProps as MuiIconButtonProps } from "@mui/material/IconButton";
import { SvgIconProps } from "@mui/material/SvgIcon";
import { TooltipProps } from "@mui/material/Tooltip";

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
  icon: ReactElement<SvgIconProps>;
  label: string;
  tooltipPlacement?: TooltipProps["placement"];
}

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(
  ({ icon, label, size = "medium", tooltipPlacement, ...props }, ref) => {
    const { t } = useTranslation();
    const sizedIcon = cloneElement(icon, { fontSize: icon.props.fontSize ?? size });
    const button = (
      <MuiIconButton ref={ref} size={size} data-cy={`${label}-button`} {...props} aria-label={t(label)}>
        {sizedIcon}
      </MuiIconButton>
    );

    return (
      <Tooltip title={t(label)} placement={tooltipPlacement}>
        {props.disabled ? <span style={{ display: "inline-flex" }}>{button}</span> : button}
      </Tooltip>
    );
  },
);

IconButton.displayName = "IconButton";
