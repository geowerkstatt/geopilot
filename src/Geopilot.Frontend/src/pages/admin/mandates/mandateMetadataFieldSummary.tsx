import { FC } from "react";
import { useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Box, Chip, Stack, Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { FieldEvaluationType, MetadataFieldType } from "../../../api/generated";
import { Language } from "../../../appInterfaces.ts";
import { NEOGEO_FONTWEIGHT_BOLD, px2rem } from "../../../appTheme.ts";
import { metadataFieldTypeLabels } from "./metadataFieldTypeLabels.ts";

interface MandateMetadataFieldSummaryProps {
  index: number;
  activeLanguage: Language;
  hasError: boolean;
}

// Every row is a grid of its own, so no column takes its width from its content: only then do the columns line up
// from row to row.
const positionColumn = px2rem(24);
const requiredColumn = px2rem(112);
const iconColumn = px2rem(24);

/**
 * The row of a metadata field: its position, its title in the language of the tabs, its id, its type and whether it
 * is required. It reads the form, so it follows the inputs while the field is open.
 */
const MandateMetadataFieldSummary: FC<MandateMetadataFieldSummaryProps> = ({ index, activeLanguage, hasError }) => {
  const { t } = useTranslation();
  const name = `metadataFields.${index}` as const;
  const field = useWatch<MandateFormValues, typeof name>({ name });
  // A field added just now has no entry for a language until its title inputs have registered.
  const title = field.title[activeLanguage]?.trim();

  // The row sits inside the button that opens the field, so it is built from spans. From the md breakpoint on it has
  // the two equal halves of the form rows around it, so the type starts where their right column starts. Below, the
  // form rows stack, and the row keeps the title and what fits beside it. The spaces between the cells are for
  // assistive technology, which reads the row as one text; a grid does not render them.
  return (
    <Box
      component="span"
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "minmax(0, 1fr) auto", md: "minmax(0, 1fr) minmax(0, 1fr)" },
        columnGap: 2,
        alignItems: "center",
        flexGrow: 1,
        minWidth: 0,
      }}>
      <Box
        component="span"
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: `${positionColumn} minmax(0, 1fr)`,
            md: `${positionColumn} minmax(0, 3fr) minmax(0, 2fr)`,
          },
          columnGap: 2,
          alignItems: "center",
          minWidth: 0,
        }}>
        <Typography component="span" variant="body2" sx={{ color: "text.secondary" }}>
          {index + 1}
        </Typography>{" "}
        <Stack component="span" direction="row" spacing={1} sx={{ alignItems: "center", minWidth: 0 }}>
          {hasError && <ErrorOutlineIcon color="error" fontSize="small" titleAccess={t("metadataFieldHasErrors")} />}{" "}
          <Typography
            component="span"
            noWrap
            sx={{
              color: hasError ? "error.main" : title ? "text.primary" : "text.secondary",
              fontWeight: title ? NEOGEO_FONTWEIGHT_BOLD : undefined,
            }}>
            {title || t("metadataFieldUntitled")}
          </Typography>
        </Stack>{" "}
        <Typography
          component="span"
          variant="body2"
          noWrap
          sx={{ color: "text.secondary", fontFamily: "monospace", display: { xs: "none", md: "block" } }}>
          {field.id}
        </Typography>
      </Box>{" "}
      <Box
        component="span"
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: iconColumn,
            sm: `${requiredColumn} ${iconColumn}`,
            md: `minmax(0, 1fr) ${requiredColumn} ${iconColumn}`,
          },
          columnGap: 2,
          alignItems: "center",
          minWidth: 0,
        }}>
        <Typography
          component="span"
          variant="body2"
          noWrap
          sx={{ color: "text.secondary", display: { xs: "none", md: "block" } }}>
          {t(metadataFieldTypeLabels[field.type])}
          {field.type === MetadataFieldType.Selection && ` · ${field.options?.length ?? 0}`}
        </Typography>{" "}
        <Box component="span" sx={{ display: { xs: "none", sm: "block" } }}>
          {field.evaluation === FieldEvaluationType.Required && (
            <Chip component="span" size="small" label={t("metadataFieldRequired")} />
          )}
        </Box>
        {/* The arrow is part of the row and not the expandIcon of the summary, which would take its width from both
            halves and move the right one off the column of the form. */}
        <ExpandMoreIcon
          sx={theme => ({
            color: theme.palette.primary.main,
            transition: theme.transitions.create("transform", { duration: theme.transitions.duration.shortest }),
            ".MuiAccordionSummary-root.Mui-expanded &": { transform: "rotate(180deg)" },
          })}
        />
      </Box>
    </Box>
  );
};

export default MandateMetadataFieldSummary;
