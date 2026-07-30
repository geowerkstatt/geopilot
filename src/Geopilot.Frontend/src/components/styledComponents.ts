import { Stack, styled } from "@mui/material";

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

export const pageContentPadding = "40px";

export const PageContent = styled(Stack)({
  height: "100%",
  padding: pageContentPadding,
  flex: "1",
  alignItems: "center",
});

export const CenteredContent = styled(Stack, {
  shouldForwardProp: prop => prop !== "maxWidth",
})<{ maxWidth?: string }>(({ maxWidth = "1200px" }) => ({
  width: "100%",
  maxWidth,
  height: "100%",
  padding: pageContentPadding,
  alignSelf: "center",
  flex: "1",
  [`@media (min-width: ${maxWidth})`]: {
    paddingLeft: `calc(${pageContentPadding} + ((100vw - 100%) / 2))`, // prevent the scrollbar from shifting the content
  },
}));

export const GeopilotBox = styled(Stack)(({ theme }) => ({
  backgroundColor: theme.palette.background.content,
  border: `1px solid ${theme.palette.primary.light}`,
  borderRadius: theme.radius.default,
  padding: theme.spacing(2),
}));
