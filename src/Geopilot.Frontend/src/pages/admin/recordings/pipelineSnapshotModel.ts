import { DefinitionSnapshot, LocalizedText } from "../../../api/apiInterfaces.ts";

/** Where a configuration value of a step comes from. */
export type SnapshotConfigOrigin = "base" | "definition" | "step";

interface SnapshotConfigEntry {
  key: string;
  /** The value as text, empty for an empty text or list, null when the definition sets it to null. */
  value: string | null;
  origin: SnapshotConfigOrigin;
}

/** A step of the snapshot as the pipeline view shows it. */
export interface SnapshotStepView {
  id: string;
  displayName?: LocalizedText;
  implementation?: string;
  /** The effective configuration: the base of the installation, the definition and the step's own values. */
  config: SnapshotConfigEntry[];
  inputs: { key: string; expression: string }[];
  /** The conditions by phase and kind, in the order of the definition; a kind without conditions is left out. */
  conditionGroups: { phase: "pre" | "post"; kind: string; conditions: { id?: string; expression: string }[] }[];
  outputs: { property: string; actions: string[] }[];
}

/** A value of the definition as text: a list as its items separated by commas, a value that is not set as null. */
const formatValue = (value: unknown): string | null => {
  if (value === null || value === undefined) {
    return null;
  }
  if (Array.isArray(value)) {
    return value.map(item => formatValue(item) ?? "null").join(", ");
  }
  return typeof value === "string" ? value : JSON.stringify(value);
};

const entries = (values: Record<string, unknown> | undefined, origin: SnapshotConfigOrigin): SnapshotConfigEntry[] =>
  Object.entries(values ?? {}).map(([key, value]) => ({ key, value: formatValue(value), origin }));

/** The steps of the snapshot's pipeline with their effective configuration, inputs, conditions and outputs. */
export const buildSnapshotSteps = (snapshot: DefinitionSnapshot): SnapshotStepView[] =>
  (snapshot.pipelines?.[0]?.steps ?? []).map((step, index) => {
    const process = snapshot.processes?.find(candidate => candidate.id === step.process_id);
    const implementation = process?.implementation;

    // A later layer replaces an earlier one, as the runtime merges them.
    const config = new Map<string, SnapshotConfigEntry>();
    [
      ...entries(implementation ? snapshot.process_configs?.[implementation] : undefined, "base"),
      ...entries(process?.default_config, "definition"),
      ...entries(step.process_config_overwrites, "step"),
    ].forEach(entry => config.set(entry.key, entry));

    const conditionGroups = (["pre", "post"] as const).flatMap(phase =>
      Object.entries(step.conditions?.[phase] ?? {})
        .filter(([, list]) => (list ?? []).length > 0)
        .map(([kind, list]) => ({
          phase,
          kind,
          conditions: (list ?? []).map(condition => ({ id: condition.id, expression: condition.expression ?? "" })),
        })),
    );

    return {
      id: step.id ?? String(index),
      displayName: step.display_name,
      implementation,
      config: [...config.values()],
      inputs: Object.entries(step.input ?? {}).map(([key, value]) => ({
        key,
        expression: formatValue(value) ?? "null",
      })),
      conditionGroups,
      outputs: (step.output_actions ?? []).map(output => ({
        property: output.property ?? "",
        actions: output.actions ?? [],
      })),
    };
  });
