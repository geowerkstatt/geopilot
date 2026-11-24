import { createParser, parseAsNativeArrayOf } from "nuqs";

/** The state an entry is opened with from its list: the query of the list, so going back shows the same entries. */
export const listQueryState = (listQuery: string) => ({ listQuery });

/** The query of the list an entry was opened from, or an empty string when it was opened from anywhere else. */
export const listQueryOf = (state: unknown): string =>
  typeof state === "object" && state !== null && "listQuery" in state && typeof state.listQuery === "string"
    ? state.listQuery
    : "";

// The address can be typed by hand, and a value the API cannot take would fail the whole list, so such a value sets
// no filter.
const parseAsReadable = (isReadable: (value: string) => boolean) =>
  createParser({ parse: (value: string) => (isReadable(value) ? value : null), serialize: (value: string) => value });

// Ids as the API binds them, at most nine digits so that they fit its whole numbers.
const idPattern = /^\d{1,9}$/;

// The highest page the API serves.
const maxPage = 1_000_000;

/**
 * A start or end of the period in local time, as yyyy-mm-ddThh:mm. A time that is none would throw once the query
 * turns it into an instant.
 */
export const parseAsPeriodTime = parseAsReadable(time => !Number.isNaN(new Date(time).getTime())).withDefault("");

/** Who processed or delivered, as `user:<id>` or `machineClient:<id>`, one key in the address per value. */
export const parseAsUsersOrClients = parseAsNativeArrayOf(
  parseAsReadable(value => /^(user|machineClient):\d{1,9}$/.test(value)),
);

/** Ids, such as those of mandates, one key in the address per id. */
export const parseAsIds = parseAsNativeArrayOf(parseAsReadable(value => idPattern.test(value)));

/** One id, or an empty string for none. */
export const parseAsId = parseAsReadable(value => idPattern.test(value)).withDefault("");

/** Values out of a fixed set, such as states, one key in the address per value. */
export const parseAsValuesOf = (values: readonly string[]) =>
  parseAsNativeArrayOf(parseAsReadable(value => values.includes(value)));

/** The page of a list, counted from 0; any other page in the address shows the first. */
export const parseAsPage = createParser({
  parse: (value: string) => (/^\d{1,7}$/.test(value) && Number(value) <= maxPage ? Number(value) : null),
  serialize: String,
}).withDefault(0);

/** The filters both lists of the administration share, as their fields hold them. */
interface SharedListFilters {
  /** The start of the period in local time, as yyyy-mm-ddThh:mm. */
  from: string;
  /** The last minute of the period, included, in local time as yyyy-mm-ddThh:mm. */
  to: string;
  /** "user:<id>" or "machineClient:<id>" each. */
  usersOrClients: string[];
  mandateIds: string[];
}

// The field holds the local time of the browser, as the times in the list are shown there.
const toInstant = (localDateTime: string, offsetMinutes = 0) => {
  const date = new Date(localDateTime);
  date.setMinutes(date.getMinutes() + offsetMinutes);
  return date.toISOString();
};

/** The page and the filters both lists of the administration send their API alike. */
export const toListQuery = (filters: SharedListFilters, page: number, pageSize: number): URLSearchParams => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters.from) {
    params.set("from", toInstant(filters.from));
  }
  if (filters.to) {
    // The API excludes the end, so the last minute ends where the next one starts.
    params.set("to", toInstant(filters.to, 1));
  }
  filters.usersOrClients.forEach(userOrClient => {
    const [kind, id] = userOrClient.split(":");
    params.append(kind === "machineClient" ? "machineClientIds" : "userIds", id);
  });
  filters.mandateIds.forEach(mandateId => params.append("mandateIds", mandateId));
  return params;
};
