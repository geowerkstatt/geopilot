import { FC, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Stack, ToggleButton } from "@mui/material";
import { DiffChange, DiffChangeType } from "../../../api/apiInterfaces";
import { Button } from "../../buttons";
import { SearchField } from "../../searchField";
import { countByChangeType, DIFF_CHANGE_TYPE_LABEL_KEYS, DIFF_CHANGE_TYPES } from "./diffChanges";
import { DiffChangeTypeIcon } from "./diffChangeTypeIcon";

interface DiffFilterBarProps {
  /** All changes of the diff, to count them per change type. */
  changes: DiffChange[];
  query: string;
  onQueryChange: (query: string) => void;
  /** The selected change types; none selected shows all of them. */
  changeTypes: DiffChangeType[];
  onChangeTypesChange: (changeTypes: DiffChangeType[]) => void;
}

/** Filters the changes by a search text and by their change type. The filter applies to the table and the map. */
export const DiffFilterBar: FC<DiffFilterBarProps> = ({
  changes,
  query,
  onQueryChange,
  changeTypes,
  onChangeTypesChange,
}) => {
  const { t } = useTranslation();
  const counts = useMemo(() => countByChangeType(changes), [changes]);
  const hasActiveFilters = query.trim().length > 0 || changeTypes.length > 0;

  const toggleChangeType = (changeType: DiffChangeType) =>
    onChangeTypesChange(
      changeTypes.includes(changeType) ? changeTypes.filter(type => type !== changeType) : [...changeTypes, changeType],
    );

  const clearFilters = () => {
    onQueryChange("");
    onChangeTypesChange([]);
  };

  return (
    <Stack sx={{ width: "100%", gap: 1 }}>
      <SearchField placeholder="diffSearch" value={query} onChange={onQueryChange} dataCy="diff-search" />
      <Stack direction="row" sx={{ gap: 1, flexWrap: "wrap", alignItems: "center" }}>
        {/* Single buttons: unlike the joined bar of a ToggleButtonGroup, they wrap in a narrow panel. */}
        <Stack direction="row" role="group" aria-label={t("diffColumnChange")} sx={{ gap: 1, flexWrap: "wrap" }}>
          {DIFF_CHANGE_TYPES.map(changeType => (
            <ToggleButton
              key={changeType}
              size="small"
              value={changeType}
              selected={changeTypes.includes(changeType)}
              disabled={counts[changeType] === 0}
              onChange={() => toggleChangeType(changeType)}
              sx={{ gap: 1 }}
              data-cy={`diff-filter-${changeType}`}>
              <DiffChangeTypeIcon changeType={changeType} color="inherit" />
              {`${t(DIFF_CHANGE_TYPE_LABEL_KEYS[changeType])} (${counts[changeType]})`}
            </ToggleButton>
          ))}
        </Stack>
        {hasActiveFilters && <Button size="small" variant="text" label="treeFilterReset" onClick={clearFilters} />}
      </Stack>
    </Stack>
  );
};
