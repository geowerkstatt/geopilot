import { TouchEvent, useEffect, useRef, WheelEvent } from "react";
import { useMediaQuery, useTheme } from "@mui/system";

// Horizontal finger travel (px) required before a touch gesture counts as a step swipe.
const SWIPE_THRESHOLD_PX = 50;

interface UseStepSwipeArgs {
  activeStep: number;
  stepCount: number;
  cooldownMs: number;
  onNavigate: (target: number) => boolean;
}

export interface StepSwipeHandlers {
  onWheel: (event: WheelEvent<HTMLDivElement>) => void;
  onTouchStart: (event: TouchEvent<HTMLDivElement>) => void;
  onTouchEnd: (event: TouchEvent<HTMLDivElement>) => void;
}

/**
 * Turns horizontal wheel/trackpad scrolls and touch swipes into discrete step navigation. Navigation
 * updates the active step, which both the stepper and the content carousel slide to, keeping them in
 * sync. Gestures are ignored on desktop and at the track boundaries.
 *
 * The cooldown keeps one gesture to one step. It is armed only when onNavigate reports an
 * accepted move, so a swipe toward a locked step does not silently swallow input. Callers pass
 * cooldownMs (their slide duration) so the next step cannot start mid-animation. A wheel gesture
 * has no discrete end, so every horizontal tick re-arms the cooldown: an inertial/continuous
 * scroll is treated as a single gesture and cannot advance a second step until it settles.
 */
export const useStepSwipe = ({
  activeStep,
  stepCount,
  cooldownMs,
  onNavigate,
}: UseStepSwipeArgs): StepSwipeHandlers => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const cooldown = useRef(false);
  const cooldownTimer = useRef<number | undefined>(undefined);
  const touchStart = useRef<{ x: number; y: number } | null>(null);

  const armCooldown = () => {
    cooldown.current = true;
    window.clearTimeout(cooldownTimer.current);
    cooldownTimer.current = window.setTimeout(() => {
      cooldown.current = false;
    }, cooldownMs);
  };

  useEffect(() => () => window.clearTimeout(cooldownTimer.current), []);

  const navigateBy = (direction: number) => {
    if (cooldown.current) return;

    const target = activeStep + direction;
    if (target < 0 || target >= stepCount) return;

    if (onNavigate(target)) {
      armCooldown();
    }
  };

  const onWheel = (event: WheelEvent<HTMLDivElement>) => {
    if (!isMobile) return;
    if (Math.abs(event.deltaX) <= Math.abs(event.deltaY)) return;

    if (cooldown.current) {
      // Tail of a continuous or inertial wheel gesture: keep the cooldown open so the
      // same physical scroll cannot advance a second step once cooldownMs has elapsed.
      armCooldown();
      return;
    }

    navigateBy(event.deltaX > 0 ? 1 : -1);
  };

  const onTouchStart = (event: TouchEvent<HTMLDivElement>) => {
    if (!isMobile) return;

    const touch = event.touches[0];
    touchStart.current = { x: touch.clientX, y: touch.clientY };
  };

  const onTouchEnd = (event: TouchEvent<HTMLDivElement>) => {
    const start = touchStart.current;
    touchStart.current = null;
    if (!isMobile || !start) return;

    const touch = event.changedTouches[0];
    const deltaX = touch.clientX - start.x;
    const deltaY = touch.clientY - start.y;
    if (Math.abs(deltaX) <= Math.abs(deltaY)) return;
    if (Math.abs(deltaX) < SWIPE_THRESHOLD_PX) return;

    navigateBy(deltaX < 0 ? 1 : -1);
  };

  return { onWheel, onTouchStart, onTouchEnd };
};
