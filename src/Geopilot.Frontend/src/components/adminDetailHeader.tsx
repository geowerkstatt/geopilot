import { FC } from "react";
import { useTranslation } from "react-i18next";
import { ChevronLeft } from "@mui/icons-material";
import { Stack, Typography } from "@mui/material";
import { TranslationKey } from "../appInterfaces";
import { Button } from "./buttons.tsx";

interface AdminDetailHeaderProps {
  backLabel: TranslationKey;
  onBack: () => void;
  /** The id of the entry, shown once it has one. */
  id?: number;
}

/** The head of an admin detail page: the way back to its list and the id of the entry. */
export const AdminDetailHeader: FC<AdminDetailHeaderProps> = ({ backLabel, onBack, id }) => {
  const { t } = useTranslation();

  return (
    <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "space-between" }}>
      <Button variant="text" startIcon={<ChevronLeft />} onClick={onBack} label={backLabel} />
      {!!id && <Typography variant={"body2"}>{t("id") + ": " + id}</Typography>}
    </Stack>
  );
};
