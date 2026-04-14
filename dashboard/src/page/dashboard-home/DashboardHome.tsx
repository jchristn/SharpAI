
import React, { useState } from "react";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import SharpTable from "#/components/base/table/Table";
import {
  useGetLocalModelsQuery,
  useGetRunningModelsQuery,
  useDeleteModelMutation,
  useUnloadModelMutation,
} from "#/lib/reducer/apiSlice";
import { LocalModel, RunningModel } from "#/lib/reducer/types";
import PageLoading from "#/components/base/loading/PageLoading";
import FallBack from "#/components/base/fallback/FallBack";
import SharpText from "#/components/base/typograpghy/Text";
import { createColumnConfig } from "./constants";
import { formatError, formatSizeInMB } from "#/utils/utils";
import SharpButton from "#/components/base/button/Button";
import SharpFlex from "#/components/base/flex/Flex";
import {
  CloseCircleOutlined,
  LinkOutlined,
  LoadingOutlined,
  PlayCircleOutlined,
  PoweroffOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import PullModelModal from "./PullModelModal";
import ConfirmationModal from "#/components/common/ConfirmationModal";
import { message, Progress } from "antd";
import { formatSize } from "#/utils/utils";
import { usePullProgress } from "#/hooks/usePullProgress";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import TooltipHeader from "#/components/base/tooltip/TooltipHeader";
import { tooltips, pageDescriptions } from "#/constants/tooltips";

const DashboardHome = () => {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isConfirmModalOpen, setIsConfirmModalOpen] = useState(false);
  const [modelToDelete, setModelToDelete] = useState<LocalModel | null>(null);
  const { activePulls, cancelPull } = usePullProgress();

  const {
    data: localModels,
    isLoading,
    isError,
    error,
    refetch,
  } = useGetLocalModelsQuery();

  const { data: runningModelsData, refetch: refetchRunning } =
    useGetRunningModelsQuery(undefined, {
      pollingInterval: 5000,
      refetchOnMountOrArgChange: true,
    });
  const runningModels: RunningModel[] = runningModelsData?.models ?? [];

  const [deleteModel, { isLoading: isDeleting }] = useDeleteModelMutation();
  const [unloadModel, { isLoading: isUnloading }] = useUnloadModelMutation();

  // Handle unload operations
  const handleUnloadModel = async (modelName: string) => {
    try {
      await unloadModel({ model: modelName }).unwrap();
      message.success(`Successfully unloaded model: ${modelName}`);
      refetchRunning();
    } catch (error) {
      message.error(`Failed to unload model: ${formatError(error)}`);
    }
  };

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
      refetch();
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
        <SharpText>Local Models ({localModels?.length || 0})</SharpText>
      }
      pageSubtitle={pageDescriptions.models}
      pageTitleRightContent={
        <SharpFlex align="center" gap="20">
          <SharpTooltip title="Browse GGUF models on HuggingFace in a new tab.">
            <SharpButton
              icon={<LinkOutlined />}
              type="link"
              href="https://huggingface.co/models?search=gguf"
              target="_blank"
              rel="noopener noreferrer"
            >
              Available Models
            </SharpButton>
          </SharpTooltip>
          <SharpTooltip title="Download a new model from HuggingFace by its 'organization/repo' slug.">
            <SharpButton type="primary" onClick={handleOpenModal}>
              Pull models
            </SharpButton>
          </SharpTooltip>
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
          showTotal: (total: number, range: [number, number]) => (
            <SharpFlex align="center" gap={8}>
              <ReloadOutlined
                onClick={refetch}
                title="Refresh"
                style={{ cursor: "pointer" }}
              />
              <span>
                {range[0]}-{range[1]} of {total} models
              </span>
            </SharpFlex>
          ),
        }}
        size="middle"
      />

      {/* Running Models */}
      <div style={{ marginTop: 24 }}>
        <SharpFlex
          align="center"
          gap={8}
          style={{
            marginBottom: 12,
          }}
        >
          <PlayCircleOutlined style={{ color: "var(--ant-color-success)" }} />
          <SharpTooltip title={tooltips.running.heading}>
            <SharpText
              style={{
                fontWeight: 600,
                fontSize: 14,
                cursor: "help",
              }}
            >
              Running Models ({runningModels.length})
            </SharpText>
          </SharpTooltip>
          <SharpTooltip title="Refresh the running models list immediately.">
            <ReloadOutlined
              onClick={refetchRunning}
              style={{ cursor: "pointer", fontSize: 12, opacity: 0.6 }}
            />
          </SharpTooltip>
        </SharpFlex>
        {runningModels.length === 0 ? (
          <SharpText
            style={{
              fontSize: 13,
              color: "var(--ant-color-text-secondary)",
              fontStyle: "italic",
            }}
          >
            No models currently loaded in memory.
          </SharpText>
        ) : (
          <SharpTable
            columns={
              [
                {
                  title: (
                    <TooltipHeader
                      label="Name"
                      tooltip={tooltips.running.name}
                    />
                  ),
                  dataIndex: "name",
                  key: "name",
                  ellipsis: true,
                },
                {
                  title: (
                    <TooltipHeader
                      label="Family"
                      tooltip={tooltips.running.family}
                    />
                  ),
                  dataIndex: ["details", "family"],
                  key: "family",
                  width: 160,
                },
                {
                  title: (
                    <TooltipHeader
                      label="Quantization"
                      tooltip={tooltips.running.quantization}
                    />
                  ),
                  dataIndex: ["details", "quantization_level"],
                  key: "quantization",
                  width: 160,
                },
                {
                  title: (
                    <TooltipHeader
                      label="Size"
                      tooltip={tooltips.running.size}
                    />
                  ),
                  dataIndex: "size",
                  key: "size",
                  width: 130,
                  align: "right",
                  render: (value: number) => formatSize(value),
                },
                {
                  title: (
                    <TooltipHeader
                      label="VRAM"
                      tooltip={tooltips.running.vram}
                    />
                  ),
                  dataIndex: "size_vram",
                  key: "size_vram",
                  width: 130,
                  align: "right",
                  render: (value: number) =>
                    value > 0 ? (
                      formatSize(value)
                    ) : (
                      <SharpTooltip title={tooltips.running.vram}>
                        <span style={{ cursor: "help" }}>—</span>
                      </SharpTooltip>
                    ),
                },
                {
                  title: "",
                  key: "actions",
                  width: 80,
                  align: "center",
                  render: (_: any, record: RunningModel) => (
                    <SharpTooltip title="Unload model from memory">
                      <SharpButton
                        type="text"
                        danger
                        size="small"
                        icon={<PoweroffOutlined />}
                        loading={isUnloading}
                        onClick={() => handleUnloadModel(record.name)}
                      >
                        Unload
                      </SharpButton>
                    </SharpTooltip>
                  ),
                },
              ] as any
            }
            dataSource={runningModels}
            rowKey="digest"
            pagination={false}
            size="small"
          />
        )}
      </div>

      {/* Active Pull Progress */}
      {Object.keys(activePulls).length > 0 && (
        <div style={{ marginTop: 24 }}>
          <SharpFlex
            align="center"
            gap={8}
            style={{ marginBottom: 12 }}
          >
            <LoadingOutlined />
            <SharpTooltip title={tooltips.pulling.heading}>
              <SharpText
                style={{
                  fontWeight: 600,
                  fontSize: 14,
                  cursor: "help",
                }}
              >
                Pulling Models ({Object.keys(activePulls).length})
              </SharpText>
            </SharpTooltip>
          </SharpFlex>
          <SharpTable
            columns={[
              {
                title: (
                  <TooltipHeader
                    label="Model"
                    tooltip={tooltips.pulling.modelName}
                  />
                ),
                dataIndex: "modelName",
                key: "modelName",
                ellipsis: true,
              },
              {
                title: (
                  <TooltipHeader
                    label="Status"
                    tooltip={tooltips.pulling.status}
                  />
                ),
                dataIndex: "status",
                key: "status",
                width: 180,
                ellipsis: true,
              },
              {
                title: (
                  <TooltipHeader
                    label="Progress"
                    tooltip={tooltips.pulling.progress}
                  />
                ),
                key: "progress",
                width: 280,
                render: (_: any, record: any) => {
                  const percent =
                    record.total && record.total > 0
                      ? Math.round((record.downloaded / record.total) * 100)
                      : 0;
                  return (
                    <Progress
                      percent={percent}
                      size="small"
                      status="active"
                    />
                  );
                },
              },
              {
                title: (
                  <TooltipHeader
                    label="Size"
                    tooltip={tooltips.pulling.size}
                  />
                ),
                key: "size",
                width: 180,
                align: "right",
                render: (_: any, record: any) => (
                  <SharpText
                    style={{
                      fontSize: 12,
                      whiteSpace: "nowrap",
                      fontVariantNumeric: "tabular-nums",
                    }}
                  >
                    {record.total && record.total > 0
                      ? `${formatSizeInMB(record.downloaded)} / ${formatSizeInMB(record.total)}`
                      : "—"}
                  </SharpText>
                ),
              },
              {
                title: "",
                key: "actions",
                width: 56,
                align: "right",
                render: (_: any, record: any) => (
                  <SharpTooltip title={tooltips.pulling.cancel}>
                    <SharpButton
                      type="text"
                      danger
                      size="small"
                      icon={<CloseCircleOutlined />}
                      onClick={() => cancelPull(record.modelName)}
                    />
                  </SharpTooltip>
                ),
              },
            ] as any}
            dataSource={Object.values(activePulls)}
            rowKey="modelName"
            pagination={false}
            size="small"
          />
        </div>
      )}

      {/* Pull Model Modal */}
      <PullModelModal isOpen={isModalOpen} onClose={handleCloseModal} />

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
