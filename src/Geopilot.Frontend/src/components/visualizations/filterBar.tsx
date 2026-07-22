import { useState } from "react";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import { Badge, Box, Stack } from "@mui/material";
import { Button, IconButton } from "../buttons";
import { FormAutocomplete } from "../form/formAutocomplete";
import { SearchField } from "../searchField";
import { MetadataAttribute, MetadataFilters } from "./tree/treeNode";

interface FilterBarProps {
  attributes: MetadataAttribute[];
  messageQuery: string;
  onMessageQueryChange: (value: string) => void;
  metadataFilters: MetadataFilters;
  onMetadataFilterChange: (key: string, selected: string[]) => void;
  onClearFilters: () => void;
  forceMobileView?: boolean;
}

export const FilterBar = ({
  attributes,
  messageQuery,
  onMessageQueryChange,
  metadataFilters,
  onMetadataFilterChange,
  onClearFilters,
  forceMobileView,
}: FilterBarProps) => {
  const [showFilters, setShowFilters] = useState(false);

  const activeFilterCount = Object.values(metadataFilters).filter(values => values.length > 0).length;
  const hasActiveFilters = messageQuery.trim().length > 0 || activeFilterCount > 0;
  // Emphasize the toggle (filled) while the filter panel is open or filters are in effect.
  const toggleActive = showFilters || activeFilterCount > 0;

  return (
    <Stack sx={{ width: "100%", gap: 1 }}>
      <Stack>
        <Stack direction="row" sx={{ alignItems: "stretch" }}>
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
                icon={<FilterAltIcon />}
                label="treeFilterToggle"
              />
            </Badge>
          )}
        </Stack>
        {showFilters && attributes.length > 0 && (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: forceMobileView ? undefined : "repeat(2, minmax(0, 1fr))" },
              gap: 2,
              width: "100%",
            }}>
            {attributes.map(attribute => (
              <FormAutocomplete
                key={attribute.key}
                label={attribute.key}
                values={attribute.options}
                selected={metadataFilters[attribute.key] ?? []}
                onChange={value => onMetadataFilterChange(attribute.key, value as string[])}
                dataCy={`metadata-filter-${attribute.key}`}
                sx={{ minWidth: "0" }}
              />
            ))}
          </Box>
        )}
      </Stack>
      {hasActiveFilters && (
        <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
          <Button size="small" variant="text" label="treeFilterReset" onClick={onClearFilters} />
        </Stack>
      )}
    </Stack>
  );
};
