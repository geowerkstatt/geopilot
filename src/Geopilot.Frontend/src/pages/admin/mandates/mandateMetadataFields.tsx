import { FC, useEffect, useState } from "react";
import { useFieldArray, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { List, ListItem, ListItemButton, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataField, MetadataFieldType } from "../../../api/generated";
import { Button } from "../../../components/buttons.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import MetadataFieldDialog from "./metadataFieldDialog.tsx";
import { metadataFieldTypeLabels, newMetadataField } from "./metadataFieldModel.ts";

interface MandateMetadataFieldsProps {
  mandate?: MandateFormValues;
}

/**
 * The additional fields a deliverer fills in for the mandate, in the order the delivery form shows them. Each field is
 * a button that opens its dialog, two side by side in the half widths of the form columns. A new field goes to the end.
 */
const MandateMetadataFields: FC<MandateMetadataFieldsProps> = ({ mandate }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const { getValues, setValue } = useFormContext<MandateFormValues>();
  // A field has an id of its own, which the key that tracks the entries would otherwise replace.
  const { fields, append, update, remove } = useFieldArray<MandateFormValues, "metadataFields", "key">({
    name: "metadataFields",
    keyName: "key",
  });
  // The index of the field in the dialog; one past the last field for a new one.
  const [editedIndex, setEditedIndex] = useState<number>();

  // The form has no default values, so the list is filled here. Unlike replace or append, setValue does not mark the
  // form as changed, so reset and save stay disabled until the admin edits something.
  useEffect(() => {
    setValue("metadataFields", mandate?.metadataFields ?? []);
  }, [mandate, setValue]);

  const typeText = (field: MetadataField) =>
    field.type === MetadataFieldType.Selection
      ? `${t(metadataFieldTypeLabels[field.type])}, ${t("metadataFieldOptionCount", { count: field.options?.length ?? 0 })}`
      : t(metadataFieldTypeLabels[field.type]);

  const applyField = (index: number, field: MetadataField) => {
    if (index < fields.length) {
      update(index, field);
    } else {
      append(field);
    }
    setEditedIndex(undefined);
  };

  const removeField = (index: number) => {
    remove(index);
    setEditedIndex(undefined);
  };

  return (
    <>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", marginTop: 1 }}>
        <Typography variant={"h4"} sx={{ margin: 0 }}>
          {t("metadataFields")}
        </Typography>
        <Button startIcon={<AddIcon />} label={"addMetadataField"} onClick={() => setEditedIndex(fields.length)} />
      </Stack>
      {fields.length > 0 ? (
        <List
          disablePadding
          data-cy="metadataFields-list"
          sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
          {fields.map((field, index) => (
            <ListItem key={field.key} disablePadding>
              <ListItemButton
                aria-haspopup="dialog"
                onClick={() => setEditedIndex(index)}
                sx={theme => ({
                  gap: 2,
                  border: `1px solid ${theme.palette.primary.light}`,
                  borderRadius: theme.radius.default,
                })}>
                <Typography noWrap title={localized(field.title)} sx={{ minWidth: 0 }}>
                  {localized(field.title)}
                </Typography>
                <Typography variant="body2" noWrap sx={{ flexShrink: 0, color: "text.secondary" }}>
                  {typeText(field)}
                </Typography>
                <Stack direction="row" sx={{ marginLeft: "auto", flexShrink: 0, alignItems: "center" }}>
                  {field.evaluation === FieldEvaluationType.Required && (
                    <Typography variant="body2" noWrap sx={{ color: "text.secondary" }}>
                      {t("metadataFieldRequired")}
                    </Typography>
                  )}
                  <EditOutlinedIcon color="primary" />
                </Stack>
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      ) : (
        <Typography data-cy="metadataFieldsNone">{t("metadataFieldsNone")}</Typography>
      )}
      {editedIndex !== undefined && (
        <MetadataFieldDialog
          fields={
            editedIndex < fields.length
              ? getValues("metadataFields")
              : [...getValues("metadataFields"), newMetadataField()]
          }
          index={editedIndex}
          isNew={editedIndex >= fields.length}
          onApply={field => applyField(editedIndex, field)}
          onRemove={() => removeField(editedIndex)}
          onClose={() => setEditedIndex(undefined)}
        />
      )}
    </>
  );
};

export default MandateMetadataFields;
