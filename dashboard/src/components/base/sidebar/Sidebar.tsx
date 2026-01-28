import React from "react";
import { Layout, Menu, Button } from "antd";
import {
  HomeOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UnorderedListOutlined,
  MessageOutlined,
} from "@ant-design/icons";
import { usePathname } from "next/navigation";
import "../../../assets/css/globals.scss";
import SharpFlex from "../flex/Flex";
import styles from "./sidebar.module.scss";
import SharpText from "../typograpghy/Text";
import Link from "next/link";
import SharpLogo from "#/components/logo/SharpLogo";

const { Sider } = Layout;

interface SidebarProps {
  collapsed?: boolean;
  onCollapse?: (collapsed: boolean) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ collapsed = false, onCollapse }) => {
  const pathname = usePathname();

  const menuItems = [
    {
      key: "models",
      icon: <HomeOutlined />,
      label: <Link href="/dashboard">Models</Link>,
    },
    {
      key: "embeddings",
      icon: <UnorderedListOutlined />,
      label: <Link href="/dashboard/embeddings">Embeddings</Link>,
    },
    {
      key: "completions",
      icon: <MessageOutlined />,
      label: <Link href="/dashboard/completions">Completions</Link>,
    },
    {
      key: "chat-completion",
      icon: <MessageOutlined />,
      label: <Link href="/dashboard/chat-completion">Chat Completion</Link>,
    },
  ];

  // Determine the selected key based on current pathname
  const getSelectedKey = () => {
    if (pathname === "/dashboard") return ["models"];
    if (pathname.startsWith("/dashboard/embeddings")) return ["embeddings"];
    if (pathname.startsWith("/dashboard/completions")) return ["completions"];
    if (pathname.startsWith("/dashboard/chat-completion"))
      return ["chat-completion"];
    return ["home"]; // default fallback
  };

  return (
    <Sider
      theme="light"
      width={200}
      trigger={null}
      collapsible
      collapsed={collapsed}
      collapsedWidth={60}
      className={styles.sidebarContainer}
    >
      <SharpFlex
        justify="center"
        gap={8}
        align="center"
        className={styles.logoContainer}
      >
        <SharpText weight={600} fontSize={20}>
          <SharpLogo onlyIcon={collapsed} />
        </SharpText>
      </SharpFlex>
      <SharpFlex
        justify="center"
        className=" mt"
        vertical
        align={collapsed ? "center" : "flex-end"}
        gap={10}
      >
        <Button
          type="text"
          icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
          onClick={() => onCollapse?.(!collapsed)}
          title={collapsed ? "Expand Sidebar" : "Collapse Sidebar"}
        />
      </SharpFlex>
      <Menu mode="inline" selectedKeys={getSelectedKey()} items={menuItems} />
    </Sider>
  );
};

export default Sidebar;
