import { theme, ThemeConfig } from "antd";

export const ThemeColors = {
  primary: "#9333ea",
  primaryHover: "#7e22ce",
  primaryLight: "#a855f7",
  sidebarBg: "#2a1142",
  sidebarText: "#c4b5d4",
  sidebarActive: "#ffffff",
  bgLight: "#f8fafc",
  cardBg: "#ffffff",
  border: "#e2e8f0",
  textPrimary: "#1e293b",
  textSecondary: "#64748b",
  inputBorder: "#cbd5e1",
  white: "#ffffff",
  fontFamily:
    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
};

export const DarkThemeColors = {
  bgDark: "#050505",
  cardBg: "#101010",
  border: "#242424",
  textPrimary: "#f1f5f9",
  textSecondary: "#9ca3af",
  inputBg: "#101010",
  inputBorder: "#2e2e2e",
  sidebarBg: "#050505",
};

export const primaryTheme: ThemeConfig = {
  cssVar: true,
  algorithm: theme.defaultAlgorithm,
  token: {
    colorBgElevated: ThemeColors.cardBg,
    colorBgBase: ThemeColors.bgLight,
    colorPrimary: ThemeColors.primary,
    colorPrimaryHover: ThemeColors.primaryHover,
    colorPrimaryActive: ThemeColors.primaryHover,
    colorLink: ThemeColors.primary,
    colorLinkHover: ThemeColors.primaryHover,
    fontFamily: ThemeColors.fontFamily,
    colorBorder: ThemeColors.border,
    borderRadius: 8,
  },
  components: {
    Tabs: {
      cardBg: "#f1f5f9",
      titleFontSize: 13,
    },
    Typography: {
      fontWeightStrong: 500,
    },
    Layout: {
      fontFamily: ThemeColors.fontFamily,
      siderBg: ThemeColors.sidebarBg,
      lightSiderBg: ThemeColors.sidebarBg,
      lightTriggerBg: ThemeColors.sidebarBg,
    },
    Menu: {
      darkItemBg: ThemeColors.sidebarBg,
      darkItemColor: ThemeColors.sidebarText,
      darkItemSelectedBg: "rgba(168, 85, 247, 0.18)",
      darkItemSelectedColor: ThemeColors.sidebarActive,
      darkItemHoverBg: "rgba(255, 255, 255, 0.06)",
      darkItemHoverColor: ThemeColors.sidebarActive,
      itemSelectedBg: "rgba(147, 51, 234, 0.12)",
      itemSelectedColor: ThemeColors.primary,
    },
    Button: {
      borderRadius: 6,
      primaryColor: ThemeColors.white,
      primaryShadow: "none",
      defaultColor: ThemeColors.textPrimary,
      colorLink: ThemeColors.primary,
      colorLinkHover: ThemeColors.primaryHover,
    },
    Table: {
      headerBg: ThemeColors.bgLight,
      padding: 16,
      borderColor: ThemeColors.border,
      borderRadius: 8,
    },
    Collapse: {
      headerBg: ThemeColors.cardBg,
      borderRadiusLG: 8,
    },
    Input: {
      borderRadius: 6,
      borderRadiusLG: 6,
      borderRadiusXS: 6,
    },
    Select: {
      borderRadius: 6,
      borderRadiusLG: 6,
      borderRadiusXS: 6,
      optionSelectedColor: ThemeColors.white,
      optionSelectedBg: ThemeColors.primary,
    },
    Pagination: {
      fontFamily: ThemeColors.fontFamily,
    },
    Form: {
      labelColor: ThemeColors.textSecondary,
      colorBorder: "none",
      verticalLabelPadding: 0,
      itemMarginBottom: 10,
    },
    Card: {
      borderRadiusLG: 8,
    },
  },
};

export const darkTheme: ThemeConfig = {
  cssVar: true,
  algorithm: theme.darkAlgorithm,
  token: {
    colorBgElevated: DarkThemeColors.cardBg,
    colorBgBase: DarkThemeColors.bgDark,
    colorPrimary: ThemeColors.primary,
    colorPrimaryHover: ThemeColors.primaryLight,
    colorPrimaryActive: ThemeColors.primaryHover,
    colorLink: ThemeColors.primaryLight,
    colorLinkHover: ThemeColors.primaryLight,
    fontFamily: ThemeColors.fontFamily,
    colorBorder: DarkThemeColors.border,
    borderRadius: 8,
  },
  components: {
    Tabs: {
      cardBg: DarkThemeColors.cardBg,
      titleFontSize: 13,
    },
    Typography: {
      fontWeightStrong: 500,
    },
    Layout: {
      fontFamily: ThemeColors.fontFamily,
      siderBg: DarkThemeColors.sidebarBg,
      lightSiderBg: DarkThemeColors.sidebarBg,
      lightTriggerBg: DarkThemeColors.sidebarBg,
    },
    Menu: {
      darkItemBg: DarkThemeColors.sidebarBg,
      darkItemColor: ThemeColors.sidebarText,
      darkItemSelectedBg: "rgba(168, 85, 247, 0.18)",
      darkItemSelectedColor: ThemeColors.sidebarActive,
      darkItemHoverBg: "rgba(255, 255, 255, 0.06)",
      darkItemHoverColor: ThemeColors.sidebarActive,
      itemSelectedBg: "rgba(168, 85, 247, 0.15)",
      itemSelectedColor: ThemeColors.primaryLight,
    },
    Button: {
      borderRadius: 6,
      primaryColor: ThemeColors.white,
      primaryShadow: "none",
      defaultColor: DarkThemeColors.textPrimary,
      colorLink: ThemeColors.primaryLight,
      colorLinkHover: ThemeColors.primaryLight,
    },
    Table: {
      headerBg: "var(--ant-color-bg-container)",
      padding: 16,
      borderColor: "var(--ant-color-border)",
      borderRadius: 8,
    },
    Collapse: {
      headerBg: DarkThemeColors.cardBg,
      borderRadiusLG: 8,
    },
    Input: {
      borderRadius: 6,
      borderRadiusLG: 6,
      borderRadiusXS: 6,
    },
    Select: {
      borderRadius: 6,
      borderRadiusLG: 6,
      borderRadiusXS: 6,
      optionSelectedColor: ThemeColors.white,
      optionSelectedBg: ThemeColors.primary,
    },
    Pagination: {
      fontFamily: ThemeColors.fontFamily,
    },
    Form: {
      labelColor: "var(--ant-color-text-base)",
      colorBorder: "none",
      verticalLabelPadding: 0,
      itemMarginBottom: 10,
    },
    Card: {
      borderRadiusLG: 8,
    },
  },
};
