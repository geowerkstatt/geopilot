import { FC, Fragment, useEffect, useRef } from "react";
import { useFieldArray, useFormContext, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Box, Divider, FormHelperText, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataFieldType } from "../../../api/generated";
import { Language, TranslationKey } from "../../../appInterfaces.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import { FormContainer, FormInput, FormLocalizedInput, FormSelect } from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";

interface MandateMetadataFieldsProps {
  mandate?: MandateFormValues;
  activeLanguage: Language;
}

interface SelectionOptionsProps {
  fieldIndex: number;
  activeLanguage: Language;
}

// The rule of the server. An id that starts with a digit would read as an array index in the delivery form.
const metadataIdPattern = /^[a-z][a-z0-9_]{0,63}$/;

const typeLabels: Record<MetadataFieldType, TranslationKey> = {
  [MetadataFieldType.Text]: "metadataFieldTypeText",
  [MetadataFieldType.TextList]: "metadataFieldTypeTextList",
  [MetadataFieldType.Selection]: "metadataFieldTypeSelection",
  [MetadataFieldType.Integer]: "metadataFieldTypeInteger",
  [MetadataFieldType.Decimal]: "metadataFieldTypeDecimal",
  [MetadataFieldType.Date]: "metadataFieldTypeDate",
};

/** Checks an id against the pattern and against its siblings, which include the entry that holds the id. */
const validateId = (id: string, siblings: { id: string }[] | null | undefined): string | boolean => {
  if (!metadataIdPattern.test(id)) {
    return "metadataIdInvalid";
  }
  return (siblings ?? []).filter(sibling => sibling.id === id).length < 2 || "metadataIdDuplicate";
};

const idFieldNames = (listName: string, count: number) =>
  Array.from({ length: count }, (_, index) => `${listName}.${index}.id`);

/**
 * Keeps a fixed duplicate id from leaving its error behind, which would keep saving disabled. Each id input names the
 * other ids of its list as deps, so changing one checks them all. A removal moves the entries after it up, and they
 * take their new names only when they render, so the ids are checked again after that render.
 */
const useIdRecheck = (listName: string, count: number, remove: (index: number) => void) => {
  const { trigger } = useFormContext();
  const removed = useRef(false);

  useEffect(() => {
    if (removed.current) {
      removed.current = false;
      trigger(idFieldNames(listName, count));
    }
  }, [count, listName, trigger]);

  return {
    otherIds: (index: number) => idFieldNames(listName, count).filter((_, other) => other !== index),
    removeEntry: (index: number) => {
      removed.current = true;
      remove(index);
    },
  };
};

/** The options of a field, listed while the field is a selection. */
const SelectionOptions: FC<SelectionOptionsProps> = ({ fieldIndex, activeLanguage }) => {
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
            validate={id => validateId(id, getValues(optionsName))}
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
      {fields.length === 0 && (
        <FormHelperText error data-cy={`${optionsName}-formHelperText`}>
          {t("metadataOptionsRequired")}
        </FormHelperText>
      )}
      <Box>
        <Button startIcon={<AddIcon />} label={"addMetadataFieldOption"} onClick={addOption} />
      </Box>
    </Stack>
  );
};

/**
 * The additional fields a deliverer fills in for the mandate, in the order the delivery form shows them. A new field
 * goes to the end. Whether the titles differ is checked by the server alone; its message shows when saving fails.
 */
const MandateMetadataFields: FC<MandateMetadataFieldsProps> = ({ mandate, activeLanguage }) => {
  const { t } = useTranslation();
  const { getValues, setValue } = useFormContext<MandateFormValues>();
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
  const evaluationValues: FormSelectValue[] = [
    { key: 0, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
    { key: 1, value: FieldEvaluationType.Required, name: t("fieldRequired") },
  ];

  const addField = () =>
    append({ id: "", title: {}, type: MetadataFieldType.Text, evaluation: FieldEvaluationType.Optional, options: [] });

  // A selection needs an option. The select only turns red, the list of options below it shows the message.
  const validateType = (index: number, type: number | string) =>
    type !== MetadataFieldType.Selection ||
    (getValues(`metadataFields.${index}.options`)?.length ?? 0) > 0 ||
    "metadataOptionsRequired";

  return (
    <>
      {fields.map((field, index) => (
        <Fragment key={field.key}>
          <FormContainer>
            <FormInput
              fieldName={`metadataFields.${index}.id`}
              label={"id"}
              required={true}
              value={field.id}
              validate={id => validateId(id, getValues("metadataFields"))}
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
            <FormSelect
              fieldName={`metadataFields.${index}.type`}
              label={"metadataFieldType"}
              required={true}
              selected={field.type}
              values={typeValues}
              validate={type => validateType(index, type)}
            />
            <FormSelect
              fieldName={`metadataFields.${index}.evaluation`}
              label={"metadataFieldEvaluation"}
              required={true}
              selected={field.evaluation}
              values={evaluationValues}
            />
            <IconButton
              icon={<DeleteOutlinedIcon />}
              label={"removeMetadataField"}
              onClick={() => removeEntry(index)}
              sx={{ alignSelf: "center" }}
            />
          </FormContainer>
          <SelectionOptions fieldIndex={index} activeLanguage={activeLanguage} />
          <Divider />
        </Fragment>
      ))}
      <Box>
        <Button startIcon={<AddIcon />} label={"addMetadataField"} onClick={addField} />
      </Box>
    </>
  );
};

export default MandateMetadataFields;
