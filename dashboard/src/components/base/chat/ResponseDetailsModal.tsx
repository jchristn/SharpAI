import React from "react";
import { Input, Modal } from "antd";
import { ResponseMetadata } from "./Chat";
import SharpTitle from "../typograpghy/Title";
import SharpText from "../typograpghy/Text";
import SharpInput from "../input/Input";
import SharpDivider from "../divider/Divider";
import CopyText from "#/components/common/CopyText";
import SharpFlex from "../flex/Flex";

interface ResponseDetailsModalProps {
  visible: boolean;
  metadata: ResponseMetadata | null;
  onClose: () => void;
}

const ResponseDetailsModal: React.FC<ResponseDetailsModalProps> = ({
  visible,
  metadata,
  onClose,
}) => {
  const formatMetadataContent = (metadata: ResponseMetadata) => {
    if (!metadata) return null;

    const { status, headers, body } = metadata;

    return (
      <div>
        <SharpDivider className="mt-sm mb-sm" />
        {status && (
          <div style={{ marginBottom: 16 }}>
            <SharpTitle level={5} weight={600} className="mb-sm">
              Status Information
            </SharpTitle>
            <div>
              <SharpText>Code: {status.code}</SharpText>
            </div>
            <div>
              <SharpText>Text: {status.text}</SharpText>
            </div>
            <div>
              {status.timeToFirstToken !== undefined && (
                <SharpText>
                  Time to First Token: {status.timeToFirstToken} ms
                </SharpText>
              )}{" "}
            </div>
            <div>
              {status.totalStreamingTime !== undefined && (
                <SharpText>
                  Total Streaming Time: {status.totalStreamingTime} ms
                </SharpText>
              )}
            </div>
          </div>
        )}
        <SharpDivider className="mt-sm mb-sm" />
        <SharpTitle level={5} weight={600} className="mb-sm">
          Headers
        </SharpTitle>
        {headers && Object.keys(headers).length > 0 && (
          <div style={{ marginBottom: 16 }}>
            {Object.entries(headers).map(([key, value]) => (
              <SharpText key={key}>
                {key}: {value}
              </SharpText>
            ))}
          </div>
        )}
        {body && (
          <div>
            <SharpDivider className="mt-sm mb-sm" />
            <SharpFlex align="center" justify="space-between" gap={12}>
              <SharpTitle level={5} weight={600} className="mb-sm">
                Response Body
              </SharpTitle>
              <CopyText
                displayText="Copy body"
                text={JSON.stringify(body, null, 2)}
              />
            </SharpFlex>
            <Input.TextArea value={JSON.stringify(body, null, 2)} rows={10} />
          </div>
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
      width={600}
      destroyOnHidden
    >
      {metadata && formatMetadataContent(metadata)}
    </Modal>
  );
};

export default ResponseDetailsModal;
