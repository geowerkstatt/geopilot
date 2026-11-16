import { FC, useContext, useEffect, useRef, useState } from "react";
import { Controller, FieldValues, useFieldArray, useFormContext, useFormState } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Accordion, AccordionDetails, AccordionSummary, Box, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataFieldType } from "../../../api/generated";
import { isLanguage, Language } from "../../../appInterfaces.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import {
  FormCheckbox,
  FormColumnHalfWidth,
  FormContainer,
  FormInput,
  FormLanguageTabs,
  FormLocalizedInput,
  FormSelect,
  getFormFieldError,
  getFormFieldErrorMessage,
} from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import { ScrollMarginContext } from "../../../components/scrollMargin/ScrollMarginContext.tsx";
import { scrollIntoFullView } from "../../../components/visualizations/scrollIntoFullView.ts";
import MandateMetadataFieldSummary from "./mandateMetadataFieldSummary.tsx";
import MandateMetadataOptions from "./mandateMetadataOptions.tsx";
import { metadataFieldTypeLabels } from "./metadataFieldTypeLabels.ts";
import { useIdRecheck, validateMetadataId } from "./metadataIdValidation.ts";

interface MandateMetadataFieldsProps {
  mandate?: MandateFormValues;
}

/**
 * The additional fields a deliverer fills in for the mandate, in the order the delivery form shows them, with a
 * language choice of their own for the titles. Each field is a row that opens into its editor, one at a time, so the
 * list stays readable however many fields there are. A new field goes to the end. Whether the titles differ is
 * checked by the server alone; its message shows when saving fails.
 */
const MandateMetadataFields: FC<MandateMetadataFieldsProps> = ({ mandate }) => {
  const { t, i18n } = useTranslation();
  const [activeLanguage, setActiveLanguage] = useState<Language>(
    isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : Language.EN,
  );
  // At most one field is open, so the list never grows beyond its rows and one editor.
  const [openIndex, setOpenIndex] = useState<number>();
  const rows = useRef<(HTMLElement | null)[]>([]);
  const addButton = useRef<HTMLButtonElement>(null);
  const { scrollMarginTop, scrollMarginBottom } = useContext(ScrollMarginContext);
  const { clearErrors, control, getValues, setValue, trigger } = useFormContext<MandateFormValues>();
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

  const typeValues: FormSelectValue[] = Object.entries(metadataFieldTypeLabels).map(([value, label], key) => ({
    key,
    value,
    name: t(label),
  }));

  // The field that closes is checked on the way: its inputs leave the screen, so what is missing in them has to show
  // on its row.
  const openField = (index: number | undefined) => {
    if (openIndex !== undefined && openIndex !== index) {
      trigger(`metadataFields.${openIndex}`);
    }
    setOpenIndex(index);
  };

  // A new field is the one to fill in, so it opens.
  const addField = () => {
    append({ id: "", title: {}, type: MetadataFieldType.Text, evaluation: FieldEvaluationType.Optional, options: [] });
    openField(fields.length);
  };

  // The button that removes a field sits in its editor, so the field that goes is the open one. The button goes with
  // it, which would drop the focus out of the list. It moves to the row that takes the place of the field, else to
  // the row before it, else to the button that adds a field.
  const removeField = (index: number) => {
    (rows.current[index + 1] ?? rows.current[index - 1] ?? addButton.current)?.focus();
    removeEntry(index);
    setOpenIndex(undefined);
  };

  // A field that closes above the one that opens pulls it up, at worst out of sight, so the open field is brought
  // back into view once it has its full height.
  const revealField = (editor: HTMLElement) => {
    if (editor.parentElement) {
      scrollIntoFullView(editor.parentElement);
    }
  };

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
      {fields.length > 0 && (
        // No Stack: closed rows join into one list, which the gap of a Stack would pull apart.
        <Box>
          {fields.map((field, index) => (
            <Accordion
              key={field.key}
              expanded={openIndex === index}
              onChange={(_, expanded) => openField(expanded ? index : undefined)}
              // Every field repeats the same labels, so the group names which field they belong to.
              role="group"
              aria-label={t("metadataFieldGroup", { position: index + 1 })}
              slotProps={{ heading: { component: "h4" }, transition: { onEntered: revealField } }}
              sx={{ scrollMarginTop, scrollMarginBottom }}
              data-cy={`metadataField-${index}`}>
              <AccordionSummary
                ref={row => {
                  rows.current[index] = row;
                }}
                id={`metadataField-${field.key}-summary`}
                aria-controls={`metadataField-${field.key}-editor`}
                data-cy={`metadataField-${index}-summary`}
                // The row of the open field stays tinted, so it reads as the head of its editor.
                sx={{ "&:hover, &.Mui-expanded": { backgroundColor: "primary.states.hover" } }}>
                <MandateMetadataFieldSummary
                  index={index}
                  activeLanguage={activeLanguage}
                  // The inputs of a closed field stay in the form. An error among them would keep saving disabled
                  // with nothing on screen, so the row shows it.
                  hasError={getFormFieldError(`metadataFields.${index}`, errors)}
                />
              </AccordionSummary>
              <AccordionDetails sx={{ paddingTop: 2 }}>
                <Stack>
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
                        onClick={() => removeField(index)}
                      />
                    </FormColumnHalfWidth>
                  </FormContainer>
                  <MandateMetadataOptions fieldIndex={index} activeLanguage={activeLanguage} />
                </Stack>
              </AccordionDetails>
            </Accordion>
          ))}
        </Box>
      )}
      <Box>
        <Button ref={addButton} startIcon={<AddIcon />} label={"addMetadataField"} onClick={addField} />
      </Box>
    </>
  );
};

export default MandateMetadataFields;
