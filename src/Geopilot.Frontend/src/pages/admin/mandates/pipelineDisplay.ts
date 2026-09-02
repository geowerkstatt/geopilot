import { PipelineSummary } from "../../../api/generated";

/**
 * Finds a pipeline by its id. Returns undefined when the id is unset or no longer part of the available
 * pipelines, which is how a mandate referencing a removed pipeline is detected.
 */
export const findPipeline = (
  pipelines: PipelineSummary[] | undefined,
  id: string | undefined,
): PipelineSummary | undefined => (id === undefined ? undefined : pipelines?.find(pipeline => pipeline.id === id));
