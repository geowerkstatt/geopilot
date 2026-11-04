import { FC, useEffect, useState } from "react";
import { Controller, FieldValues, useFieldArray, useFormContext, useFormState } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Box, Divider, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataFieldType } from "../../../api/generated";
import { Language, TranslationKey } from "../../../appInterfaces.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import {
  FormCheckbox,
  FormColumnHalfWidth,
  FormContainer,
  FormInput,
  FormLanguageTabs,
  FormLocalizedInput,
  FormSelect,
  getFormFieldErrorMessage,
} from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import MandateMetadataOptions from "./mandateMetadataOptions.tsx";
import { useIdRecheck, validateMetadataId } from "./metadataIdValidation.ts";

interface MandateMetadataFieldsProps {
  mandate?: MandateFormValues;
}

const typeLabels: Record<MetadataFieldType, TranslationKey> = {
  [MetadataFieldType.Text]: "metadataFieldTypeText",
  [MetadataFieldType.TextList]: "metadataFieldTypeTextList",
  [MetadataFieldType.Selection]: "metadataFieldTypeSelection",
  [MetadataFieldType.Integer]: "metadataFieldTypeInteger",
  [MetadataFieldType.Decimal]: "metadataFieldTypeDecimal",
  [MetadataFieldType.Date]: "metadataFieldTypeDate",
};

/**
 * The additional fields a deliverer fills in for the mandate, in the order the delivery form shows them, with a
 * language choice of their own for the titles. A new field goes to the end. Whether the titles differ is checked by
 * the server alone; its message shows when saving fails.
 */
const MandateMetadataFields: FC<MandateMetadataFieldsProps> = ({ mandate }) => {
  const { t, i18n } = useTranslation();
  const [activeLanguage, setActiveLanguage] = useState<Language>(i18n.resolvedLanguage as Language);
  const { clearErrors, control, getValues, setValue } = useFormContext<MandateFormValues>();
  const { errors } = useFormState<FieldValues>({ name: "metadataFields" });
  // A field has an id of its own, which the key that tracks the entries would otherwise replace.
  const { fields, append, remove } = useFieldArray<MandateFormValues, "metadataFields", "key">({
    name: "metadataFields",
    keyName: "key",
  });
  const { otherIds, removeEntry } = useIdRecheck("metadataFields", fields.length, remove);

  // The form has no default values, so the list is filled here. Unlike replace or append, setValue does not mark the
  // form as changed, so reset and save stay disabled until the admin edits something.
  useEffect(() => {
    setValue("metadataFields", mandate?.metadataFields ?? []);
  }, [mandate, setValue]);

  const typeValues: FormSelectValue[] = Object.entries(typeLabels).map(([value, label], key) => ({
    key,
    value,
    name: t(label),
  }));

  const addField = () =>
    append({ id: "", title: {}, type: MetadataFieldType.Text, evaluation: FieldEvaluationType.Optional, options: [] });

  const validateType = (index: number, type: number | string) =>
    type !== MetadataFieldType.Selection ||
    (getValues(`metadataFields.${index}.options`)?.length ?? 0) > 0 ||
    "metadataOptionsRequired";

  // FormSelect shows no message of a nested field, so the reason goes in as helper text, which MUI links to the select.
  const typeHelperText = (index: number) =>
    getFormFieldErrorMessage(`metadataFields.${index}.type`, errors) === "metadataOptionsRequired"
      ? t("metadataOptionsRequired")
      : undefined;

  // The options of a field that is no selection are hidden, and an error they carry would keep saving disabled with
  // nothing on screen.
  const clearHiddenOptionErrors = (index: number) => {
    if (getValues(`metadataFields.${index}.type`) !== MetadataFieldType.Selection) {
      clearErrors(`metadataFields.${index}.options`);
    }
  };

  return (
    <>
      <Stack direction="row" sx={{ justifyContent: "space-between" }}>
        <Typography variant={"body1"}>{t("metadataFields")}</Typography>
        <FormLanguageTabs
          language={activeLanguage}
          onLanguageChange={setActiveLanguage}
          dataCy={"metadataFields-language"}
          label={"metadataFieldsInputLanguage"}
        />
      </Stack>
      {fields.map((field, index) => (
        // Every field repeats the same labels, so the group names which field they belong to.
        <Stack key={field.key} role="group" aria-label={t("metadataFieldGroup", { position: index + 1 })}>
          <FormContainer>
            <FormInput
              fieldName={`metadataFields.${index}.id`}
              label={"id"}
              required={true}
              value={field.id}
              validate={id => validateMetadataId(id, getValues("metadataFields"))}
              deps={otherIds(index)}
            />
            <FormLocalizedInput
              fieldName={`metadataFields.${index}.title`}
              label={"metadataFieldTitle"}
              value={field.title}
              activeLanguage={activeLanguage}
              requireAtLeastOne={true}
            />
          </FormContainer>
          <FormContainer>
            <FormColumnHalfWidth>
              <FormSelect
                fieldName={`metadataFields.${index}.type`}
                label={"metadataFieldType"}
                required={true}
                selected={field.type}
                values={typeValues}
                validate={type => validateType(index, type)}
                helperText={typeHelperText(index)}
                onUpdate={() => clearHiddenOptionErrors(index)}
              />
            </FormColumnHalfWidth>
            <FormColumnHalfWidth direction="row" sx={{ alignItems: "center", justifyContent: "space-between" }}>
              {/* The checkbox edits the evaluation, which the mandate keeps as optional or required. */}
              <Controller
                name={`metadataFields.${index}.evaluation`}
                control={control}
                render={({ field: evaluation }) => (
                  <FormCheckbox
                    fieldName={evaluation.name}
                    label={"metadataFieldRequired"}
                    checked={evaluation.value === FieldEvaluationType.Required}
                    onChange={checked =>
                      evaluation.onChange(checked ? FieldEvaluationType.Required : FieldEvaluationType.Optional)
                    }
                  />
                )}
              />
              <IconButton
                icon={<DeleteOutlinedIcon />}
                label={"removeMetadataField"}
                labelValues={{ position: index + 1 }}
                onClick={() => removeEntry(index)}
              />
            </FormColumnHalfWidth>
          </FormContainer>
          <MandateMetadataOptions fieldIndex={index} activeLanguage={activeLanguage} />
          <Divider />
        </Stack>
      ))}
      <Box>
        <Button startIcon={<AddIcon />} label={"addMetadataField"} onClick={addField} />
      </Box>
    </>
  );
};

export default MandateMetadataFields;
