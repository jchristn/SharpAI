"use client";

import React, { useState } from "react";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import SharpTable from "#/components/base/table/Table";
import {
  useGetLocalModelsQuery,
  useDeleteModelMutation,
} from "#/lib/reducer/apiSlice";
import { LocalModel } from "#/lib/reducer/types";
import PageLoading from "#/components/base/loading/PageLoading";
import FallBack from "#/components/base/fallback/FallBack";
import SharpText from "#/components/base/typograpghy/Text";
import { createColumnConfig } from "./constants";
import { formatError } from "#/utils/utils";
import SharpButton from "#/components/base/button/Button";
import SharpFlex from "#/components/base/flex/Flex";
import { LinkOutlined, ReloadOutlined } from "@ant-design/icons";
import PullModelModal from "./PullModelModal";
import ConfirmationModal from "#/components/common/ConfirmationModal";
import { message } from "antd";

const DashboardHome = () => {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isConfirmModalOpen, setIsConfirmModalOpen] = useState(false);
  const [modelToDelete, setModelToDelete] = useState<LocalModel | null>(null);

  const {
    data: localModels,
    isLoading,
    isError,
    error,
    refetch,
  } = useGetLocalModelsQuery();

  const [deleteModel, { isLoading: isDeleting }] = useDeleteModelMutation();

  // Handle delete operations
  const handleDeleteClick = (model: LocalModel) => {
    setModelToDelete(model);
    setIsConfirmModalOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!modelToDelete) return;

    try {
      await deleteModel({ model: modelToDelete.name }).unwrap();
      message.success(`Successfully deleted model: ${modelToDelete.name}`);
      setIsConfirmModalOpen(false);
      setModelToDelete(null);
      refetch(); // Refresh the models list
    } catch (error) {
      message.error(`Failed to delete model: ${formatError(error)}`);
    }
  };

  const handleDeleteCancel = () => {
    setIsConfirmModalOpen(false);
    setModelToDelete(null);
  };

  // Define table columns using utilities
  const columns = createColumnConfig(localModels, handleDeleteClick);

  // Handle modal operations
  const handleOpenModal = () => {
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
  };

  const handlePullSuccess = () => {
    refetch(); // Refresh the models list after successful pull
  };

  // Handle loading state
  if (isLoading) {
    return <PageLoading message="Loading local models..." />;
  }

  // Handle error state
  if (isError) {
    return (
      <FallBack>
        <div style={{ textAlign: "center" }}>
          <p>Failed to load local models</p>
          <p style={{ fontSize: "0.9em", color: "#666", marginTop: "8px" }}>
            {formatError(error)}
          </p>
        </div>
      </FallBack>
    );
  }

  return (
    <PageContainer
      pageTitle={
        <SharpFlex align="center" gap="20">
          <SharpText>Local Models ({localModels?.length || 0})</SharpText>
          <ReloadOutlined className="ml-sm" onClick={refetch} />
        </SharpFlex>
      }
      pageTitleRightContent={
        <SharpFlex align="center" gap="20">
          <SharpButton
            icon={<LinkOutlined />}
            type="link"
            href="https://huggingface.co/models?search=gguf"
            target="_blank"
            rel="noopener noreferrer"
          >
            Available Models
          </SharpButton>
          <SharpButton type="primary" onClick={handleOpenModal}>
            Pull models
          </SharpButton>
        </SharpFlex>
      }
    >
      <SharpTable
        columns={columns as any}
        dataSource={Array.isArray(localModels) ? localModels : []}
        rowKey="digest"
        pagination={{
          pageSize: 10,
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total: number, range: [number, number]) =>
            `${range[0]}-${range[1]} of ${total} models`,
        }}
        scroll={{ x: 1200 }}
        size="middle"
      />

      {/* Pull Model Modal */}
      <PullModelModal
        isOpen={isModalOpen}
        onClose={handleCloseModal}
        onSuccess={handlePullSuccess}
      />

      {/* Delete Confirmation Modal */}
      <ConfirmationModal
        isOpen={isConfirmModalOpen}
        onClose={handleDeleteCancel}
        onConfirm={handleDeleteConfirm}
        title="Delete Model"
        message={
          modelToDelete
            ? `Are you sure you want to delete the model "${modelToDelete.name}"? This action cannot be undone.`
            : ""
        }
        confirmText="Delete"
        cancelText="Cancel"
        type="danger"
        isLoading={isDeleting}
      />
    </PageContainer>
  );
};

export default DashboardHome;
