import { FC } from "react";
import { Box, Chip } from "@mui/material";
import { px2rem } from "../../appTheme";

interface SelectedChipsProps {
  /** The selected values, in order. */
  values: string[];
  /** Receives the index of the chip whose delete icon was clicked. */
  onDelete: (index: number) => void;
  dataCy?: string;
}

/**
 * Renders every selected value as a removable chip, wrapping onto as many rows as needed, so the whole selection
 * stays visible. OverflowChips is the counterpart for fields that keep their chips on the input row and collapse
 * whatever does not fit into a trailing "+N".
 */
export const SelectedChips: FC<SelectedChipsProps> = ({ values, onDelete, dataCy }) => (
  <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }} data-cy={dataCy}>
    {values.map((value, index) => (
      <Chip
        key={`${value}-${index}`}
        size="small"
        label={value}
        onDelete={() => onDelete(index)}
        sx={{ "& .MuiChip-deleteIcon": { fontSize: px2rem(18) } }}
      />
    ))}
  </Box>
);
