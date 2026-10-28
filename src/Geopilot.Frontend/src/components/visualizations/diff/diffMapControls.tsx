import { FC, Fragment, useCallback, useId, useState } from "react";
import { useTranslation } from "react-i18next";
import CompareIcon from "@mui/icons-material/Compare";
import FormatListBulletedIcon from "@mui/icons-material/FormatListBulleted";
import { Box, ButtonGroup, Slider, Stack, Typography, useTheme } from "@mui/material";
import { DiffChangeType, DiffSide } from "../../../api/apiInterfaces";
import { TranslationKey } from "../../../appInterfaces";
import { IconButton } from "../../buttons";
import { MapPanelPopper } from "../map/mapPanelPopper";
import { DiffLook, getLook } from "./diffMapGeometry";

type LegendGeometry = "point" | "line" | "area";

const LINES: Record<DiffSide, string> = { old: "5,19 16,10 28,15 39,7", new: "5,22 16,14 28,19 39,11" };
const AREAS: Record<DiffSide, { x: number; y: number }> = { old: { x: 8, y: 7 }, new: { x: 12, y: 5 } };

/** Where a change type has a geometry: added only after, deleted only before, changed in both. */
const STATES_OF: Record<DiffChangeType, DiffSide[]> = { added: ["new"], deleted: ["old"], changed: ["old", "new"] };

const LEGEND_ROWS: { changeType: DiffChangeType; label: TranslationKey }[] = [
  { changeType: "added", label: "diffChangeAdded" },
  { changeType: "changed", label: "diffChangeChanged" },
  { changeType: "deleted", label: "diffChangeDeleted" },
];

const LEGEND_GEOMETRIES: { geometry: LegendGeometry; label: TranslationKey }[] = [
  { geometry: "point", label: "diffLegendPoint" },
  { geometry: "line", label: "diffLegendLine" },
  { geometry: "area", label: "diffLegendArea" },
];

interface LookShapeProps {
  look: DiffLook;
  geometry: LegendGeometry;
  state: DiffSide;
  color: string;
  rim: string;
}

/** One state of a geometry in the look the map draws it in. */
const LookShape: FC<LookShapeProps> = ({ look, geometry, state, color, rim }) => {
  const stroke = {
    crisp: { stroke: color, strokeWidth: 2.5 },
    dotted: { stroke: color, strokeWidth: 3.5, strokeDasharray: "0.1 6", strokeLinecap: "round" as const },
    ghost: { stroke: color, strokeOpacity: 0.4, strokeWidth: 9, strokeLinecap: "round" as const },
    dashed: { stroke: color, strokeWidth: 3, strokeDasharray: "8 6" },
  }[look];
  const fillOpacity = { crisp: 0.25, dotted: 0.25, ghost: 0.08, dashed: 0.1 }[look];
  switch (geometry) {
    case "point":
      if (look === "ghost")
        return (
          <circle
            cx="22"
            cy="14"
            r="10"
            fill={color}
            fillOpacity={0.12}
            stroke={color}
            strokeOpacity={0.75}
            strokeWidth="3"
          />
        );
      if (look === "dashed")
        return (
          <circle
            cx="22"
            cy="14"
            r="7"
            fill={color}
            fillOpacity={0.15}
            stroke={color}
            strokeWidth="2.5"
            strokeDasharray="3 3"
          />
        );
      return <circle cx="22" cy="14" r="5" fill={color} stroke={rim} strokeWidth="1.5" />;
    case "line":
      return <polyline points={LINES[state]} fill="none" {...stroke} />;
    case "area":
      return (
        <rect
          {...AREAS[state]}
          width="24"
          height="15"
          fill={color}
          fillOpacity={fillOpacity}
          {...stroke}
          strokeWidth={look === "ghost" ? 7 : stroke.strokeWidth}
        />
      );
  }
};

const DiffMapLegend: FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const heading = (label: TranslationKey) => (
    <Typography variant="caption" sx={{ color: "text.secondary", textAlign: "center" }}>
      {t(label)}
    </Typography>
  );
  return (
    <Box
      data-cy="diff-map-legend"
      sx={{
        p: 1.5,
        maxWidth: 280,
        bgcolor: "background.content",
        border: 1,
        borderColor: "primary.light",
        borderRadius: theme.radius.default,
      }}>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: "auto repeat(3, 44px)",
          columnGap: 1,
          rowGap: 0.5,
          alignItems: "center",
        }}>
        <span />
        {LEGEND_GEOMETRIES.map(column => (
          <Fragment key={column.geometry}>{heading(column.label)}</Fragment>
        ))}
        {LEGEND_ROWS.map(row => (
          <Fragment key={row.changeType}>
            <Typography variant="body2">{t(row.label)}</Typography>
            {LEGEND_GEOMETRIES.map(column => (
              <svg key={column.geometry} width="44" height="28" aria-hidden>
                {STATES_OF[row.changeType].map(state => (
                  <LookShape
                    key={state}
                    look={getLook(row.changeType, state)}
                    geometry={column.geometry}
                    state={state}
                    color={theme.palette.map.diff[row.changeType]}
                    rim={theme.palette.map.stroke}
                  />
                ))}
              </svg>
            ))}
          </Fragment>
        ))}
      </Box>
      <Typography variant="caption" component="p" sx={{ mt: 1, color: "text.secondary" }}>
        {t("diffLegendChangedHint")}
      </Typography>
    </Box>
  );
};

/** The legend button, grouped with the layers button, and the legend beside it. */
export const DiffMapLegendButton: FC = () => {
  const [open, setOpen] = useState(false);
  const [button, setButton] = useState<HTMLButtonElement | null>(null);
  const legendId = useId();
  const close = useCallback(() => setOpen(false), []);

  return (
    <>
      <MapPanelPopper id={legendId} open={open} anchorEl={button} onClose={close}>
        <DiffMapLegend />
      </MapPanelPopper>
      <IconButton
        ref={setButton}
        color="primaryOutlined"
        icon={<FormatListBulletedIcon />}
        label="diffMapLegend"
        tooltipPlacement="left"
        aria-expanded={open}
        aria-controls={open ? legendId : undefined}
        sx={{ bgcolor: open ? "primary.states.selected" : undefined }}
        onClick={() => setOpen(previous => !previous)}
      />
    </>
  );
};

interface DiffMapSliderButtonProps {
  /** 0 shows only the old state, 1 only the new one; null means the slider is off. */
  slider: number | null;
  onSliderChange: (slider: number | null) => void;
  /** Only one state has changes on the map, so there is nothing to fade between. */
  disabled: boolean;
}

/** The slider button, in a group of its own above the legend and the layers. */
export const DiffMapSliderButton: FC<DiffMapSliderButtonProps> = ({ slider, onSliderChange, disabled }) => {
  const sliderOn = slider !== null;
  return (
    <ButtonGroup orientation="vertical">
      <IconButton
        color="primaryOutlined"
        icon={<CompareIcon />}
        label={disabled ? "diffMapSliderOneState" : "diffMapSlider"}
        data-cy="diffMapSlider-button"
        disabled={disabled}
        tooltipPlacement="left"
        aria-pressed={sliderOn}
        sx={{ bgcolor: sliderOn ? "primary.states.selected" : undefined }}
        onClick={() => onSliderChange(sliderOn ? null : 0)}
      />
    </ButtonGroup>
  );
};

interface DiffMapSliderProps {
  value: number;
  onChange: (value: number) => void;
}

/** Fades the map from the old state on the left to the new one on the right. */
export const DiffMapSlider: FC<DiffMapSliderProps> = ({ value, onChange }) => {
  const { t } = useTranslation();
  const theme = useTheme();
  return (
    <Stack
      direction="row"
      data-cy="diff-map-slider"
      sx={{
        position: "absolute",
        bottom: 0,
        left: "50%",
        transform: "translateX(-50%)",
        mb: { xs: 1, sm: 2 },
        width: "min(420px, calc(100% - 140px))",
        gap: 2,
        px: 2,
        py: 0.5,
        alignItems: "center",
        bgcolor: "background.content",
        border: 1,
        borderColor: "primary.light",
        borderRadius: theme.radius.default,
      }}>
      <Typography variant="body2">{t("diffColumnOldValue")}</Typography>
      <Slider
        size="small"
        min={0}
        max={1}
        step={0.01}
        value={value}
        onChange={(_, next) => onChange(Array.isArray(next) ? next[0] : next)}
        aria-label={t("diffMapSlider")}
        getAriaValueText={share => `${Math.round(share * 100)} %`}
      />
      <Typography variant="body2">{t("diffColumnNewValue")}</Typography>
    </Stack>
  );
};
