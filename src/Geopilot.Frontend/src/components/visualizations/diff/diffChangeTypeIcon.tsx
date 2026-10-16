import { FC } from "react";
import AddCircleOutlineOutlinedIcon from "@mui/icons-material/AddCircleOutlineOutlined";
import ChangeCircleOutlinedIcon from "@mui/icons-material/ChangeCircleOutlined";
import RemoveCircleOutlineOutlinedIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import { DiffChangeType } from "../../../api/apiInterfaces";

const ICONS = {
  added: AddCircleOutlineOutlinedIcon,
  deleted: RemoveCircleOutlineOutlinedIcon,
  changed: ChangeCircleOutlinedIcon,
} satisfies Record<DiffChangeType, typeof ChangeCircleOutlinedIcon>;

interface DiffChangeTypeIconProps {
  changeType: DiffChangeType;
  /** `inherit` takes the colour of the text around it, so the icon greys out with a disabled button. */
  color?: "primary" | "inherit";
}

/**
 * The icon of a change type. The change types differ by shape only: orange and blue stand for the old and the new
 * state on the map, which a deleted or added value of an object that exists in both states would contradict.
 */
export const DiffChangeTypeIcon: FC<DiffChangeTypeIconProps> = ({ changeType, color = "primary" }) => {
  const Icon = ICONS[changeType];
  return <Icon fontSize="small" color={color} />;
};
