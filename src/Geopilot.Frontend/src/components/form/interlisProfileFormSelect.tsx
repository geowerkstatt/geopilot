import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Profile } from "../../api/apiInterfaces";
import i18n from "../../i18n";
import { FormSelect, FormSelectValue } from "./formSelect";

interface InterlisProfileFormSelectProps {
  profiles?: Profile[];
  selected?: string;
}

const getLocalisedProfileTitle = (profile: Profile, language: string): string => {
  if (!profile.titles || profile.titles.length === 0) {
    return profile.id;
  }

  const localTitle = profile.titles.find(title => title.language === language);
  if (localTitle) {
    return localTitle.text || profile.id;
  }

  const fallbackTitle = profile.titles.find(title => !title.language);
  if (fallbackTitle) {
    return fallbackTitle.text;
  }

  return profile.id;
};

const getProfileSelectMenuItems = (
  profiles?: Profile[],
  language?: string,
  t?: (key: string) => string,
): FormSelectValue[] => {
  return (
    profiles?.map((profile, idx) => ({
      key: idx,
      value: profile.id,
      name: `${getLocalisedProfileTitle(profile, language ?? "de")} (${t ? t("id") : "ID"}: ${profile.id})`,
    })) ?? []
  );
};

export const InterlisProfileFormSelect: FC<InterlisProfileFormSelectProps> = ({ profiles, selected }) => {
  const { t } = useTranslation();

  return (
    <FormSelect
      fieldName="interlisValidationProfile"
      label="validationProfile"
      required={false}
      selected={selected}
      values={getProfileSelectMenuItems(profiles, i18n.language, t)}
    />
  );
};

export default InterlisProfileFormSelect;
