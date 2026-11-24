import { createSerializer } from "nuqs";
import { ReactionKind } from "../../../api/generated";
import {
  parseAsId,
  parseAsIds,
  parseAsPage,
  parseAsPeriodTime,
  parseAsUsersOrClients,
  parseAsValuesOf,
  toListQuery,
} from "../listQuery.ts";

/** The filters of the list of deliveries as the fields hold them; an empty string or list sets no filter. */
export interface DeliveryFilterValues {
  /** The start of the period in local time, as yyyy-mm-ddThh:mm. */
  from: string;
  /** The last minute of the period, included, in local time as yyyy-mm-ddThh:mm. */
  to: string;
  /** "user:<id>" or "machineClient:<id>" each. */
  deliveredBy: string[];
  mandateIds: string[];
  /** The kinds of reactions of which one gave up, such as the forwarding. */
  failed: string[];
}

export const emptyDeliveryFilters: DeliveryFilterValues = {
  from: "",
  to: "",
  deliveredBy: [],
  mandateIds: [],
  failed: [],
};

/**
 * How the address of the list holds its filters, the beginning of the id it is narrowed to and its page; a value that
 * cannot be read sets no filter.
 */
export const deliverySearchParams = {
  from: parseAsPeriodTime,
  to: parseAsPeriodTime,
  deliveredBy: parseAsUsersOrClients,
  mandateIds: parseAsIds,
  failed: parseAsValuesOf(Object.values(ReactionKind)),
  idPrefix: parseAsId,
  page: parseAsPage,
};

/** The names in the address that differ from the fields. */
export const deliveryUrlKeys = { mandateIds: "mandate", idPrefix: "id" };

/** The address of the list for its filters, id prefix and page, as useQueryStates writes it. */
export const serializeDeliverySearch = createSerializer(deliverySearchParams, { urlKeys: deliveryUrlKeys });

/** The query string of GET api/v1/delivery for the filters, the beginning of the id and the page. */
export const toDeliveryQuery = (
  filters: DeliveryFilterValues,
  idPrefix: string,
  page: number,
  pageSize: number,
): string => {
  const params = toListQuery({ ...filters, usersOrClients: filters.deliveredBy }, page, pageSize);
  filters.failed.forEach(kind => params.append("failedReactionKinds", kind));
  if (idPrefix) {
    params.set("idPrefix", idPrefix);
  }
  return params.toString();
};
