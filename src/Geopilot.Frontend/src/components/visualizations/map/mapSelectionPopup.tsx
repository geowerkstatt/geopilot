import { FC } from "react";
import { useTranslation } from "react-i18next";
import { List, ListItemButton, ListItemText, Paper } from "@mui/material";

/** One error offered in the selection popup: the message to show and the feature id it selects. */
export interface SelectionEntry {
  id: string;
  text: string;
}

interface MapSelectionPopupProps {
  /** The errors the clicked marker stands for, listed as one row each. */
  entries: SelectionEntry[];
  /** Called with the feature id of the row the user picked. */
  onSelect: (featureId: string) => void;
}

const OUTER_ARROW_SIZE = 8;
const INNER_ARROW_SIZE = 7;

/**
 * The popup anchored to a clicked marker, listing every error it stands for so each of them can be selected.
 * Errors sharing the same coordinate stay in one cluster marker at every zoom level, so the list is the only
 * way to reach them on the map.
 */
export const MapSelectionPopup: FC<MapSelectionPopupProps> = ({ entries, onSelect }) => {
  const { t } = useTranslation();
  const label = (entry: SelectionEntry) => entry.text || t("mapFeatureWithoutMessage");

  return (
    <Paper
      elevation={0}
      sx={{
        position: "relative",
        maxWidth: "300px",
        backgroundColor: theme => theme.palette.background.content,
        border: theme => `1px solid ${theme.palette.primary.light}`,
        // The overlay hosting the popup is click-through, the list rows need the clicks.
        pointerEvents: "auto",
        // The arrow pointing at the marker, as two stacked triangles: the outer one carries the border
        // colour, the smaller inner one covers its face and leaves only the border edge visible.
        "&::before, &::after": {
          content: '""',
          position: "absolute",
          left: "50%",
          transform: "translateX(-50%)",
        },
        "&::before": {
          top: "100%",
          borderLeft: `${OUTER_ARROW_SIZE}px solid transparent`,
          borderRight: `${OUTER_ARROW_SIZE}px solid transparent`,
          borderTop: theme => `${OUTER_ARROW_SIZE}px solid ${theme.palette.primary.light}`,
        },
        "&::after": {
          top: "calc(100% - 1px)",
          borderLeft: `${INNER_ARROW_SIZE}px solid transparent`,
          borderRight: `${INNER_ARROW_SIZE}px solid transparent`,
          borderTop: theme => `${INNER_ARROW_SIZE}px solid ${theme.palette.background.content}`,
        },
      }}>
      <List
        dense
        disablePadding
        sx={{ maxHeight: "180px", overflowY: "auto", borderRadius: theme => theme.radius.default }}>
        {entries.map((entry, index) => (
          <ListItemButton
            key={entry.id}
            disableGutters
            divider={index < entries.length - 1}
            onClick={() => onSelect(entry.id)}
            sx={{ py: 1, px: 1.5 }}>
            <ListItemText primary={label(entry)} slotProps={{ primary: { variant: "body2" } }} />
          </ListItemButton>
        ))}
      </List>
    </Paper>
  );
};
