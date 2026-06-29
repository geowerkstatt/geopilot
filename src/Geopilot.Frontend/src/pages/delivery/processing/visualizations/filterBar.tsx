import { useTranslation } from "react-i18next";
import { TextField } from "@mui/material";
import { FlexRowBox } from "../../../../components/styledComponents";
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
      <FlexRowBox sx={{ width: "100%", gap: 1.5 }}>
        {attributes.map(attribute => (
          <MetadataFilter
            key={attribute.key}
            attribute={attribute}
            selected={metadataFilters[attribute.key] ?? []}
            onChange={onMetadataFilterChange}
          />
        ))}
      </FlexRowBox>
    </>
  );
};
