import { toLocalDateTime } from "../../src/components/form/localDateTime";

describe("local date time", () => {
  it("keeps a picked date in local time, clears an empty field and skips a partly typed date", () => {
    expect(toLocalDateTime(new Date(2026, 9, 28, 8, 5))).to.equal("2026-10-28T08:05");
    expect(toLocalDateTime(null)).to.equal("");
    expect(toLocalDateTime(new Date(Number.NaN))).to.equal(undefined);
  });
});
