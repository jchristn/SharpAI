import { LocalModel } from "#/lib/reducer/types";
import { formatSize } from "#/utils/utils";
import React from "react";
import SharpButton from "#/components/base/button/Button";
import SharpDropdown from "#/components/base/dropdown/Dropdown";
import {
  DeleteOutlined,
  InfoCircleOutlined,
  MoreOutlined,
  CodeOutlined,
  PlayCircleOutlined,
} from "@ant-design/icons";
import TooltipHeader from "#/components/base/tooltip/TooltipHeader";
import { tooltips } from "#/constants/tooltips";

interface ActionHandlers {
  onViewDetails: (model: LocalModel) => void;
  onViewJson: (model: LocalModel) => void;
  onLoad: (model: LocalModel) => void;
  onDelete: (model: LocalModel) => void;
  isModelLoaded?: (model: LocalModel) => boolean;
}

// Column configuration factory
export const createColumnConfig = (
  _localModels: LocalModel[] | undefined,
  handlers: ActionHandlers
) => [
  {
    title: <TooltipHeader label="Model Name" tooltip={tooltips.models.name} />,
    dataIndex: "name",
    key: "name",
    width: 240,
  },
  {
    title: <TooltipHeader label="Model ID" tooltip={tooltips.models.model} />,
    dataIndex: "model",
    key: "model",
    width: 300,
    ellipsis: true,
  },
  {
    title: <TooltipHeader label="Size" tooltip={tooltips.models.size} />,
    dataIndex: "size",
    key: "size",
    width: 140,
    render: formatSize,
  },
  {
    title: (
      <TooltipHeader
        label="Quantization"
        tooltip={tooltips.models.quantization}
      />
    ),
    dataIndex: ["details", "quantization_level"],
    key: "quantization_level",
    width: 150,
  },
  {
    title: <TooltipHeader label="Actions" tooltip={tooltips.models.actions} />,
    key: "actions",
    width: 90,
    align: "center" as const,
    render: (_: any, record: LocalModel) => {
      const alreadyLoaded = handlers.isModelLoaded?.(record) ?? false;
      return (
      <SharpDropdown
        trigger={["click"]}
        placement="bottomRight"
        getPopupContainer={() => document.body}
        menu={{
          items: [
            {
              key: "load",
              icon: <PlayCircleOutlined />,
              label: "Load Model",
              disabled: alreadyLoaded,
              onClick: () => handlers.onLoad(record),
            },
            {
              key: "details",
              icon: <InfoCircleOutlined />,
              label: "View Details",
              onClick: () => handlers.onViewDetails(record),
            },
            {
              key: "json",
              icon: <CodeOutlined />,
              label: "View JSON",
              onClick: () => handlers.onViewJson(record),
            },
            { type: "divider" as const },
            {
              key: "delete",
              icon: <DeleteOutlined />,
              label: "Delete",
              danger: true,
              onClick: () => handlers.onDelete(record),
            },
          ],
        }}
      >
        <SharpButton
          type="text"
          size="small"
          icon={<MoreOutlined style={{ fontSize: 18 }} />}
          onClick={(e) => e.preventDefault()}
        />
      </SharpDropdown>
      );
    },
  },
];
