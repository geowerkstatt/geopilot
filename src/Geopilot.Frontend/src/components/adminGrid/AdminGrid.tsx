import { FC, useEffect, useState } from "react";
import Button from "@mui/material/Button";
import AddIcon from "@mui/icons-material/Add";
import {
  DataGrid,
  GridActionsCellItem,
  GridColDef,
  GridRowEditStopReasons,
  GridRowId,
  GridRowModes,
  GridRowModesModel,
  GridRowParams,
  MuiEvent,
} from "@mui/x-data-grid";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import { useTranslation } from "react-i18next";
import { AdminGridProps, DataRow } from "./AdminGridTypes.ts";

export const AdminGrid: FC<AdminGridProps> = ({ addLabel, data, columns, onSave, onDisconnect }) => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<DataRow[]>([]);
  const [rowModesModel, setRowModesModel] = useState<GridRowModesModel>({});

  useEffect(() => {
    if (data) {
      console.log(data);
      setRows(data);
    }
  }, [data]);

  const actionColumn: GridColDef = {
    field: "actions",
    type: "actions",
    headerName: "",
    width: 86,
    cellClassName: "actions",
    getActions: ({ id }) => {
      const isInEditMode = rowModesModel[id]?.mode === GridRowModes.Edit;

      if (isInEditMode) {
        return [
          <GridActionsCellItem
            key="save"
            icon={<SaveOutlinedIcon />}
            label={t("save")}
            onClick={handleSaveClick(id)}
            color="inherit"
          />,
          <GridActionsCellItem
            key="cancel"
            icon={<CancelOutlinedIcon />}
            label={t("cancel")}
            onClick={handleCancelClick(id)}
            color="inherit"
          />,
        ];
      }

      return [
        <GridActionsCellItem
          key="edit"
          icon={<EditOutlinedIcon />}
          label={t("edit")}
          onClick={handleEditClick(id)}
          color="inherit"
        />,
        <GridActionsCellItem
          key="disconnect"
          icon={<LinkOffIcon />}
          label={t("disconnect")}
          onClick={handleDisconnectClick(id)}
          color="error"
        />,
      ];
    },
  };
  const adminGridColumns: GridColDef[] = columns.concat(actionColumn);

  const handleClick = () => {
    console.log("add record");
    const id = 0;
    setRows(oldRows => [...oldRows, { id }]);
    setRowModesModel(oldModel => ({
      ...oldModel,
      [id]: { mode: GridRowModes.Edit, fieldToFocus: columns[0].field },
    }));
  };

  const handleRowEditStop = (params: GridRowParams, event: MuiEvent) => {
    if (params.reason === GridRowEditStopReasons.rowFocusOut) {
      event.defaultMuiPrevented = true;
    }
  };

  const handleEditClick = (id: GridRowId) => () => {
    setRowModesModel({ ...rowModesModel, [id]: { mode: GridRowModes.Edit } });
  };

  const handleSaveClick = (id: GridRowId) => () => {
    setRowModesModel({ ...rowModesModel, [id]: { mode: GridRowModes.View } });
    onSave(rows[id as number]);
  };

  const handleDisconnectClick = (id: GridRowId) => () => {
    onDisconnect(rows[id as number]);
  };

  const handleCancelClick = (id: GridRowId) => () => {
    setRowModesModel({
      ...rowModesModel,
      [id]: { mode: GridRowModes.View, ignoreModifications: true },
    });

    const editedRow = rows.find(row => row.id === id);
    if (editedRow?.id === 0) {
      setRows(rows.filter(row => row.id !== id));
    }
  };

  const processRowUpdate = (newRow: DataRow) => {
    const updatedRow = { ...newRow };
    setRows(rows.map(row => (row.id === newRow.id ? updatedRow : row)));
    return updatedRow;
  };

  const handleRowModesModelChange = (newRowModesModel: GridRowModesModel) => {
    setRowModesModel(newRowModesModel);
  };

  return (
    <>
      <Button
        color="primary"
        variant="outlined"
        startIcon={<AddIcon />}
        sx={{ marginBottom: "20px" }}
        onClick={handleClick}>
        {t(addLabel)}
      </Button>
      <DataGrid
        sx={{
          fontFamily: "system-ui, -apple-system",
        }}
        rows={rows}
        columns={adminGridColumns}
        editMode="row"
        rowModesModel={rowModesModel}
        onRowModesModelChange={handleRowModesModelChange}
        onRowEditStop={handleRowEditStop}
        processRowUpdate={processRowUpdate}
      />
    </>
  );
};
