import { Stack, styled } from "@mui/material";
import { themeSpacing } from "../appTheme";

export const FullPageStack = styled(Stack)({
  height: "100vh", // fallback for older browsers
  "@supports (height: 100dvh)": {
    height: "100dvh", // preferred for modern browsers
  },
});

export const ScrollableContent = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.base,
  paddingTop: "60px",
  flex: "1",
}));

export const pageContentPadding = {
  default: themeSpacing(5),
  xs: themeSpacing(2),
};

export const PageContent = styled(Stack)(({ theme }) => ({
  height: "100%",
  padding: pageContentPadding.default,
  flex: "1",
  alignItems: "center",
  [theme.breakpoints.down("sm")]: {
    padding: pageContentPadding.xs,
  },
}));

export const CenteredContent = styled(Stack, {
  shouldForwardProp: prop => prop !== "maxWidth",
})<{ maxWidth?: string }>(({ theme, maxWidth = "1200px" }) => ({
  width: "100%",
  maxWidth,
  height: "100%",
  padding: pageContentPadding.default,
  alignSelf: "center",
  flex: "1",
  [`@media (min-width: ${maxWidth})`]: {
    paddingLeft: `calc(${pageContentPadding.default} + ((100vw - 100%) / 2))`, // prevent the scrollbar from shifting the content
  },
  [theme.breakpoints.down("sm")]: {
    padding: pageContentPadding.xs,
  },
}));

export const GeopilotBox = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.content,
  border: `1px solid ${theme.palette.primary.light}`,
  borderRadius: theme.radius.default,
  padding: theme.spacing(2),
}));
