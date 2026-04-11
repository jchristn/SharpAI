import React, { useState } from "react";
import { Layout } from "antd";
import { Link, Outlet } from "react-router-dom";
import Sidebar from "#/components/base/sidebar";
import { ThemeEnum } from "#/types/types";
import SharpFlex from "#/components/base/flex/Flex";
import ThemeModeSwitch from "#/components/theme-mode-switch/ThemeModeSwitch";
import SharpTooltip from "#/components/base/tooltip/Tooltip";

import styles from "./dashboardLayout.module.scss";
import { useAppContext } from "#/hooks/appHooks";
import withConnectivityValidation from "#/hoc/hoc";
import { GithubOutlined, LogoutOutlined } from "@ant-design/icons";

const { Header } = Layout;

const DashboardLayout = () => {
  const { theme } = useAppContext();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sidebar collapsed={collapsed} onCollapse={setCollapsed} />
      <Layout>
        <Header className={styles.header}>
          <div />
          <SharpFlex
            align="center"
            gap={20}
            data-testid="user-section"
            style={{ height: 32 }}
          >
            <SharpTooltip title="View SharpAI on GitHub">
              <a
                href="https://github.com/jchristn/sharpai"
                target="_blank"
                rel="noopener noreferrer"
                className={styles.headerIconLink}
              >
                <GithubOutlined style={{ fontSize: 20 }} />
              </a>
            </SharpTooltip>
            <SharpTooltip
              title={`Switch to ${
                theme === ThemeEnum.DARK ? "Light" : "Dark"
              } mode`}
            >
              <div className={styles.headerIconLink}>
                <ThemeModeSwitch />
              </div>
            </SharpTooltip>
            <SharpTooltip title="Return to the landing page to switch SharpAI instances">
              <Link to="/" className={styles.headerLogoutLink}>
                <LogoutOutlined style={{ fontSize: 16 }} />
                <span>Logout</span>
              </Link>
            </SharpTooltip>
          </SharpFlex>
        </Header>
        <Outlet />
      </Layout>
    </Layout>
  );
};

export default withConnectivityValidation(DashboardLayout);
