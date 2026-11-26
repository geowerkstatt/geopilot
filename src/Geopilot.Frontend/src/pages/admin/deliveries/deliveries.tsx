import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { Box, Stack, Tooltip } from "@mui/material";
import {
  GridActionsCell,
  GridActionsCellItem,
  GridColDef,
  GridPaginationModel,
  GridRenderCellParams,
  GridRowId,
} from "@mui/x-data-grid";
import { useQueryStates } from "nuqs";
import { DeliveryPageResponse, DeliveryResponse } from "../../../api/generated";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import { UserOrClientLabel } from "../../../components/userOrClientLabel.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { listQueryState } from "../listQuery.ts";
import { DeliveryFilters } from "./deliveryFilters.tsx";
import {
  DeliveryFilterValues,
  deliverySearchParams,
  deliveryUrlKeys,
  serializeDeliverySearch,
  toDeliveryQuery,
} from "./deliveryQuery.ts";

// The most deliveries the API returns per page, fewer than the grid fits on a very tall screen.
const maxPageSize = 100;

/** All deliveries of the installation with how far their reactions got, newest first, paged and filtered on the server. */
export const Deliveries: FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  // Filters, search and page live in the address, so going back from a delivery shows the same deliveries again.
  const [{ idPrefix, page, ...filters }, setSearch] = useQueryStates(deliverySearchParams, {
    urlKeys: deliveryUrlKeys,
  });
  // The grid fits its page to its height and reports the size once it has measured it; until then nothing is loaded.
  const [pageSize, setPageSize] = useState(0);
  const [loaded, setLoaded] = useState<{ query: string; page?: DeliveryPageResponse }>();

  const query = toDeliveryQuery(filters, idPrefix, page, Math.min(pageSize, maxPageSize));
  // Loading lasts until the answer to the current query is there, so an answer to an older query never shows.
  const isLoading = loaded?.query !== query;

  useEffect(() => {
    if (pageSize === 0) {
      return;
    }
    let current = true;
    fetchApi<DeliveryPageResponse>(`/api/v1/delivery?${query}`, { errorMessageLabel: "deliveriesLoadingError" })
      .then(response => current && setLoaded({ query, page: response }))
      // useFetch has shown why; an empty page ends the loading.
      .catch(() => current && setLoaded({ query }));
    return () => {
      current = false;
    };
  }, [fetchApi, query, pageSize]);

  const changeSearch = (values: DeliveryFilterValues, searchedIdPrefix: string) =>
    setSearch({ ...values, idPrefix: searchedIdPrefix, page: 0 });

  const changePagination = (model: GridPaginationModel) => {
    setPageSize(model.pageSize);
    // The grid also reports a new page size when its height changes; only the page goes into the address.
    if (model.page !== page) {
      setSearch({ page: model.page });
    }
  };

  const openDelivery = (deliveryId: GridRowId) =>
    navigate(`/admin/deliveries/${deliveryId}`, {
      state: listQueryState(serializeDeliverySearch({ idPrefix, page, ...filters })),
    });

  const columns: GridColDef<DeliveryResponse>[] = [
    { field: "id", headerName: t("id"), width: 60 },
    {
      field: "date",
      headerName: t("deliveryDate"),
      type: "dateTime",
      valueGetter: (date: string) => new Date(date),
      width: 180,
    },
    {
      field: "deliveredByName",
      headerName: t("deliveredBy"),
      flex: 0.5,
      minWidth: 250,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<DeliveryResponse>) => (
        <UserOrClientLabel userOrClient={{ name: row.deliveredByName, machineClientId: row.deliveredByClientId }} />
      ),
    },
    {
      field: "mandateName",
      headerName: t("mandate"),
      flex: 0.5,
      minWidth: 200,
      valueGetter: (_value, row) => localized(row.mandate.name),
    },
    {
      field: "forwardingState",
      headerName: t("forwarding"),
      width: 170,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<DeliveryResponse>) =>
        row.forwardingState && <ReactionStateLabel state={row.forwardingState} />,
    },
    {
      field: "notificationState",
      headerName: t("notifications"),
      width: 190,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<DeliveryResponse>) =>
        row.notificationState && <ReactionStateLabel state={row.notificationState} />,
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      renderCell: params => (
        <GridActionsCell {...params}>
          <GridActionsCellItem
            icon={
              <Tooltip title={t("showDetails")} key={`details-${params.id}`}>
                <VisibilityOutlinedIcon data-cy="details" color="primary" />
              </Tooltip>
            }
            label={t("showDetails")}
            onClick={event => {
              // Otherwise the click reaches the row as well, which opens the page a second time.
              event.stopPropagation();
              openDelivery(params.id);
            }}
          />
        </GridActionsCell>
      ),
    },
  ];

  return (
    <Stack sx={{ height: "100%" }}>
      <DeliveryFilters values={filters} idPrefix={idPrefix} onChange={changeSearch} />
      <Box sx={{ flex: "1 1 0", minHeight: 0 }}>
        <GeopilotDataGrid
          name="deliveries"
          loading={isLoading}
          rows={loaded?.page?.items ?? []}
          columns={columns}
          paginationMode="server"
          rowCount={loaded?.page?.totalCount ?? 0}
          paginationModel={{ page, pageSize }}
          onPaginationModelChange={changePagination}
          disableColumnSorting
          disableColumnFilter
          onSelect={openDelivery}
        />
      </Box>
    </Stack>
  );
};
