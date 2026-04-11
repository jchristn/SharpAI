
import React from "react";
import Modal from "#/components/base/modal/Modal";
import SharpButton from "#/components/base/button/Button";
import SharpText from "#/components/base/typograpghy/Text";
import SharpTitle from "#/components/base/typograpghy/Title";
import { ExclamationCircleOutlined } from "@ant-design/icons";
import SharpFlex from "#/components/base/flex/Flex";

interface ConfirmationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title?: string;
  message?: string;
  confirmText?: string;
  cancelText?: string;
  type?: "danger" | "warning" | "info";
  isLoading?: boolean;
}

const ConfirmationModal: React.FC<ConfirmationModalProps> = ({
  isOpen,
  onClose,
  onConfirm,
  title = "Confirm Action",
  message = "Are you sure you want to proceed?",
  confirmText = "Confirm",
  cancelText = "Cancel",
  type = "warning",
  isLoading = false,
}) => {
  return (
    <Modal
      title={
        <SharpFlex align="center" gap="8">
          <ExclamationCircleOutlined
            style={{
              color: type === "danger" ? "#ff4d4f" : "#faad14",
              fontSize: "18px",
            }}
            className="mr-sm"
          />
          <SharpTitle level={5} style={{ margin: 0 }}>
            {title}
          </SharpTitle>
        </SharpFlex>
      }
      open={isOpen}
      onCancel={onClose}
      footer={[
        <SharpButton key="cancel" onClick={onClose} disabled={isLoading}>
          {cancelText}
        </SharpButton>,
        <SharpButton
          key="confirm"
          type={"primary"}
          danger={true}
          loading={isLoading}
          onClick={onConfirm}
        >
          {confirmText}
        </SharpButton>,
      ]}
      width={400}
    >
      <div style={{ padding: "16px 0" }}>
        <SharpText>{message}</SharpText>
      </div>
    </Modal>
  );
};

export default ConfirmationModal;
