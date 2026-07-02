import { FC, useMemo, useRef, useState } from "react";
import AddIcon from "@mui/icons-material/Add";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import { Box, Stack, Tooltip } from "@mui/material";
import { styled } from "@mui/system";
import { DataGrid, DataGridProps, GridColDef, GridRenderCellParams, GridRowSelectionModel } from "@mui/x-data-grid";
import { GridRowId } from "@mui/x-data-grid/models/gridRows";
import { BaseButton } from "./buttons.tsx";

interface GeopilotDataGridProps extends DataGridProps {
  name: string;
  addLabel?: string;
  onSelect?: (id: GridRowId) => void;
}

const StyledDataGrid = styled(DataGrid)(({ theme }) => ({
  height: "100%",
  backgroundColor: "white",
  borderColor: theme.palette.primary.light,
}));

const OverflowTooltipCell: FC<{ text: string }> = ({ text }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);

  const handleMouseEnter = () => {
    const node = containerRef.current;
    if (node && node.scrollWidth > node.clientWidth) {
      setOpen(true);
    }
  };
  const handleMouseLeave = () => setOpen(false);

  return (
    <Tooltip
      title={text}
      open={open}
      disableInteractive
      slotProps={{ popper: { modifiers: [{ name: "offset", options: { offset: [-20, -16] } }] } }}>
      <Box
        ref={containerRef}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        sx={{
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
          width: "100%",
        }}>
        {text}
      </Box>
    </Tooltip>
  );
};

const BooleanTooltipCell: FC<{ value: boolean; label: string }> = ({ value, label }) => (
  <Tooltip title={label} disableInteractive>
    <Stack sx={{ alignItems: "center", justifyContent: "center" }}>
      {value ? <CheckIcon fontSize="small" aria-label={label} /> : <CloseIcon fontSize="small" aria-label={label} />}
    </Stack>
  </Tooltip>
);

// Only inject the tooltip wrapper for plain-text columns and booleans. Other built-in column types
// (actions, singleSelect, checkboxSelection) ship their own renderers we don't want to clobber.
const TEXT_COLUMN_TYPES: ReadonlySet<string | undefined> = new Set([undefined, "string"]);

const withCellTooltips = (columns: readonly GridColDef[]): GridColDef[] =>
  columns.map(column => {
    if (column.renderCell) {
      return column;
    }
    if (column.type === "boolean") {
      return {
        ...column,
        renderCell: (params: GridRenderCellParams) => (
          <BooleanTooltipCell value={Boolean(params.value)} label={params.formattedValue?.toString() ?? ""} />
        ),
      };
    }
    if (!TEXT_COLUMN_TYPES.has(column.type)) {
      return column;
    }
    return {
      ...column,
      renderCell: (params: GridRenderCellParams) => {
        const value = params.formattedValue ?? params.value;
        const text = value == null ? "" : String(value);
        return <OverflowTooltipCell text={text} />;
      },
    };
  });

const GeopilotDataGrid: FC<GeopilotDataGridProps> = props => {
  const tooltipColumns = useMemo(() => withCellTooltips(props.columns), [props.columns]);
  const handleRowSelection = (newRowSelectionModel: GridRowSelectionModel) => {
    if (props.onSelect && newRowSelectionModel.length > 0) {
      props.onSelect(newRowSelectionModel[0]);
    }
  };

  const handleAddClick = () => {
    if (props.onSelect) {
      props.onSelect(0);
    }
  };

  return props.addLabel && props.onSelect ? (
    <Stack sx={{ height: "100%" }}>
      <Box sx={{ flex: "0" }}>
        <BaseButton icon={<AddIcon />} onClick={handleAddClick} label={props.addLabel} />
      </Box>
      <StyledDataGrid
        data-cy={`${props.name}-grid`}
        sx={{ flex: "1" }}
        autoPageSize
        disableColumnSelector
        hideFooterSelectedRowCount
        onRowSelectionModelChange={handleRowSelection}
        {...props}
        columns={tooltipColumns}
      />
    </Stack>
  ) : (
    <StyledDataGrid
      data-cy={`${props.name}-grid`}
      autoPageSize
      disableColumnSelector
      hideFooterSelectedRowCount
      onRowSelectionModelChange={handleRowSelection}
      {...props}
      columns={tooltipColumns}
    />
  );
};

export default GeopilotDataGrid;
