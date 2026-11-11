/** The state a run is opened with from the list: the query of the list, so going back shows the same runs. */
export const listQueryState = (listQuery: string) => ({ listQuery });

/** The query of the list a run was opened from, or an empty string when it was opened from anywhere else. */
export const listQueryOf = (state: unknown): string =>
  typeof state === "object" && state !== null && "listQuery" in state && typeof state.listQuery === "string"
    ? state.listQuery
    : "";

/** The filters of the list of pipeline runs as the fields hold them; an empty string or list sets no filter. */
export interface ProcessingRunFilterValues {
  /** The start of the period in local time, as yyyy-mm-ddThh:mm. */
  from: string;
  /** The last minute of the period, included, in local time as yyyy-mm-ddThh:mm. */
  to: string;
  /** "person:<id>" or "machineClient:<id>" each. */
  declarers: string[];
  mandateIds: string[];
  states: string[];
}

export const emptyProcessingRunFilters: ProcessingRunFilterValues = {
  from: "",
  to: "",
  declarers: [],
  mandateIds: [],
  states: [],
};

const timeFields = ["from", "to"] as const;
const listFields = ["declarers", "mandateIds", "states"] as const;

// The names in the address of the list, one parameter per value.
const listSearchParamNames: Record<(typeof listFields)[number], string> = {
  declarers: "declarer",
  mandateIds: "mandate",
  states: "state",
};

/** The address of the list for the filters and the page, so going back to it shows the same runs. */
export const toProcessingRunSearchParams = (filters: ProcessingRunFilterValues, page: number): URLSearchParams => {
  const params = new URLSearchParams();
  timeFields.filter(field => filters[field]).forEach(field => params.set(field, filters[field]));
  listFields.forEach(field => filters[field].forEach(value => params.append(listSearchParamNames[field], value)));
  if (page > 0) {
    params.set("page", String(page));
  }
  return params;
};

// The address can be typed by hand, and a time that is none would throw once the query turns it into an instant.
const readTime = (params: URLSearchParams, field: (typeof timeFields)[number]) => {
  const time = params.get(field) ?? "";
  return Number.isNaN(new Date(time).getTime()) ? "" : time;
};

/** The filters and the page from the address of the list; an unreadable time sets no filter, an unreadable page the first. */
export const readProcessingRunSearchParams = (
  params: URLSearchParams,
): { filters: ProcessingRunFilterValues; page: number } => {
  const filters: ProcessingRunFilterValues = {
    from: readTime(params, "from"),
    to: readTime(params, "to"),
    declarers: params.getAll(listSearchParamNames.declarers),
    mandateIds: params.getAll(listSearchParamNames.mandateIds),
    states: params.getAll(listSearchParamNames.states),
  };
  const page = Number.parseInt(params.get("page") ?? "", 10);
  return { filters, page: Number.isNaN(page) || page < 0 ? 0 : page };
};

const jobIdPattern = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/;

/** The job id in the form the API writes it, lowercase with hyphens, or undefined when the text holds none. */
export const parseJobId = (text: string): string | undefined => {
  const candidate = text.trim().replace(/^\{/, "").replace(/\}$/, "").toLowerCase();
  if (!jobIdPattern.test(candidate)) {
    return undefined;
  }
  const hex = candidate.replace(/-/g, "");
  return [hex.slice(0, 8), hex.slice(8, 12), hex.slice(12, 16), hex.slice(16, 20), hex.slice(20)].join("-");
};

// The field holds the local time of the browser, as the times in the list are shown there.
const toInstant = (localDateTime: string, offsetMinutes = 0) => {
  const date = new Date(localDateTime);
  date.setMinutes(date.getMinutes() + offsetMinutes);
  return date.toISOString();
};

/** The query string of GET api/v1/pipelinerun for the filters and the page. */
export const toProcessingRunQuery = (filters: ProcessingRunFilterValues, page: number, pageSize: number): string => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters.from) {
    params.set("from", toInstant(filters.from));
  }
  if (filters.to) {
    // The API excludes the end, so the last minute ends where the next one starts.
    params.set("to", toInstant(filters.to, 1));
  }
  filters.declarers.forEach(declarer => {
    const [kind, id] = declarer.split(":");
    params.append(kind === "machineClient" ? "machineClientIds" : "userIds", id);
  });
  filters.mandateIds.forEach(mandateId => params.append("mandateIds", mandateId));
  filters.states.forEach(state => params.append("states", state));
  return params.toString();
};

const processingRunTabs = ["steps", "reactions", "pipeline"] as const;

/** A tab of the page of one pipeline run. */
export type ProcessingRunTab = (typeof processingRunTabs)[number];

/** The tab the address names, such as #pipeline, or the steps for any other address. */
export const tabFromHash = (hash: string): ProcessingRunTab =>
  processingRunTabs.find(tab => `#${tab}` === hash) ?? "steps";
