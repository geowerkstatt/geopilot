import { formatPickedDate, parseLocalDate, partlyTypedDate } from "../../src/components/form/localDateTime";

describe("local date time", () => {
  it("keeps a picked date in local time, clears an empty field and skips a partly typed date", () => {
    expect(formatPickedDate(new Date(2026, 9, 28, 8, 5), true)).to.equal("2026-10-28T08:05");
    expect(formatPickedDate(new Date(2026, 9, 28, 8, 5), false)).to.equal("2026-10-28");
    expect(formatPickedDate(null, true)).to.equal("");
    expect(formatPickedDate(new Date(Number.NaN), false)).to.equal(undefined);
  });

  it("reads a date in local time, not as midnight in UTC", () => {
    expect(parseLocalDate("2026-11-04")?.getHours()).to.equal(0);
    expect(parseLocalDate("2026-11-04T08:05")?.getHours()).to.equal(8);
    expect(parseLocalDate("")).to.equal(null);
    expect(parseLocalDate(partlyTypedDate)).to.equal(null);
  });
});
