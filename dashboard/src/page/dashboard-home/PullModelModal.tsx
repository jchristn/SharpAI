"use client";

import React from "react";
import { Alert, Form, message } from "antd";
import Modal from "#/components/base/modal/Modal";
import SharpButton from "#/components/base/button/Button";
import SharpInput from "#/components/base/input/Input";
import { usePullModelsMutation } from "#/lib/reducer/apiSlice";
import { formatError, formatSizeInMB, parseJSON } from "#/utils/utils";
import { useAppContext } from "#/hooks/appHooks";
import { AxiosProgressEvent } from "axios";
import { toast } from "react-hot-toast";
import SharpAlert from "#/components/base/alert/Alert";

interface PullModelModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

const PullModelModal: React.FC<PullModelModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [form] = Form.useForm();
  const [pullModel, { isLoading: isPulling }] = usePullModelsMutation();
  const { handleBackgroundTask } = useAppContext();
  const handleClose = () => {
    form.resetFields();
    onClose();
  };

  const handleSubmit = async (values: { modelName: string }) => {
    handleBackgroundTask(async () => {
      try {
        const res = await pullModel({
          model: values.modelName,
          onDownloadProgress: (pe: AxiosProgressEvent) => {
            const data: any[] | null = parseJSON(
              "[" +
                pe.event.currentTarget.responseText
                  .replace(/\n/g, "") // Remove all line breaks
                  .replace(/\r/g, "") // Remove carriage returns
                  .replaceAll("}{", "},{") + // Add commas between the objects
                "]"
            );
            const loaded = data?.[data.length - 1]?.downloaded;
            loaded &&
              toast.success(
                <div>
                  Pulling model <strong>"{values.modelName}"</strong>:{" "}
                  {formatSizeInMB(loaded)}
                </div>,
                {
                  id: "pull-model-toast",
                }
              );
          },
        }).unwrap();
        console.log(res);
        handleClose();
        onSuccess?.(); // Call the success callback (e.g., to refresh the models list)
      } catch (error) {
        message.error(`Failed to pull model: ${formatError(error)}`);
      }
    });
  };

  return (
    <Modal
      title="Pull Model"
      open={isOpen}
      onCancel={handleClose}
      footer={[
        <SharpButton key="cancel" onClick={handleClose}>
          Cancel
        </SharpButton>,
        <SharpButton
          key="submit"
          type="primary"
          loading={isPulling}
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
            autoFocus
            placeholder="Enter model name (e.g., llama2, mistral)"
            disabled={isPulling}
          />
        </Form.Item>
      </Form>
      {isPulling && (
        <SharpAlert
          className="mb"
          type="info"
          message="You can close this popup, the model will be pulled in the background."
        />
      )}
    </Modal>
  );
};

export default PullModelModal;
