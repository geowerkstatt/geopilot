import { GridRenderEditCellParams, useGridApiContext } from "@mui/x-data-grid";
import { IconButton, Popover, Tooltip } from "@mui/material";
import { GridBaseColDef } from "@mui/x-data-grid/internals";
import { GridColDef } from "../adminGrid/AdminGridInterfaces.ts";
import PublicOutlinedIcon from "@mui/icons-material/PublicOutlined";
import { MouseEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { DataGridSpatialExtentPopoverContent } from "./DataGridSpatialExtentPopoverContent.tsx";
import { Coordinate } from "../../AppInterfaces.ts";

export const IsGridSpatialExtentColDef = (columnDef: GridColDef) =>
  columnDef.type === "custom" && columnDef.field === "spatialExtent";

export const TransformToSpatialExtentColumn = (columnDef: GridBaseColDef) => {
  columnDef.renderCell = () => (
    <IconButton size="small" color="inherit" disabled>
      <PublicOutlinedIcon fontSize="small" />
    </IconButton>
  );
  columnDef.renderEditCell = params => <DataGridSpatialExtentColumn params={params} />;
};

interface DataGridSpatialExtentColumnProps {
  params: GridRenderEditCellParams;
}

const DataGridSpatialExtentColumn = ({ params }: DataGridSpatialExtentColumnProps) => {
  const apiRef = useGridApiContext();
  const { t } = useTranslation();
  const [popoverAnchor, setPopoverAnchor] = useState<HTMLButtonElement | null>(null);
  const [spatialExtent, setSpatialExtent] = useState<Coordinate[]>(params.value);

  return (
    <>
      <Tooltip title={t("spatialExtent")}>
        <IconButton
          sx={{ margin: "10px" }}
          size="small"
          color="inherit"
          onClick={(event: MouseEvent<HTMLButtonElement>) => {
            setPopoverAnchor(event.currentTarget);
          }}>
          <PublicOutlinedIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Popover
        id={"spatial-extent-popover"}
        open={!!popoverAnchor}
        anchorEl={popoverAnchor}
        onClose={() => {
          apiRef.current.setEditCellValue({
            id: params.id,
            field: "spatialExtent",
            value: spatialExtent,
          });
          setPopoverAnchor(null);
        }}
        anchorOrigin={{
          vertical: "bottom",
          horizontal: "right",
        }}
        transformOrigin={{
          vertical: "top",
          horizontal: "right",
        }}>
        <DataGridSpatialExtentPopoverContent
          spatialExtent={spatialExtent}
          onChange={(spatialExtent: Coordinate[]) => {
            setSpatialExtent(spatialExtent);
          }}
        />
      </Popover>
    </>
  );
};
