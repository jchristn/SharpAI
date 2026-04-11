import { StyleProvider } from "@ant-design/cssinjs";
import { AppContext } from "../hooks/appHooks";
import React, { useState, useEffect } from "react";
import { ThemeEnum } from "../types/types";
import { darkTheme, primaryTheme } from "../theme/theme";
import { ConfigProvider } from "antd";
import { localStorageKeys } from "../constants/constant";
import StoreProvider from "#/lib/store/StoreProvider";
import { Toaster } from "react-hot-toast";
import PullProgressProvider from "./PullProgressProvider";

const getThemeFromLocalStorage = () => {
  let theme;
  if (typeof localStorage !== "undefined") {
    theme = localStorage.getItem(localStorageKeys.theme);
  }
  return theme ? (theme as ThemeEnum) : ThemeEnum.LIGHT;
};

const AppProviders = ({ children }: { children: React.ReactNode }) => {
  const [theme, setTheme] = useState<ThemeEnum>(ThemeEnum.LIGHT);

  useEffect(() => {
    const savedTheme = getThemeFromLocalStorage();
    setTheme(savedTheme);
  }, []);

  const handleThemeChange = (theme: ThemeEnum) => {
    localStorage.setItem(localStorageKeys.theme, theme);
    setTheme(theme);
  };
  const handleBackgroundTask = async (task: () => Promise<void>) => {
    await task();
  };

  return (
    <StoreProvider>
      <AppContext.Provider
        value={{
          theme,
          setTheme: handleThemeChange,
          handleBackgroundTask,
        }}
      >
        <PullProgressProvider>
          <StyleProvider hashPriority="high">
            <ConfigProvider
              theme={theme === ThemeEnum.LIGHT ? primaryTheme : darkTheme}
            >
              {children}
              <Toaster />
            </ConfigProvider>
          </StyleProvider>
        </PullProgressProvider>
      </AppContext.Provider>
    </StoreProvider>
  );
};

export default AppProviders;
