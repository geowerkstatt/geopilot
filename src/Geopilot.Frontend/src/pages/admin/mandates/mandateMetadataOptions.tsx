import { FC } from "react";
import { useFieldArray, useFormContext, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Box, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { MetadataFieldType } from "../../../api/generated";
import { Language } from "../../../appInterfaces.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import { FormContainer, FormInput, FormLocalizedInput } from "../../../components/form/form.ts";
import { useIdRecheck, validateMetadataId } from "./metadataIdValidation.ts";

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
  const { otherIds, removeEntry } = useIdRecheck(optionsName, fields.length, remove);

  // The type of the field refuses a selection without options, so it is checked again whenever the list changes.
  const addOption = () => {
    append({ id: "", title: {} });
    trigger(typeName);
  };
  const removeOption = (optionIndex: number) => {
    removeEntry(optionIndex);
    trigger(typeName);
  };

  if (type !== MetadataFieldType.Selection) {
    return null;
  }

  return (
    <Stack sx={{ paddingLeft: 4 }}>
      <Typography variant={"body2"}>{t("metadataFieldOptions")}</Typography>
      {fields.map((option, optionIndex) => (
        <FormContainer key={option.key}>
          <FormInput
            fieldName={`${optionsName}.${optionIndex}.id`}
            label={"id"}
            required={true}
            value={option.id}
            validate={id => validateMetadataId(id, getValues(optionsName))}
            deps={otherIds(optionIndex)}
          />
          <FormLocalizedInput
            fieldName={`${optionsName}.${optionIndex}.title`}
            label={"metadataFieldTitle"}
            value={option.title}
            activeLanguage={activeLanguage}
            requireAtLeastOne={true}
          />
          <IconButton
            icon={<DeleteOutlinedIcon />}
            label={"removeMetadataFieldOption"}
            onClick={() => removeOption(optionIndex)}
            sx={{ alignSelf: "center" }}
          />
        </FormContainer>
      ))}
      <Box>
        <Button startIcon={<AddIcon />} label={"addMetadataFieldOption"} onClick={addOption} />
      </Box>
    </Stack>
  );
};

export default MandateMetadataOptions;
