import { FC, ReactNode, useContext, useEffect } from "react";
import { ClickAwayListener, Popper } from "@mui/material";
import { SxProps, Theme } from "@mui/material/styles";
import { MapPanelContainerContext } from "./mapVisualizationContext";

interface MapPanelPopperProps {
  id: string;
  open: boolean;
  /** The button that toggles the panel. Clicks on it are left to the button. */
  anchorEl: HTMLElement | null;
  onClose: () => void;
  sx?: SxProps<Theme>;
  children: ReactNode;
}

/**
 * A panel beside a map button, bottom-aligned with it. Closes on Escape, returning the focus to the
 * button, and on a click anywhere else, so two panels never overlap.
 */
export const MapPanelPopper: FC<MapPanelPopperProps> = ({ id, open, anchorEl, onClose, sx, children }) => {
  const container = useContext(MapPanelContainerContext);
  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      onClose();
      anchorEl?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [open, anchorEl, onClose]);

  return (
    <ClickAwayListener
      onClickAway={event => {
        if (!(event.target instanceof Node && anchorEl?.contains(event.target))) onClose();
      }}>
      <Popper
        id={id}
        open={open}
        anchorEl={anchorEl}
        placement="left-end"
        container={container}
        modifiers={[{ name: "offset", options: { offset: [0, 8] } }]}
        sx={[{ zIndex: 1 }, ...(Array.isArray(sx) ? sx : [sx])]}>
        {children}
      </Popper>
    </ClickAwayListener>
  );
};
