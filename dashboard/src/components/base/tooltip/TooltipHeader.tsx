import React from "react";
import { QuestionCircleOutlined } from "@ant-design/icons";
import SharpTooltip from "./Tooltip";

interface TooltipHeaderProps {
  label: React.ReactNode;
  tooltip: string;
}

/**
 * A table column-header label followed by a discoverable info icon that
 * reveals a tooltip on hover. Use as the `title` of an Ant Design column.
 */
const TooltipHeader: React.FC<TooltipHeaderProps> = ({ label, tooltip }) => {
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
      {label}
      <SharpTooltip title={tooltip}>
        <QuestionCircleOutlined
          style={{
            fontSize: 12,
            color: "var(--ant-color-text-secondary)",
            cursor: "help",
          }}
        />
      </SharpTooltip>
    </span>
  );
};

export default TooltipHeader;
