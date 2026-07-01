import { useState } from "react";
import { useTranslation } from "react-i18next";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import SearchIcon from "@mui/icons-material/Search";
import { Badge, Box, Button, IconButton, InputAdornment, Stack, TextField, Tooltip } from "@mui/material";
import { MetadataFilter } from "./metadataFilter";
import { MetadataAttribute, MetadataFilters } from "./treeNode";

interface FilterBarProps {
  attributes: MetadataAttribute[];
  messageQuery: string;
  onMessageQueryChange: (value: string) => void;
  metadataFilters: MetadataFilters;
  onMetadataFilterChange: (key: string, selected: string[]) => void;
  onClearFilters: () => void;
}

export const FilterBar = ({
  attributes,
  messageQuery,
  onMessageQueryChange,
  metadataFilters,
  onMetadataFilterChange,
  onClearFilters,
}: FilterBarProps) => {
  const { t } = useTranslation();
  const [showFilters, setShowFilters] = useState(false);

  const activeFilterCount = Object.values(metadataFilters).filter(values => values.length > 0).length;
  const hasActiveFilters = messageQuery.trim().length > 0 || activeFilterCount > 0;
  // Emphasize the toggle (filled) while the filter panel is open or filters are in effect.
  const toggleActive = showFilters || activeFilterCount > 0;

  return (
    <Stack sx={{ width: "100%", gap: 1.5 }}>
      <Stack direction="row" sx={{ gap: 1, alignItems: "stretch" }}>
        <TextField
          size="small"
          variant="outlined"
          placeholder={t("treeVisualizationMessageSearch")}
          sx={{ flex: 1 }}
          value={messageQuery}
          onChange={event => onMessageQueryChange(event.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </InputAdornment>
            ),
          }}
          inputProps={{ "aria-label": t("treeVisualizationMessageSearch") }}
          data-cy="tree-message-search"
        />
        {attributes.length > 0 && (
          <Tooltip title={t("treeFilterToggle")}>
            <Badge badgeContent={activeFilterCount} color="primary" sx={{ display: "flex", alignItems: "stretch" }}>
              <IconButton
                onClick={() => setShowFilters(show => !show)}
                aria-label={t("treeFilterToggle")}
                data-cy="tree-filter-toggle"
                sx={{
                  aspectRatio: "1 / 1",
                  height: "auto",
                  border: 1,
                  borderColor: toggleActive ? "primary.light" : "divider",
                  borderRadius: 1,
                  // Keep the icon dark on a light selected fill (per design) instead of inverting to white.
                  color: "primary.main",
                  backgroundColor: toggleActive ? "primary.selected" : "transparent",
                  "&:hover": { backgroundColor: toggleActive ? "primary.selected" : "primary.hover" },
                  "&:focus, &:focus-visible, &:active": {
                    backgroundColor: toggleActive ? "primary.selected" : "transparent",
                  },
                }}>
                <FilterAltIcon />
              </IconButton>
            </Badge>
          </Tooltip>
        )}
      </Stack>
      {showFilters && attributes.length > 0 && (
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 1.5, width: "100%" }}>
          {attributes.map(attribute => (
            <MetadataFilter
              key={attribute.key}
              attribute={attribute}
              selected={metadataFilters[attribute.key] ?? []}
              onChange={onMetadataFilterChange}
            />
          ))}
        </Box>
      )}
      <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
        <Button
          variant="text"
          size="small"
          onClick={onClearFilters}
          disabled={!hasActiveFilters}
          data-cy="tree-filter-reset">
          {t("treeFilterReset")}
        </Button>
      </Stack>
    </Stack>
  );
};
