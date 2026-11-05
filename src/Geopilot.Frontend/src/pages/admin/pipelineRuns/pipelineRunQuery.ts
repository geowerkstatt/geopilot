/** The filters of the list of pipeline runs as the fields hold them; an empty string sets no filter. */
export interface PipelineRunFilterValues {
  /** The start of the period in local time, as yyyy-mm-ddThh:mm. */
  from: string;
  /** The last minute of the period, included, in local time as yyyy-mm-ddThh:mm. */
  to: string;
  /** "person:<id>", "machineClient:<id>", or "machineClient" for all machine clients. */
  declarer: string;
  mandateId: string;
  state: string;
}

export const emptyPipelineRunFilters: PipelineRunFilterValues = {
  from: "",
  to: "",
  declarer: "",
  mandateId: "",
  state: "",
};

const filterFields: (keyof PipelineRunFilterValues)[] = ["from", "to", "declarer", "mandateId", "state"];

// The names in the address of the list, shorter where the field name says more than the address needs.
const searchParamNames: Record<keyof PipelineRunFilterValues, string> = {
  from: "from",
  to: "to",
  declarer: "declarer",
  mandateId: "mandate",
  state: "state",
};

/** The address of the list for the filters and the page, so going back to it shows the same runs. */
export const toPipelineRunSearchParams = (filters: PipelineRunFilterValues, page: number): URLSearchParams => {
  const params = new URLSearchParams();
  filterFields.filter(field => filters[field]).forEach(field => params.set(searchParamNames[field], filters[field]));
  if (page > 0) {
    params.set("page", String(page));
  }
  return params;
};

/** The filters and the page from the address of the list; anything unreadable sets no filter or the first page. */
export const readPipelineRunSearchParams = (
  params: URLSearchParams,
): { filters: PipelineRunFilterValues; page: number } => {
  const filters = { ...emptyPipelineRunFilters };
  filterFields.forEach(field => {
    filters[field] = params.get(searchParamNames[field]) ?? "";
  });
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
export const toPipelineRunQuery = (filters: PipelineRunFilterValues, page: number, pageSize: number): string => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters.from) {
    params.set("from", toInstant(filters.from));
  }
  if (filters.to) {
    // The API excludes the end, so the last minute ends where the next one starts.
    params.set("to", toInstant(filters.to, 1));
  }
  if (filters.declarer) {
    const [kind, id] = filters.declarer.split(":");
    params.set("declarerKind", kind);
    if (id) {
      params.set("declarerId", id);
    }
  }
  if (filters.mandateId) {
    params.set("mandateId", filters.mandateId);
  }
  if (filters.state) {
    params.set("state", filters.state);
  }
  return params.toString();
};

const pipelineRunTabs = ["steps", "files", "reactions", "pipeline"] as const;

/** A tab of the page of one pipeline run. */
export type PipelineRunTab = (typeof pipelineRunTabs)[number];

/** The tab the address names, such as #files, or the steps for any other address. */
export const tabFromHash = (hash: string): PipelineRunTab => pipelineRunTabs.find(tab => `#${tab}` === hash) ?? "steps";
