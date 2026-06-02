import { DataGrid, DataGridProps, GridRowSelectionModel } from "@mui/x-data-grid";
import { FC } from "react";
import { Box, Stack } from "@mui/material";
import { styled } from "@mui/system";
import { BaseButton } from "./buttons.tsx";
import AddIcon from "@mui/icons-material/Add";
import { GridRowId } from "@mui/x-data-grid/models/gridRows";

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

const GeopilotDataGrid: FC<GeopilotDataGridProps> = props => {
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
    <Stack sx={{ height: "100%" }} gap={2}>
      <Box sx={{ flex: "0" }}>
        <BaseButton
          variant="outlined"
          icon={<AddIcon />}
          onClick={handleAddClick}
          label={props.addLabel}
          sx={{ backgroundColor: "white" }}
        />
      </Box>
      <StyledDataGrid
        data-cy={`${props.name}-grid`}
        sx={{ flex: "1" }}
        autoPageSize
        disableColumnSelector
        hideFooterSelectedRowCount
        onRowSelectionModelChange={handleRowSelection}
        {...props}
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
    />
  );
};

export default GeopilotDataGrid;
