import { PipelineSummary } from "../../../api/apiInterfaces";

/**
 * Finds a pipeline by its id. Returns undefined when the id is unset or no longer part of the available
 * pipelines, which is how a mandate referencing a removed pipeline is detected.
 */
export const findPipeline = (
  pipelines: PipelineSummary[] | undefined,
  id: string | undefined,
): PipelineSummary | undefined => (id === undefined ? undefined : pipelines?.find(pipeline => pipeline.id === id));

/**
 * Resolves the display name of a pipeline for the active language, falling back to German and finally to
 * the raw id when no localised name is available.
 */
export const getLocalisedPipelineName = (pipeline: PipelineSummary, language: string): string =>
  pipeline.displayName?.[language] || pipeline.displayName?.["de"] || pipeline.id;
