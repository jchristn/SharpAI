import React from "react";
import { Layout, Menu, Button } from "antd";
import {
  AppstoreOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UnorderedListOutlined,
  MessageOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import { Link, useLocation } from "react-router-dom";
import "../../../assets/css/globals.scss";
import SharpFlex from "../flex/Flex";
import styles from "./sidebar.module.scss";
import SharpLogo from "#/components/logo/SharpLogo";
import SharpTooltip from "../tooltip/Tooltip";
import { useGetSettingsQuery } from "#/lib/reducer/apiSlice";
import pkg from "../../../../package.json";

const { Sider } = Layout;

interface SidebarProps {
  collapsed?: boolean;
  onCollapse?: (collapsed: boolean) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ collapsed = false, onCollapse }) => {
  const { pathname } = useLocation();
  const { data: settings } = useGetSettingsQuery(undefined, {
    refetchOnMountOrArgChange: true,
  });

  const menuItems = [
    {
      key: "models",
      icon: <AppstoreOutlined />,
      label: <Link to="/dashboard">Models</Link>,
    },
    {
      key: "embeddings",
      icon: <UnorderedListOutlined />,
      label: <Link to="/dashboard/embeddings">Embeddings</Link>,
    },
    {
      key: "completions",
      icon: <MessageOutlined />,
      label: <Link to="/dashboard/completions">Completions</Link>,
    },
    {
      key: "chat-completion",
      icon: <MessageOutlined />,
      label: <Link to="/dashboard/chat-completion">Chat Completion</Link>,
    },
    {
      key: "configuration",
      icon: <SettingOutlined />,
      label: <Link to="/dashboard/configuration">Configuration</Link>,
    },
  ];

  const getSelectedKey = () => {
    if (pathname === "/dashboard" || pathname === "/dashboard/") return ["models"];
    if (pathname.startsWith("/dashboard/embeddings")) return ["embeddings"];
    if (pathname.startsWith("/dashboard/completions")) return ["completions"];
    if (pathname.startsWith("/dashboard/chat-completion"))
      return ["chat-completion"];
    if (pathname.startsWith("/dashboard/configuration"))
      return ["configuration"];
    return ["home"];
  };

  return (
    <Sider
      theme="dark"
      width={220}
      trigger={null}
      collapsible
      collapsed={collapsed}
      collapsedWidth={60}
      className={styles.sidebarContainer}
    >
      <SharpFlex
        justify={collapsed ? "center" : "start"}
        gap={8}
        align="center"
        className={`${styles.logoContainer} ${collapsed ? styles.logoCollapsed : ""}`}
      >
        <SharpLogo onlyIcon={collapsed} textColor="#ffffff" />
      </SharpFlex>
      <div className={styles.menuContainer}>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={getSelectedKey()}
          items={menuItems}
        />
      </div>
      <div className={styles.bottomContainer}>
        <div className={styles.collapseContainer}>
          <Button
            type="text"
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            onClick={() => onCollapse?.(!collapsed)}
            title={collapsed ? "Expand Sidebar" : "Collapse Sidebar"}
            className={styles.collapseButton}
            block
          />
        </div>
        {!collapsed && (
          <div className={styles.versionContainer}>
            <SharpTooltip
              title={
                <>
                  <div>Dashboard v{pkg.version}</div>
                  <div>Server v{settings?.SoftwareVersion ?? "unknown"}</div>
                </>
              }
            >
              <span className={styles.versionText}>v{pkg.version}</span>
            </SharpTooltip>
          </div>
        )}
      </div>
    </Sider>
  );
};

export default Sidebar;
