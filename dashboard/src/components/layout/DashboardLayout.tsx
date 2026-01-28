"use client";

import React, { useState } from "react";
import { Layout } from "antd";
import Sidebar from "#/components/base/sidebar";
import { ThemeEnum } from "#/types/types";
import SharpFlex from "#/components/base/flex/Flex";
import ThemeModeSwitch from "#/components/theme-mode-switch/ThemeModeSwitch";
import SharpTooltip from "#/components/base/tooltip/Tooltip";

import styles from "./dashboardLayout.module.scss";
import { useAppContext } from "#/hooks/appHooks";
import withConnectivityValidation from "#/hoc/hoc";
import { SwitcherOutlined } from "@ant-design/icons";
import SharpButton from "../base/button/Button";
import Link from "next/link";

const { Content, Header } = Layout;

const DashboardLayout = ({ children }: { children: React.ReactNode }) => {
  const { theme } = useAppContext();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sidebar collapsed={collapsed} onCollapse={setCollapsed} />
      <Layout>
        <Header className={styles.header}>
          <SharpFlex
            align="center"
            justify="space-between"
            data-testid="user-section"
          >
            <SharpTooltip
              title={`Switch to ${
                theme === ThemeEnum.DARK ? "Light" : "Dark"
              } mode`}
            >
              <ThemeModeSwitch />
            </SharpTooltip>
          </SharpFlex>
          <Link href="/">
            <SharpButton type="link" icon={<SwitcherOutlined />}>
              Change SharpAPI Instance
            </SharpButton>
          </Link>
        </Header>
        {children}
      </Layout>
    </Layout>
  );
};

export default withConnectivityValidation(DashboardLayout);
