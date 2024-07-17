import { FC } from "react";
import { Coordinate } from "../../AppInterfaces.ts";
import { useTranslation } from "react-i18next";
import { Box, TextField } from "@mui/material";

interface SpatialExtentPopoverContentProps {
  spatialExtent: Coordinate[];
  onChange: (spatialExtent: Coordinate[]) => void;
}

export const DataGridSpatialExtentPopoverContent: FC<SpatialExtentPopoverContentProps> = ({
  spatialExtent,
  onChange,
}) => {
  const { t } = useTranslation();

  return (
    <div className="spatial-extent-popover">
      <h6>{t("spatialExtent")}</h6>
      <div className="spatial-extent-coordinate-row">
        <Box
          sx={{
            margin: "10px 20px 10px 10px",
            width: "20px",
            height: "20px",
            border: `1px solid black`,
            borderTopWidth: 0,
            borderRightWidth: 0,
          }}
        />
        <TextField
          type="number"
          size="small"
          sx={{ marginRight: "20px" }}
          label={t("longitude")}
          defaultValue={spatialExtent[0]?.x || null}
          onChange={e => onChange([{ x: +e.target.value, y: spatialExtent[0].y }, spatialExtent[1]])}
        />
        <TextField
          type="number"
          size="small"
          label={t("latitude")}
          defaultValue={spatialExtent[0]?.y || null}
          onChange={e => onChange([{ x: spatialExtent[0].x, y: +e.target.value }, spatialExtent[1]])}
        />
      </div>
      <div className="spatial-extent-coordinate-row">
        <Box
          sx={{
            margin: "10px 20px 10px 10px",
            width: "20px",
            height: "20px",
            border: `1px solid black`,
            borderLeftWidth: 0,
            borderBottomWidth: 0,
          }}
        />
        <TextField
          type="number"
          size="small"
          sx={{ marginRight: "20px" }}
          label={t("longitude")}
          defaultValue={spatialExtent[1]?.x || null}
          onChange={e => onChange([spatialExtent[0], { x: +e.target.value, y: spatialExtent[1].y }])}
        />
        <TextField
          type="number"
          size="small"
          label={t("latitude")}
          defaultValue={spatialExtent[1]?.y || null}
          onChange={e => onChange([spatialExtent[0], { x: spatialExtent[1].x, y: +e.target.value }])}
        />
      </div>
    </div>
  );
};
