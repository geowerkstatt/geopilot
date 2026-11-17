import { buildSnapshotSteps } from "../../src/pages/admin/processingRuns/pipelineSnapshotModel";

describe("pipeline snapshot model", () => {
  it("merges the configuration of a step and names where each value comes from", () => {
    const [step] = buildSnapshotSteps({
      pipelines: [
        {
          id: "dmav",
          steps: [{ id: "validation", process_id: "validator", process_config_overwrites: { toolVersion: "2" } }],
        },
      ],
      processes: [
        {
          id: "validator",
          implementation: "XtfValidatorProcess",
          default_config: { toolVersion: "1", profile: "dmav" },
        },
      ],
      process_configs: { XtfValidatorProcess: { modelDirs: "%ITF_DIR;https://models.interlis.ch/" } },
    });

    expect(step.implementation).to.equal("XtfValidatorProcess");
    expect(step.config).to.deep.include({ key: "toolVersion", value: "2", origin: "step" });
    expect(step.config).to.deep.include({ key: "profile", value: "dmav", origin: "definition" });
    expect(step.config).to.deep.include({
      key: "modelDirs",
      value: "%ITF_DIR;https://models.interlis.ch/",
      origin: "base",
    });
    expect(step.config.filter(entry => entry.key === "toolVersion")).to.have.length(1);
  });

  it("writes a list as its items, keeps an empty value empty and a null value null", () => {
    const [step] = buildSnapshotSteps({
      pipelines: [{ steps: [{ id: "matching", process_id: "matcher" }] }],
      processes: [
        {
          id: "matcher",
          default_config: { fileExtensions: ["xtf", "itf"], iliModels: null, profile: "", patterns: [], strict: true },
        },
      ],
    });

    expect(step.config.map(entry => [entry.key, entry.value])).to.deep.equal([
      ["fileExtensions", "xtf, itf"],
      ["iliModels", null],
      ["profile", ""],
      ["patterns", ""],
      ["strict", "true"],
    ]);
  });

  it("lists the inputs, conditions and outputs of a step", () => {
    const [step] = buildSnapshotSteps({
      pipelines: [
        {
          steps: [
            {
              id: "matching",
              input: { files: "${upload()}", extensions: ["xtf", "itf"] },
              conditions: {
                post: { fail_conditions: [{ id: "one-xtf", expression: "Length([matching.XtfFiles]) != 1" }] },
              },
              output_actions: [{ property: "XtfFiles", actions: ["Delivery"] }],
            },
          ],
        },
      ],
    });

    expect(step.inputs).to.deep.equal([
      { key: "files", expression: "${upload()}" },
      { key: "extensions", expression: "xtf, itf" },
    ]);
    expect(step.conditionGroups).to.deep.equal([
      {
        phase: "post",
        kind: "fail_conditions",
        conditions: [{ id: "one-xtf", expression: "Length([matching.XtfFiles]) != 1" }],
      },
    ]);
    expect(step.outputs).to.deep.equal([{ property: "XtfFiles", actions: ["Delivery"] }]);
  });

  it("groups the conditions by phase and kind, in the order of the definition, and leaves out empty kinds", () => {
    const [step] = buildSnapshotSteps({
      pipelines: [
        {
          steps: [
            {
              id: "validation",
              conditions: {
                pre: {
                  skip_conditions: [{ id: "no-xtf", expression: "Length([matching.XtfFiles]) == 0" }],
                  fail_conditions: [
                    { id: "too-many", expression: "Length([matching.XtfFiles]) > 1" },
                    { id: "no-model", expression: "Length([models.Files]) == 0" },
                  ],
                  warn_conditions: [],
                },
                post: { fail_conditions: [{ id: "invalid", expression: "![validation.ValidationSuccessful]" }] },
              },
            },
          ],
        },
      ],
    });

    expect(step.conditionGroups.map(group => [group.phase, group.kind, group.conditions.length])).to.deep.equal([
      ["pre", "skip_conditions", 1],
      ["pre", "fail_conditions", 2],
      ["post", "fail_conditions", 1],
    ]);
    expect(step.conditionGroups[1].conditions.map(condition => condition.id)).to.deep.equal(["too-many", "no-model"]);
  });

  it("copes with a snapshot that lacks the optional parts", () => {
    const steps = buildSnapshotSteps({ pipelines: [{ steps: [{ id: "a" }] }] });

    expect(steps).to.have.length(1);
    expect(steps[0].implementation).to.equal(undefined);
    expect(steps[0].config).to.deep.equal([]);
    expect(steps[0].inputs).to.deep.equal([]);
    expect(steps[0].conditionGroups).to.deep.equal([]);
    expect(buildSnapshotSteps({})).to.deep.equal([]);
  });
});
