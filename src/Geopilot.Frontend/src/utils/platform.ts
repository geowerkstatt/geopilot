/**
 * Detects iPhone, iPod and iPad. Every browser on iOS/iPadOS runs on WebKit and shares
 * the native file-picker behaviour that greys out files whose extension has no known UTI
 * (e.g. .xtf), so a restrictive `accept` attribute must be dropped on these devices.
 *
 * iPadOS 13+ reports as "MacIntel" in its default desktop-site mode, so an iPad is only
 * distinguishable from a real Mac by its touch support (Macs report 0 touch points).
 *
 * The branching heuristic is safeguarded by a Cypress test that stubs `navigator`
 * (see cypress/e2e/delivery.cy.js).
 */
export const isIosDevice = (): boolean => {
  // `navigator.platform` is deprecated (its successor `navigator.userAgentData` is missing in
  // Safari/WebKit, exactly the browsers we target here), so it is read deliberately through a
  // local type that documents the fields we depend on and keeps the deprecation notice off it.
  const nav: { userAgent: string; platform: string; maxTouchPoints: number } = navigator;
  const isIPhone = /iPhone|iPod/.test(nav.userAgent);
  const isIPad = /iPad/.test(nav.userAgent) || (nav.platform === "MacIntel" && nav.maxTouchPoints > 1);
  return isIPhone || isIPad;
};
