import React from "react";
import { Input, Modal } from "antd";
import { ResponseMetadata } from "./Chat";
import SharpTitle from "../typograpghy/Title";
import SharpText from "../typograpghy/Text";
import SharpDivider from "../divider/Divider";
import CopyText from "#/components/common/CopyText";
import SharpFlex from "../flex/Flex";

interface ResponseDetailsModalProps {
  visible: boolean;
  metadata: ResponseMetadata | null;
  onClose: () => void;
}

interface StatusRow {
  label: string;
  value: React.ReactNode;
}

const StatusGrid: React.FC<{ rows: StatusRow[] }> = ({ rows }) => {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "200px 1fr",
        rowGap: 6,
        columnGap: 16,
      }}
    >
      {rows.map((row) => (
        <React.Fragment key={row.label}>
          <SharpText
            style={{
              color: "var(--ant-color-text-secondary)",
              fontSize: 13,
            }}
          >
            {row.label}
          </SharpText>
          <SharpText
            style={{
              fontSize: 13,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {row.value}
          </SharpText>
        </React.Fragment>
      ))}
    </div>
  );
};

const ResponseDetailsModal: React.FC<ResponseDetailsModalProps> = ({
  visible,
  metadata,
  onClose,
}) => {
  const formatMetadataContent = (metadata: ResponseMetadata) => {
    if (!metadata) return null;

    const { status, headers, body } = metadata;

    const statusRows: StatusRow[] = [];
    if (status) {
      statusRows.push({ label: "Code", value: String(status.code) });
      statusRows.push({ label: "Text", value: status.text || "—" });
      if (status.timeToFirstToken !== undefined) {
        statusRows.push({
          label: "Time to first token",
          value: `${status.timeToFirstToken} ms`,
        });
      }
      if (status.totalStreamingTime !== undefined) {
        statusRows.push({
          label: "Total streaming time",
          value: `${status.totalStreamingTime} ms`,
        });
      }
    }

    const headerRows: StatusRow[] =
      headers && Object.keys(headers).length > 0
        ? Object.entries(headers).map(([key, value]) => ({
            label: key,
            value: String(value),
          }))
        : [];

    return (
      <div>
        <SharpDivider className="mt-sm mb-sm" />
        {statusRows.length > 0 && (
          <div style={{ marginBottom: 20 }}>
            <SharpTitle level={5} weight={600} className="mb-sm">
              Status Information
            </SharpTitle>
            <StatusGrid rows={statusRows} />
          </div>
        )}
        {headerRows.length > 0 && (
          <>
            <SharpDivider className="mt-sm mb-sm" />
            <div style={{ marginBottom: 20 }}>
              <SharpTitle level={5} weight={600} className="mb-sm">
                Headers
              </SharpTitle>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "280px 1fr",
                  rowGap: 4,
                  columnGap: 16,
                  fontFamily:
                    "'Courier Prime', ui-monospace, SFMono-Regular, Menlo, Consolas, monospace",
                  fontSize: 12,
                  lineHeight: 1.6,
                }}
              >
                {headerRows.map((row) => (
                  <React.Fragment key={row.label}>
                    <span
                      style={{
                        color: "var(--ant-color-text-secondary)",
                        wordBreak: "break-all",
                      }}
                    >
                      {row.label}
                    </span>
                    <span
                      style={{
                        wordBreak: "break-all",
                      }}
                    >
                      {row.value}
                    </span>
                  </React.Fragment>
                ))}
              </div>
            </div>
          </>
        )}
        {body && (
          <>
            <SharpDivider className="mt-sm mb-sm" />
            <div>
              <SharpFlex align="center" justify="space-between" gap={12}>
                <SharpTitle level={5} weight={600} className="mb-sm">
                  Response Body
                </SharpTitle>
                <CopyText
                  displayText="Copy body"
                  text={JSON.stringify(body, null, 2)}
                />
              </SharpFlex>
              <Input.TextArea
                value={JSON.stringify(body, null, 2)}
                rows={14}
                readOnly
                style={{
                  fontFamily: "'Courier Prime', ui-monospace, monospace",
                  fontSize: 12,
                }}
              />
            </div>
          </>
        )}
      </div>
    );
  };

  return (
    <Modal
      title="Response Details"
      open={visible}
      onCancel={onClose}
      footer={null}
      width={1050}
      styles={{
        body: {
          maxHeight: "calc(100vh - 180px)",
          overflowY: "auto",
        },
      }}
      destroyOnHidden
    >
      {metadata && formatMetadataContent(metadata)}
    </Modal>
  );
};

export default ResponseDetailsModal;
