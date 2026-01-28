import { LocalModel } from "#/lib/reducer/types";
import { formatSize, formatDate } from "#/utils/utils";
import React from "react";
import SharpButton from "#/components/base/button/Button";
import { DeleteOutlined } from "@ant-design/icons";

// Column configuration factory
export const createColumnConfig = (
  localModels: LocalModel[] | undefined,
  onDelete?: (model: LocalModel) => void
) => [
  {
    title: "Model Name",
    dataIndex: "name",
    key: "name",
    width: 200,
  },
  {
    title: "Model ID",
    dataIndex: "model",
    key: "model",
    width: 250,
    ellipsis: true,
  },
  {
    title: "Family",
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
    title: "Format",
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
    title: "Size",
    dataIndex: "size",
    key: "size",
    width: 120,
    render: formatSize,
  },
  {
    title: "Quantization",
    dataIndex: ["details", "quantization_level"],
    key: "quantization_level",
    width: 130,
  },
  {
    title: "Modified",
    dataIndex: "modified_at",
    key: "modified_at",
    width: 180,
    render: formatDate,
  },
  {
    title: "Actions",
    key: "actions",
    width: 100,
    render: (_: any, record: LocalModel) => (
      <SharpButton
        type="text"
        danger
        size="small"
        icon={<DeleteOutlined />}
        onClick={() => onDelete?.(record)}
        title="Delete Model"
      />
    ),
  },
];
