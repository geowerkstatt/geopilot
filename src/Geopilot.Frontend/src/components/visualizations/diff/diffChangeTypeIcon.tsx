import { FC } from "react";
import { useTranslation } from "react-i18next";
import AddCircleOutlineOutlinedIcon from "@mui/icons-material/AddCircleOutlineOutlined";
import RemoveCircleOutlineOutlinedIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import SwapHorizontalCircleOutlinedIcon from "@mui/icons-material/SwapHorizontalCircleOutlined";
import { DiffChangeType } from "../../../api/apiInterfaces";
import { DIFF_CHANGE_TYPE_COLORS, DIFF_CHANGE_TYPE_LABEL_KEYS } from "./diffObjects";

const ICONS = {
  added: AddCircleOutlineOutlinedIcon,
  deleted: RemoveCircleOutlineOutlinedIcon,
  changed: SwapHorizontalCircleOutlinedIcon,
} satisfies Record<DiffChangeType, typeof SwapHorizontalCircleOutlinedIcon>;

// The changed icon in the blue of the map; the summary text of a changed property stays grey.
const ICON_COLORS: Record<DiffChangeType, string> = { ...DIFF_CHANGE_TYPE_COLORS, changed: "diff.changed" };

interface DiffChangeTypeIconProps {
  changeType: DiffChangeType;
}

export const DiffChangeTypeIcon: FC<DiffChangeTypeIconProps> = ({ changeType }) => {
  const { t } = useTranslation();
  const Icon = ICONS[changeType];
  return (
    <Icon
      fontSize="small"
      titleAccess={t(DIFF_CHANGE_TYPE_LABEL_KEYS[changeType])}
      sx={{ color: ICON_COLORS[changeType] }}
    />
  );
};
