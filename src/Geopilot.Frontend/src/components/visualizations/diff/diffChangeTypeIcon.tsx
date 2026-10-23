import { FC } from "react";
import { useTranslation } from "react-i18next";
import AddCircleOutlineOutlinedIcon from "@mui/icons-material/AddCircleOutlineOutlined";
import RemoveCircleOutlineOutlinedIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import SwapHorizontalCircleOutlinedIcon from "@mui/icons-material/SwapHorizontalCircleOutlined";
import { DiffChangeType } from "../../../api/apiInterfaces";
import { DIFF_CHANGE_TYPE_LABEL_KEYS } from "./diffObjects";

const ICONS = {
  added: AddCircleOutlineOutlinedIcon,
  deleted: RemoveCircleOutlineOutlinedIcon,
  changed: SwapHorizontalCircleOutlinedIcon,
} satisfies Record<DiffChangeType, typeof SwapHorizontalCircleOutlinedIcon>;

const COLORS: Record<DiffChangeType, string> = {
  added: "diff.added",
  deleted: "diff.deleted",
  changed: "text.secondary",
};

interface DiffChangeTypeIconProps {
  changeType: DiffChangeType;
}

/** The icon of a change type, green for added, red for deleted, grey for changed, with the change type as its accessible name. */
export const DiffChangeTypeIcon: FC<DiffChangeTypeIconProps> = ({ changeType }) => {
  const { t } = useTranslation();
  const Icon = ICONS[changeType];
  return (
    <Icon
      fontSize="small"
      titleAccess={t(DIFF_CHANGE_TYPE_LABEL_KEYS[changeType])}
      sx={{ color: COLORS[changeType] }}
    />
  );
};
