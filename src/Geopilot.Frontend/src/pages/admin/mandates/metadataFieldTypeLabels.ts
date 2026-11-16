import { MetadataFieldType } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces.ts";

/** The label of each metadata field type, in the order the type select offers them. */
export const metadataFieldTypeLabels: Record<MetadataFieldType, TranslationKey> = {
  [MetadataFieldType.Text]: "metadataFieldTypeText",
  [MetadataFieldType.TextList]: "metadataFieldTypeTextList",
  [MetadataFieldType.Selection]: "metadataFieldTypeSelection",
  [MetadataFieldType.Integer]: "metadataFieldTypeInteger",
  [MetadataFieldType.Decimal]: "metadataFieldTypeDecimal",
  [MetadataFieldType.Date]: "metadataFieldTypeDate",
};
