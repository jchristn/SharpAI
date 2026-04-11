import { LocalModel } from "#/lib/reducer/types";
import { formatSize, formatDate } from "#/utils/utils";
import React from "react";
import SharpButton from "#/components/base/button/Button";
import { DeleteOutlined } from "@ant-design/icons";
import TooltipHeader from "#/components/base/tooltip/TooltipHeader";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import { tooltips } from "#/constants/tooltips";

// Column configuration factory
export const createColumnConfig = (
  localModels: LocalModel[] | undefined,
  onDelete?: (model: LocalModel) => void
) => [
  {
    title: <TooltipHeader label="Model Name" tooltip={tooltips.models.name} />,
    dataIndex: "name",
    key: "name",
    width: 200,
  },
  {
    title: <TooltipHeader label="Model ID" tooltip={tooltips.models.model} />,
    dataIndex: "model",
    key: "model",
    width: 250,
    ellipsis: true,
  },
  {
    title: <TooltipHeader label="Family" tooltip={tooltips.models.family} />,
    dataIndex: ["details", "family"],
    key: "family",
    width: 150,
    filters:
      localModels && localModels?.length > 0
        ? Array.from(
            new Set(localModels.map((model) => model.details.family))
          ).map((family) => ({ text: family, value: family }))
        : [],
  },
  {
    title: <TooltipHeader label="Format" tooltip={tooltips.models.format} />,
    dataIndex: ["details", "format"],
    key: "format",
    width: 100,
    filters:
      localModels && localModels?.length > 0
        ? Array.from(
            new Set(localModels.map((model) => model.details.format))
          ).map((format) => ({ text: format, value: format }))
        : [],
  },
  {
    title: <TooltipHeader label="Size" tooltip={tooltips.models.size} />,
    dataIndex: "size",
    key: "size",
    width: 120,
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
    width: 130,
  },
  {
    title: <TooltipHeader label="Modified" tooltip={tooltips.models.modified} />,
    dataIndex: "modified_at",
    key: "modified_at",
    width: 180,
    render: formatDate,
  },
  {
    title: <TooltipHeader label="Actions" tooltip={tooltips.models.actions} />,
    key: "actions",
    width: 100,
    render: (_: any, record: LocalModel) => (
      <SharpTooltip title="Delete this model">
        <SharpButton
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          onClick={() => onDelete?.(record)}
        />
      </SharpTooltip>
    ),
  },
];
