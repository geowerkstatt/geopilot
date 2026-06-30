import { useTranslation } from "react-i18next";
import { Stack, TextField } from "@mui/material";
import { MetadataFilter } from "./metadataFilter";
import { MetadataAttribute, MetadataFilters } from "./treeNode";

interface FilterBarProps {
  attributes: MetadataAttribute[];
  messageQuery: string;
  onMessageQueryChange: (value: string) => void;
  metadataFilters: MetadataFilters;
  onMetadataFilterChange: (key: string, selected: string[]) => void;
}

export const FilterBar = ({
  attributes,
  messageQuery,
  onMessageQueryChange,
  metadataFilters,
  onMetadataFilterChange,
}: FilterBarProps) => {
  const { t } = useTranslation();

  return (
    <>
      <TextField
        size="small"
        variant="outlined"
        label={t("treeVisualizationMessageSearch")}
        sx={{ width: "100%" }}
        value={messageQuery}
        onChange={event => onMessageQueryChange(event.target.value)}
        data-cy="tree-message-search"
      />
      <Stack direction="row" sx={{ width: "100%", gap: 1.5, alignItems: "center", flexWrap: "wrap" }}>
        {attributes.map(attribute => (
          <MetadataFilter
            key={attribute.key}
            attribute={attribute}
            selected={metadataFilters[attribute.key] ?? []}
            onChange={onMetadataFilterChange}
          />
        ))}
      </Stack>
    </>
  );
};
