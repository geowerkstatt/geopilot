import { FC, useState } from "react";
import { Controller, FormProvider, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Dialog, DialogActions, DialogContent, DialogTitle, Stack } from "@mui/material";
import { LocalizedText, MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataField, MetadataFieldType } from "../../../api/generated";
import { isLanguage, Language } from "../../../appInterfaces.ts";
import { Button } from "../../../components/buttons.tsx";
import {
  FormCheckbox,
  FormColumnHalfWidth,
  FormContainer,
  FormInput,
  FormLanguageTabs,
  FormLocalizedInput,
  FormSelect,
} from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import MandateMetadataOptions from "./mandateMetadataOptions.tsx";
import { findTitleConflict, metadataFieldTypeLabels, newMetadataFieldOption } from "./metadataFieldModel.ts";
import { idFieldNames, validateMetadataId } from "./metadataIdValidation.ts";

type MetadataFieldFormValues = Pick<MandateFormValues, "metadataFields">;

interface MetadataFieldDialogProps {
  /** Every field, the edited one included, so its id is checked against the others. */
  fields: MetadataField[];
  index: number;
  isNew: boolean;
  onApply: (field: MetadataField) => void;
  onRemove: () => void;
  onClose: () => void;
}

/**
 * Edits one metadata field and its options on a copy of the fields, so cancelling leaves the mandate form as it was.
 * Applying hands the field back; the mandate is saved with the form as before.
 */
const MetadataFieldDialog: FC<MetadataFieldDialogProps> = ({ fields, index, isNew, onApply, onRemove, onClose }) => {
  const { t, i18n } = useTranslation();
  const [activeLanguage, setActiveLanguage] = useState<Language>(
    isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : Language.EN,
  );
  const methods = useForm<MetadataFieldFormValues>({ mode: "all", defaultValues: { metadataFields: fields } });
  const {
    clearErrors,
    control,
    formState: { errors },
    getValues,
    handleSubmit,
    setValue,
  } = methods;
  const field = fields[index];

  const typeValues: FormSelectValue[] = Object.entries(metadataFieldTypeLabels).map(([value, label], key) => ({
    key,
    value,
    name: t(label),
  }));

  const validateType = (type: number | string) =>
    type !== MetadataFieldType.Selection ||
    (getValues(`metadataFields.${index}.options`)?.length ?? 0) > 0 ||
    "metadataOptionsRequired";

  // FormSelect shows no message of a nested field, so the reason goes in as helper text, which MUI links to the select.
  const typeHelperText =
    errors.metadataFields?.[index]?.type?.message === "metadataOptionsRequired"
      ? t("metadataOptionsRequired")
      : undefined;

  // A new type starts the options over: a selection with one empty option, any other type with none. Runs before the
  // type is validated, which counts the options.
  const onTypeChange = () => {
    const optionsName = `metadataFields.${index}.options` as const;
    const isSelection = getValues(`metadataFields.${index}.type`) === MetadataFieldType.Selection;
    setValue(optionsName, isSelection ? [newMetadataFieldOption()] : []);
    clearErrors(optionsName);
  };

  // The other fields keep their titles while the dialog is open, so they need no check of their own.
  const validateTitle = (title: LocalizedText) => {
    const others = getValues("metadataFields");
    const conflict = findTitleConflict(
      others.map((other, otherIndex) => (otherIndex === index ? title : other.title)),
      index,
    );
    return conflict
      ? t("metadataFieldTitleConflict", {
          language: conflict.language.toUpperCase(),
          id: others[conflict.otherIndex].id,
        })
      : true;
  };

  const apply = handleSubmit(({ metadataFields }) => onApply(metadataFields[index]));

  return (
    <FormProvider {...methods}>
      <Dialog
        open
        onClose={onClose}
        maxWidth="md"
        fullWidth
        aria-labelledby="metadataFieldDialog-title"
        data-cy="metadataFieldDialog">
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
          <DialogTitle id="metadataFieldDialog-title">
            {field.id ? t("metadataFieldWithId", { id: field.id }) : t("newMetadataField")}
          </DialogTitle>
          <FormLanguageTabs
            language={activeLanguage}
            onLanguageChange={setActiveLanguage}
            dataCy={"metadataFieldDialog-language"}
            label={"metadataFieldsInputLanguage"}
          />
        </Stack>
        {/* The labels of the first inputs sit above their border, where the scrolling content would cut them off. */}
        <DialogContent sx={{ paddingTop: 1 }}>
          <Stack>
            <FormContainer>
              <FormInput
                fieldName={`metadataFields.${index}.id`}
                label={"id"}
                required={true}
                value={field.id}
                validate={id => validateMetadataId(id, getValues("metadataFields"))}
                deps={idFieldNames("metadataFields", fields.length).filter((_, other) => other !== index)}
              />
              <FormLocalizedInput
                fieldName={`metadataFields.${index}.title`}
                label={"metadataFieldTitle"}
                value={field.title}
                activeLanguage={activeLanguage}
                requireAtLeastOne={true}
                validate={validateTitle}
                helperText={t("atLeastOneLanguageRequired")}
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
                  validate={validateType}
                  helperText={typeHelperText}
                  onUpdate={onTypeChange}
                />
              </FormColumnHalfWidth>
              <FormColumnHalfWidth direction="row" sx={{ alignItems: "center" }}>
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
              </FormColumnHalfWidth>
            </FormContainer>
            <MandateMetadataOptions fieldIndex={index} activeLanguage={activeLanguage} />
          </Stack>
        </DialogContent>
        <DialogActions>
          {!isNew && (
            <Button variant="outlined" color="error" label={"delete"} onClick={onRemove} sx={{ marginRight: "auto" }} />
          )}
          <Button label={"cancel"} onClick={onClose} />
          <Button
            variant="contained"
            label={"apply"}
            disabled={Object.keys(errors).length > 0}
            onClick={() => apply()}
          />
        </DialogActions>
      </Dialog>
    </FormProvider>
  );
};

export default MetadataFieldDialog;
