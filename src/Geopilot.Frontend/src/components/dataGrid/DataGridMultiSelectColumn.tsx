import {
  GridActionsColDef,
  GridRenderEditCellParams,
  GridSingleSelectColDef,
  GridValidRowModel,
  useGridApiContext,
} from "@mui/x-data-grid";
import { FormControl, InputLabel, MenuItem, Select, SelectChangeEvent } from "@mui/material";
import { ReactNode } from "react";
import { GridBaseColDef } from "@mui/x-data-grid/internals";
import { DataRow } from "../adminGrid/AdminGridInterfaces.ts";

// eslint-disable-next-line
export interface GridMultiSelectColDef<R extends GridValidRowModel = any, V = any, F = V>
  extends GridBaseColDef<R, V, F> {
  type: "custom";
  valueOptions: Array<DataRow | string>;
  getOptionLabel: (value: DataRow | string) => string;
  getOptionValue: (value: DataRow | string) => string | number;
}

// eslint-disable-next-line
export type GridColDef<R extends GridValidRowModel = any, V = any, F = V> =
  | GridBaseColDef<R, V, F>
  | GridActionsColDef<R, V, F>
  | GridSingleSelectColDef<R, V, F>
  | GridMultiSelectColDef<R, V, F>;

export const IsGridMultiSelectColDef = (columnDef: GridColDef) =>
  columnDef.type === "custom" && "valueOptions" in columnDef;

export const TransformToMultiSelectColumn = (columnDef: GridMultiSelectColDef) => {
  columnDef.valueFormatter = (value: number[] | DataRow[] | string[] | undefined) => {
    if (value) {
      return value
        .map((row: number | DataRow | string) => {
          let selectedOption: DataRow | string | undefined;
          if (typeof row === "number") {
            selectedOption = (columnDef.valueOptions as DataRow[]).find(option => (option["id"] as number) === row);
          } else {
            selectedOption = row;
          }
          if (selectedOption) {
            return columnDef.getOptionLabel(selectedOption);
          }
        })
        .join(", ");
    } else {
      return "";
    }
  };

  columnDef.renderEditCell = params => (
    <DataGridMultiSelectColumn params={params}>
      {columnDef.valueOptions.map(option => (
        <MenuItem key={columnDef.getOptionValue(option)} value={columnDef.getOptionValue(option)}>
          {columnDef.getOptionLabel(option)}
        </MenuItem>
      ))}
    </DataGridMultiSelectColumn>
  );

  columnDef.filterOperators = [
    {
      value: "contains",
      getApplyFilterFn: filterItem => {
        if (filterItem.value == null || filterItem.value === "") {
          return null;
        }

        return (values: DataRow[] | string[]) => {
          return values?.some(value =>
            typeof value === "string" ? value === filterItem.value : value["id"] === filterItem.value,
          );
        };
      },
      InputComponent: props => (
        <DataGridFilterSingleSelect props={props}>
          {columnDef.valueOptions.map(option => (
            <MenuItem key={columnDef.getOptionValue(option)} value={columnDef.getOptionValue(option)}>
              {columnDef.getOptionLabel(option)}
            </MenuItem>
          ))}
        </DataGridFilterSingleSelect>
      ),
    },
  ];
};

interface DataGridMultiSelectColumnProps {
  params: GridRenderEditCellParams;
  children: ReactNode;
}

const DataGridMultiSelectColumn = ({ params, children }: DataGridMultiSelectColumnProps) => {
  const apiRef = useGridApiContext();

  const handleChange = (event: SelectChangeEvent) => {
    apiRef.current.setEditCellValue({
      id: params.id,
      field: params.field,
      value: event.target.value,
    });
  };

  const values = params.value
    ? params.value?.map((row: DataRow | number | string) =>
        typeof row === "number" || typeof row === "string" ? row : row["id"],
      )
    : [];

  return (
    <Select
      labelId="data-grid-multiselect-label"
      id="data-grid-multiselect"
      multiple
      value={values}
      onChange={handleChange}
      sx={{ width: "100%" }}>
      {children}
    </Select>
  );
};

interface DataGridFilterSingleSelectProps {
  // eslint-disable-next-line
  props: Record<string, any>;
  children: ReactNode;
}

const DataGridFilterSingleSelect = ({ props, children }: DataGridFilterSingleSelectProps) => {
  const { item, applyValue, focusElementRef } = props;

  return (
    <FormControl fullWidth variant="standard">
      <InputLabel variant="standard" htmlFor="uncontrolled-native">
        Value
      </InputLabel>
      <Select
        id={`contains-input-${item.id}`}
        value={item.value}
        onChange={event => applyValue({ ...item, value: event.target.value })}
        inputRef={focusElementRef}>
        {children}
      </Select>
    </FormControl>
  );
};
