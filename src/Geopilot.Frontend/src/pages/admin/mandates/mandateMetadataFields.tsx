import { FC, useEffect, useState } from "react";
import { useFieldArray, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Stack, TableCell, TableRow, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataField } from "../../../api/generated";
import { px2rem } from "../../../appTheme.ts";
import { Button, IconButton } from "../../../components/buttons.tsx";
import { DetailTable } from "../../../components/detailTable.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import MetadataFieldDialog from "./metadataFieldDialog.tsx";
import { metadataFieldTypeLabels, newMetadataField } from "./metadataFieldModel.ts";

interface MandateMetadataFieldsProps {
  mandate?: MandateFormValues;
}

/**
 * The additional fields a deliverer fills in for the mandate, in the order the delivery form shows them, each edited
 * in a dialog. A new field goes to the end. Whether the titles differ is checked by the server alone; its message
 * shows when saving fails.
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
        <DetailTable
          dataCy="metadataFields-table"
          columns={[
            { label: t("id"), width: "25%" },
            { label: t("metadataFieldTitle") },
            { label: t("metadataFieldType"), width: "25%" },
            { label: t("metadataFieldRequired"), width: "15%" },
            { label: "", width: px2rem(56) },
          ]}>
          {fields.map((field, index) => (
            // The row opens the dialog for the mouse; the pencil stays the control for the keyboard.
            <TableRow key={field.key} onClick={() => setEditedIndex(index)} sx={{ cursor: "pointer" }}>
              <TableCell>{field.id}</TableCell>
              <TableCell>{localized(field.title)}</TableCell>
              <TableCell>{t(metadataFieldTypeLabels[field.type])}</TableCell>
              <TableCell>{field.evaluation === FieldEvaluationType.Required ? t("yes") : t("no")}</TableCell>
              <TableCell sx={{ paddingY: 0 }}>
                <IconButton
                  icon={<EditOutlinedIcon />}
                  label={"editMetadataField"}
                  labelValues={{ position: index + 1 }}
                  size="small"
                  onClick={() => setEditedIndex(index)}
                />
              </TableCell>
            </TableRow>
          ))}
        </DetailTable>
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
