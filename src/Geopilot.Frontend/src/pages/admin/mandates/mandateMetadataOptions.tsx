import { FC, Fragment } from "react";
import { useFieldArray, useFormContext, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Divider, Stack, Typography } from "@mui/material";
import { LocalizedText, MandateFormValues } from "../../../api/apiInterfaces.ts";
import { MetadataFieldType } from "../../../api/generated";
import { Language } from "../../../appInterfaces.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import { FormColumnHalfWidth, FormContainer, FormInput, FormLocalizedInput } from "../../../components/form/form.ts";
import { findTitleConflict, newMetadataFieldOption } from "./metadataFieldModel.ts";
import { useDuplicateRecheck, validateMetadataId } from "./metadataIdValidation.ts";

interface MandateMetadataOptionsProps {
  fieldIndex: number;
  activeLanguage: Language;
}

/** The options of a metadata field, listed while the field is a selection. */
const MandateMetadataOptions: FC<MandateMetadataOptionsProps> = ({ fieldIndex, activeLanguage }) => {
  const { t } = useTranslation();
  const { getValues, trigger } = useFormContext<MandateFormValues>();
  const typeName = `metadataFields.${fieldIndex}.type` as const;
  const optionsName = `metadataFields.${fieldIndex}.options` as const;
  const type = useWatch<MandateFormValues, typeof typeName>({ name: typeName });
  const { fields, append, remove } = useFieldArray<MandateFormValues, typeof optionsName, "key">({
    name: optionsName,
    keyName: "key",
  });
  const { otherIds, otherTitles, removeEntry } = useDuplicateRecheck(optionsName, fields.length, remove);

  // The type of the field refuses a selection without options, so it is checked again whenever the list changes.
  const addOption = () => {
    append(newMetadataFieldOption());
    trigger(typeName);
  };
  const removeOption = (optionIndex: number) => {
    removeEntry(optionIndex);
    trigger(typeName);
  };

  const validateTitle = (optionIndex: number, title: LocalizedText) => {
    const options = getValues(optionsName) ?? [];
    const conflict = findTitleConflict(
      options.map((option, other) => (other === optionIndex ? title : option.title)),
      optionIndex,
    );
    return conflict
      ? t("metadataOptionTitleConflict", {
          language: conflict.language.toUpperCase(),
          position: conflict.otherIndex + 1,
        })
      : true;
  };

  if (type !== MetadataFieldType.Selection) {
    return null;
  }

  return (
    <Stack>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", marginTop: 1 }}>
        <Typography variant={"h4"} sx={{ margin: 0 }}>
          {t("metadataFieldOptions")}
        </Typography>
        <Button startIcon={<AddIcon />} label={"addMetadataFieldOption"} onClick={addOption} />
      </Stack>
      <Stack spacing={0}>
        {fields.map((option, optionIndex) => (
          <Fragment key={option.key}>
            {optionIndex > 0 && <Divider sx={{ marginTop: 1, marginBottom: 2 }} />}
            <FormContainer role="group" aria-label={t("metadataFieldOptionGroup", { position: optionIndex + 1 })}>
              <FormColumnHalfWidth>
                <FormInput
                  fieldName={`${optionsName}.${optionIndex}.id`}
                  label={"id"}
                  required={true}
                  value={option.id}
                  validate={id => validateMetadataId(t, id, getValues(optionsName))}
                  deps={otherIds(optionIndex)}
                />
              </FormColumnHalfWidth>
              {/* Top-aligned: the delete icon is as tall as the title input, so it centers on the input and leaves
                the hint below it out. */}
              <FormColumnHalfWidth direction="row" sx={{ alignItems: "flex-start" }}>
                <FormLocalizedInput
                  fieldName={`${optionsName}.${optionIndex}.title`}
                  label={"metadataFieldTitle"}
                  value={option.title}
                  activeLanguage={activeLanguage}
                  requireAtLeastOne={true}
                  validate={title => validateTitle(optionIndex, title)}
                  deps={otherTitles(optionIndex)}
                  helperText={t("atLeastOneLanguageRequired")}
                />
                {/* A selection needs at least one option. */}
                <IconButton
                  icon={<DeleteOutlinedIcon />}
                  label={"removeMetadataFieldOption"}
                  labelValues={{ option: optionIndex + 1, field: fieldIndex + 1 }}
                  disabled={fields.length === 1}
                  onClick={() => removeOption(optionIndex)}
                />
              </FormColumnHalfWidth>
            </FormContainer>
          </Fragment>
        ))}
      </Stack>
    </Stack>
  );
};

export default MandateMetadataOptions;
