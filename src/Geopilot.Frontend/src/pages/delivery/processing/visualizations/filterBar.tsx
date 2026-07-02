import { useState } from "react";
import { useTranslation } from "react-i18next";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import { Badge, Box, Button, Stack } from "@mui/material";
import { IconButton } from "../../../../components/buttons";
import { FormAutocomplete } from "../../../../components/form/formAutocomplete";
import { SearchField } from "../../../../components/searchField";
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
        <SearchField
          placeholder="treeVisualizationMessageSearch"
          sx={{ flex: 1 }}
          value={messageQuery}
          onChange={onMessageQueryChange}
        />
        {attributes.length > 0 && (
          <Badge badgeContent={activeFilterCount} color="secondary" sx={{ display: "flex", alignItems: "stretch" }}>
            <IconButton
              color="primaryOutlined"
              className={toggleActive ? "active" : undefined}
              onClick={() => setShowFilters(show => !show)}
              label="treeFilterToggle">
              <FilterAltIcon />
            </IconButton>
          </Badge>
        )}
      </Stack>
      {showFilters && attributes.length > 0 && (
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 1.5, width: "100%" }}>
          {attributes.map(attribute => (
            <FormAutocomplete
              key={attribute.key}
              label={attribute.key}
              values={attribute.options}
              selected={metadataFilters[attribute.key] ?? []}
              onChange={value => onMetadataFilterChange(attribute.key, value as string[])}
              dataCy={`metadata-filter-${attribute.key}`}
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
