/** The `navigator` signals required to recognise an iOS/iPadOS device. */
interface PlatformSignals {
  userAgent: string;
  platform: string;
  maxTouchPoints: number;
}

/**
 * Detects iPhone, iPod and iPad. Every browser on iOS/iPadOS runs on WebKit and shares
 * the native file-picker behaviour that greys out files whose extension has no known UTI
 * (e.g. .xtf), so a restrictive `accept` attribute must be dropped on these devices.
 *
 * iPadOS 13+ reports as "MacIntel" in its default desktop-site mode, so an iPad is only
 * distinguishable from a real Mac by its touch support (Macs report 0 touch points).
 *
 * @param nav Navigator signals to inspect. Defaults to the global `navigator`; injectable for tests.
 */
export const isIosDevice = (nav: PlatformSignals = navigator): boolean => {
  const isIPhone = /iPhone|iPod/.test(nav.userAgent);
  const isIPad = /iPad/.test(nav.userAgent) || (nav.platform === "MacIntel" && nav.maxTouchPoints > 1);
  return isIPhone || isIPad;
};
