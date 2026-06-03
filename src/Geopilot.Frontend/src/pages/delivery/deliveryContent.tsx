import { Typography } from "@mui/material";
import { styled } from "@mui/system";
import { FC, PropsWithChildren } from "react";
import { useTranslation } from "react-i18next";
import { FlexBox, FlexRowEndBox, GeopilotBox } from "../../components/styledComponents";

interface DeliveryContentProps {
  title: string;
  subtitle?: string;
  buttons?: React.ReactNode;
}

const DeliveryContentBox = styled(FlexBox)({
  minHeight: "0",
  maxHeight: "100%",
  flex: 1,
});

const MainContentBox = styled(GeopilotBox)({
  overflow: "auto",
});

export const DeliveryContent: FC<PropsWithChildren<DeliveryContentProps>> = ({
  children,
  title,
  subtitle,
  buttons,
}) => {
  const { t } = useTranslation();

  return (
    <DeliveryContentBox>
      <MainContentBox>
        <Typography variant="h3" m={0} sx={{ display: { xs: "none", md: "block" } }}>
          {t(title)}
        </Typography>
        {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
        {children}
      </MainContentBox>
      <FlexRowEndBox>{buttons}</FlexRowEndBox>
    </DeliveryContentBox>
  );
};
