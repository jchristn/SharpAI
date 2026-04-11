
import React, { useRef } from "react";
import { Form } from "antd";
import { InputRef } from "antd/es/input";
import Modal from "#/components/base/modal/Modal";
import SharpButton from "#/components/base/button/Button";
import SharpInput from "#/components/base/input/Input";
import { usePullProgress } from "#/hooks/usePullProgress";
import { tooltips } from "#/constants/tooltips";

interface PullModelModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const PullModelModal: React.FC<PullModelModalProps> = ({ isOpen, onClose }) => {
  const [form] = Form.useForm();
  const { startPull } = usePullProgress();
  const inputRef = useRef<InputRef>(null);

  const handleClose = () => {
    form.resetFields();
    onClose();
  };

  const handleSubmit = (values: { modelName: string }) => {
    startPull(values.modelName);
    handleClose();
  };

  return (
    <Modal
      title="Pull Model"
      open={isOpen}
      onCancel={handleClose}
      afterOpenChange={(open) => {
        if (open) inputRef.current?.focus();
      }}
      footer={[
        <SharpButton key="cancel" onClick={handleClose}>
          Cancel
        </SharpButton>,
        <SharpButton
          key="submit"
          type="primary"
          onClick={() => form.submit()}
        >
          Pull Model
        </SharpButton>,
      ]}
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        autoComplete="off"
      >
        <Form.Item
          className="mt mb"
          name="modelName"
          label="Model Name"
          tooltip={tooltips.pullModal.modelName}
          rules={[
            {
              required: true,
              message: "Please enter a model name",
            },
            {
              min: 1,
              message: "Model name cannot be empty",
            },
          ]}
        >
          <SharpInput
            ref={inputRef}
            placeholder="Enter model name (e.g., llama2, mistral)"
          />
        </Form.Item>
      </Form>
    </Modal>
  );
};

export default PullModelModal;
