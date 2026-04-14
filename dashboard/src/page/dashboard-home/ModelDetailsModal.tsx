import React from "react";
import Modal from "#/components/base/modal/Modal";
import SharpButton from "#/components/base/button/Button";
import SharpText from "#/components/base/typograpghy/Text";
import SharpFlex from "#/components/base/flex/Flex";
import { LocalModel } from "#/lib/reducer/types";
import { formatSize, formatDateTime } from "#/utils/utils";
import CopyText from "#/components/common/CopyText";

interface ModelDetailsModalProps {
  isOpen: boolean;
  onClose: () => void;
  model: LocalModel | null;
  mode: "details" | "json";
}

const DetailRow: React.FC<{ label: string; value: React.ReactNode }> = ({
  label,
  value,
}) => (
  <SharpFlex
    align="flex-start"
    gap={16}
    style={{
      padding: "6px 0",
    }}
  >
    <div style={{ width: 160, flexShrink: 0 }}>
      <SharpText
        style={{
          color: "var(--ant-color-text-secondary)",
          fontSize: 13,
        }}
      >
        {label}
      </SharpText>
    </div>
    <div style={{ flex: 1, wordBreak: "break-all" }}>
      {typeof value === "string" || typeof value === "number" ? (
        <SharpText style={{ fontSize: 13 }}>{value}</SharpText>
      ) : (
        value
      )}
    </div>
  </SharpFlex>
);

const ModelDetailsModal: React.FC<ModelDetailsModalProps> = ({
  isOpen,
  onClose,
  model,
  mode,
}) => {
  if (!model) return null;

  const isJson = mode === "json";
  const jsonText = JSON.stringify(model, null, 2);
  const families =
    model.details.families && model.details.families.length > 0
      ? model.details.families.join(", ")
      : "—";
  const caps = model.capabilities
    ? [
        model.capabilities.embeddings ? "embeddings" : null,
        model.capabilities.completions ? "completions" : null,
      ]
        .filter(Boolean)
        .join(", ") || "none"
    : "—";

  return (
    <Modal
      title={isJson ? "Model JSON" : "Model Details"}
      open={isOpen}
      onCancel={onClose}
      width={720}
      footer={[
        isJson ? (
          <CopyText key="copy" text={jsonText} displayText="Copy JSON" />
        ) : null,
        <SharpButton key="close" type="primary" onClick={onClose}>
          Close
        </SharpButton>,
      ]}
    >
      {isJson ? (
        <pre
          style={{
            maxHeight: "60vh",
            overflow: "auto",
            padding: 12,
            margin: 0,
            background: "var(--ant-color-fill-quaternary)",
            border: "1px solid var(--ant-color-border-secondary)",
            borderRadius: 6,
            fontSize: 12,
            fontFamily:
              "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
          }}
        >
          {jsonText}
        </pre>
      ) : (
        <div style={{ padding: "4px 0" }}>
          <DetailRow label="Name" value={model.name} />
          <DetailRow label="Model ID" value={model.model} />
          <DetailRow label="Digest" value={model.digest} />
          <DetailRow label="Size" value={formatSize(model.size)} />
          <DetailRow label="Modified" value={formatDateTime(model.modified_at)} />
          <DetailRow label="Family" value={model.details.family || "—"} />
          <DetailRow label="Families" value={families} />
          <DetailRow label="Format" value={model.details.format || "—"} />
          <DetailRow
            label="Parameter Size"
            value={model.details.parameter_size || "—"}
          />
          <DetailRow
            label="Quantization"
            value={model.details.quantization_level || "—"}
          />
          <DetailRow
            label="Parent Model"
            value={model.details.parent_model || "—"}
          />
          <DetailRow label="Capabilities" value={caps} />
        </div>
      )}
    </Modal>
  );
};

export default ModelDetailsModal;
