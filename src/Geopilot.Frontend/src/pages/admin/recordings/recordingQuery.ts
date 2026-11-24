import { createSerializer } from "nuqs";
import { RecordingStateFilter } from "../../../api/generated";
import {
  parseAsIds,
  parseAsPage,
  parseAsPeriodTime,
  parseAsUsersOrClients,
  parseAsValuesOf,
  toListQuery,
} from "../listQuery.ts";

/** The filters of the list of recordings as the fields hold them; an empty string or list sets no filter. */
export interface RecordingFilterValues {
  /** The start of the period in local time, as yyyy-mm-ddThh:mm. */
  from: string;
  /** The last minute of the period, included, in local time as yyyy-mm-ddThh:mm. */
  to: string;
  /** "user:<id>" or "machineClient:<id>" each. */
  startedBy: string[];
  mandateIds: string[];
  states: string[];
}

export const emptyRecordingFilters: RecordingFilterValues = {
  from: "",
  to: "",
  startedBy: [],
  mandateIds: [],
  states: [],
};

/** How the address of the list holds its filters and its page; a value that cannot be read sets no filter. */
export const recordingSearchParams = {
  from: parseAsPeriodTime,
  to: parseAsPeriodTime,
  startedBy: parseAsUsersOrClients,
  mandateIds: parseAsIds,
  states: parseAsValuesOf(Object.values(RecordingStateFilter)),
  page: parseAsPage,
};

/** The names in the address that differ from the fields, kept so that links to the list keep working. */
export const recordingUrlKeys = { mandateIds: "mandate", states: "state" };

/** The address of the list for its filters and page, as useQueryStates writes it. */
export const serializeRecordingSearch = createSerializer(recordingSearchParams, { urlKeys: recordingUrlKeys });

const jobIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** The job id the text holds without the spaces around it, or undefined when it holds none. */
export const parseJobId = (text: string): string | undefined => {
  const candidate = text.trim();
  return jobIdPattern.test(candidate) ? candidate : undefined;
};

/** The query string of GET api/v1/recording for the filters and the page. */
export const toRecordingQuery = (filters: RecordingFilterValues, page: number, pageSize: number): string => {
  const params = toListQuery({ ...filters, usersOrClients: filters.startedBy }, page, pageSize);
  filters.states.forEach(state => params.append("states", state));
  return params.toString();
};

const recordingTabs = ["steps", "reactions", "pipeline"] as const;

/** A tab of the page of one recording. */
export type RecordingTab = (typeof recordingTabs)[number];

/** The tab the address names, such as #pipeline, or the steps for any other address. */
export const tabFromHash = (hash: string): RecordingTab => recordingTabs.find(tab => `#${tab}` === hash) ?? "steps";
