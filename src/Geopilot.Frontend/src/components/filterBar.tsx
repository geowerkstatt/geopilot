import { ReactNode, useState } from "react";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import { Badge, Box, Stack } from "@mui/material";
import { TranslationKey } from "../appInterfaces";
import { Button, IconButton } from "./buttons";
import { SearchField } from "./searchField";

interface FilterBarProps {
  searchPlaceholder: TranslationKey;
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  /** How many of the filter fields hold a value. */
  activeFilterCount: number;
  onClearFilters: () => void;
  /** Hide the filter fields behind a toggle, for a panel with little room. */
  collapsible?: boolean;
  /** How many filter fields share a row on a wide screen. */
  columns?: number;
  /** Stack the filter fields, for a narrow panel on a wide screen. */
  forceMobileView?: boolean;
  dataCy?: string;
  /** The filter fields. */
  children?: ReactNode;
}

/** A search field with filter fields below it, and a button to clear both once something is set. */
export const FilterBar = ({
  searchPlaceholder,
  searchQuery,
  onSearchQueryChange,
  activeFilterCount,
  onClearFilters,
  collapsible,
  columns = 2,
  forceMobileView,
  dataCy,
  children,
}: FilterBarProps) => {
  const [showFilters, setShowFilters] = useState(false);

  const hasActiveFilters = searchQuery.trim().length > 0 || activeFilterCount > 0;
  const filtersShown = children && (!collapsible || showFilters);
  // Emphasize the toggle (filled) while the filter panel is open or filters are in effect.
  const toggleActive = showFilters || activeFilterCount > 0;

  return (
    <Stack sx={{ width: "100%", gap: 1 }} data-cy={dataCy}>
      <Stack sx={{ gap: collapsible ? 0 : 2 }}>
        <Stack direction="row" sx={{ alignItems: "stretch" }}>
          <SearchField
            placeholder={searchPlaceholder}
            sx={{ flex: 1 }}
            value={searchQuery}
            onChange={onSearchQueryChange}
          />
          {collapsible && children && (
            <Badge badgeContent={activeFilterCount} color="secondary" sx={{ display: "flex", alignItems: "stretch" }}>
              <IconButton
                color="primaryOutlined"
                className={toggleActive ? "active" : undefined}
                onClick={() => setShowFilters(show => !show)}
                icon={<FilterAltIcon />}
                label="toggleFilters"
              />
            </Badge>
          )}
        </Stack>
        {filtersShown && (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: forceMobileView ? undefined : "repeat(2, minmax(0, 1fr))",
                md: forceMobileView ? undefined : `repeat(${columns}, minmax(0, 1fr))`,
              },
              gap: 2,
              width: "100%",
            }}>
            {children}
          </Box>
        )}
      </Stack>
      {hasActiveFilters && (
        <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
          <Button size="small" variant="text" label="resetFilters" onClick={onClearFilters} />
        </Stack>
      )}
    </Stack>
  );
};
