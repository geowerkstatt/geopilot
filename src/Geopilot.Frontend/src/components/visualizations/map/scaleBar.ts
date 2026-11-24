/** A round length and the width it covers on the map. */
export interface ScaleBar {
  /** The length with its unit, for example "500 m". */
  label: string;
  /** The width of the bar in CSS pixels. */
  width: number;
}

const MIN_WIDTH = 64;
const LEADING_DIGITS = [1, 2, 5];

const metricUnit = (meters: number): [unit: string, unitsPerMeter: number] => {
  if (meters >= 1000) return ["km", 0.001];
  if (meters >= 1) return ["m", 1];
  if (meters >= 0.001) return ["mm", 1000];
  if (meters >= 1e-6) return ["μm", 1e6];
  return ["nm", 1e9];
};

/**
 * The bar OpenLayers' metric ScaleLine draws where one pixel covers `metersPerPixel`: the shortest length of 1, 2
 * or 5 times a power of ten that is at least 64 pixels wide. Undefined unless `metersPerPixel` is positive and finite.
 */
export const getScaleBar = (metersPerPixel: number): ScaleBar | undefined => {
  if (!Number.isFinite(metersPerPixel) || metersPerPixel <= 0) return undefined;
  const [unit, unitsPerMeter] = metricUnit(MIN_WIDTH * metersPerPixel);
  const unitsPerPixel = metersPerPixel * unitsPerMeter;
  for (let step = 3 * Math.floor(Math.log10(MIN_WIDTH * unitsPerPixel)); ; step++) {
    const exponent = Math.floor(step / 3);
    const length = LEADING_DIGITS[((step % 3) + 3) % 3] * 10 ** exponent;
    const width = Math.round(length / unitsPerPixel);
    if (width >= MIN_WIDTH) return { label: `${length.toFixed(Math.max(0, -exponent))} ${unit}`, width };
  }
};
