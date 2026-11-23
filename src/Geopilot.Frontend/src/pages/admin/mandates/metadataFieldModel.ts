import { LocalizedText } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataField, MetadataFieldOption, MetadataFieldType } from "../../../api/generated";
import { Language, TranslationKey } from "../../../appInterfaces.ts";
import { resolveLocalized } from "../../../hooks/useLocalized.ts";

/** A language in which a title reads the same as the title of another entry of the list. */
export interface TitleConflict {
  language: string;
  otherIndex: number;
}

/**
 * The first conflict of the title at the index with another title of the list. A title reads in a language as geopilot
 * shows it there: its own entry or, where that is missing, the fallback. The server refuses the same when it saves.
 */
export const findTitleConflict = (titles: LocalizedText[], index: number): TitleConflict | undefined => {
  const title = titles[index];
  for (const [otherIndex, other] of titles.entries()) {
    if (otherIndex === index) {
      continue;
    }
    const languages = new Set([...Object.values(Language), ...Object.keys(title), ...Object.keys(other)]);
    for (const language of languages) {
      const shown = resolveLocalized(title, language);
      if (shown !== "" && shown === resolveLocalized(other, language)) {
        return { language, otherIndex };
      }
    }
  }
  return undefined;
};

// Every language is set: in the dialog, an input without a value falls back to the value the dialog opened with,
// which for a reset or re-added option is the title of an option that is gone.
const emptyTitle = (): LocalizedText => Object.fromEntries(Object.values(Language).map(language => [language, ""]));

export const newMetadataFieldOption = (): MetadataFieldOption => ({ id: "", title: emptyTitle() });

export const newMetadataField = (): MetadataField => ({
  id: "",
  title: emptyTitle(),
  type: MetadataFieldType.Text,
  evaluation: FieldEvaluationType.Optional,
  options: [],
});

export const metadataFieldTypeLabels: Record<MetadataFieldType, TranslationKey> = {
  [MetadataFieldType.Text]: "metadataFieldTypeText",
  [MetadataFieldType.TextList]: "metadataFieldTypeTextList",
  [MetadataFieldType.Selection]: "metadataFieldTypeSelection",
  [MetadataFieldType.Integer]: "metadataFieldTypeInteger",
  [MetadataFieldType.Decimal]: "metadataFieldTypeDecimal",
  [MetadataFieldType.Date]: "metadataFieldTypeDate",
};
