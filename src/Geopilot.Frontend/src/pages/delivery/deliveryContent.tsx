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
  flex: 1,
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
      <GeopilotBox>
        <Typography variant="h3" m={0}>
          {t(title)}
        </Typography>
        {subtitle && <Typography variant="body1">{t(subtitle)}</Typography>}
        {children}
      </GeopilotBox>
      <FlexRowEndBox>{buttons}</FlexRowEndBox>
    </DeliveryContentBox>
  );
};
