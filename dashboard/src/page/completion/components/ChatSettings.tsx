
import React, { useState } from "react";
import { InputNumber, Switch, Form, Select } from "antd";
import { CloseOutlined, QuestionCircleOutlined } from "@ant-design/icons";
import SharpButton from "#/components/base/button/Button";
import { completionOptions } from "../constants";
import styles from "./chatSettings.module.scss";
import SharpSelect from "#/components/base/select/Select";
import { requestFormatOptions } from "#/page/embeddings/constants";
import { RequestFormatEnum } from "#/types/types";
import SharpDivider from "#/components/base/divider/Divider";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import { tooltips } from "#/constants/tooltips";

const L: React.FC<{ text: string; tooltip: string }> = ({ text, tooltip }) => (
  <label className={styles.optionLabel}>
    {text}{" "}
    <SharpTooltip title={tooltip}>
      <QuestionCircleOutlined
        style={{
          fontSize: 11,
          color: "var(--ant-color-text-secondary)",
          cursor: "help",
          marginLeft: 4,
        }}
      />
    </SharpTooltip>
  </label>
);

interface ChatSettingsProps {
  currentOptions: typeof completionOptions;
  onOptionChange: (key: keyof typeof completionOptions, value: any) => void;
  onResetDefaults: () => void;
  isCollapsed: boolean;
  onToggle: () => void;
  disabled?: boolean;
  streamEnabled: boolean;
  onStreamToggle: (enabled: boolean) => void;
  onRequestTypeChange?: (type: RequestFormatEnum) => void;
  numberOfMessages?: number;
  onNumberOfMessagesChange?: (value: number) => void;
}

const ChatSettings: React.FC<ChatSettingsProps> = ({
  currentOptions,
  onOptionChange,
  onResetDefaults,
  isCollapsed,
  onToggle,
  disabled = false,
  streamEnabled,
  onStreamToggle,
  onRequestTypeChange,
  numberOfMessages,
  onNumberOfMessagesChange,
}) => {
  const [requestType, setRequestType] = useState<RequestFormatEnum>(
    RequestFormatEnum.OLLAMA
  );
  if (isCollapsed) return null;
  return (
    <div className={styles.sidebarContainer}>
      <div className={`${styles.sidebar}`}>
        <div className={styles.sidebarHeader}>
          <h4>Chat Settings</h4>
          <button className={styles.toggleButton} onClick={onToggle}>
            <CloseOutlined />
          </button>
        </div>

        <div className={styles.sidebarContent}>
          <Form layout="vertical" size="small">
            {/* Core Parameters */}
            <div className={styles.optionGroup}>
              <div className={styles.groupTitle}>Core Parameters</div>
              <SharpDivider className="mt-0" />
              <div className={styles.optionItem}>
                <L
                  text="Request Type"
                  tooltip={tooltips.completionsCommon.requestType}
                />
                <SharpSelect
                  placeholder="Select a request type"
                  options={requestFormatOptions}
                  style={{ width: "100%" }}
                  value={requestType}
                  onChange={(value) => {
                    setRequestType(value as RequestFormatEnum);
                    onRequestTypeChange?.(value as RequestFormatEnum);
                  }}
                />
              </div>
              {onNumberOfMessagesChange && (
                <div className={styles.optionItem}>
                  <L
                    text="Number of messages to retain in context"
                    tooltip="How many of the most recent messages to include in the prompt sent to the model. Older messages are dropped."
                  />
                  <InputNumber
                    placeholder="number of messages"
                    style={{ width: "100%" }}
                    value={numberOfMessages}
                    onChange={(value) => {
                      onNumberOfMessagesChange?.(value as any);
                    }}
                  />
                </div>
              )}
              <div className={styles.optionItem}>
                <L
                  text="Stream Response"
                  tooltip={tooltips.completionsCommon.streamEnabled}
                />
                <Switch
                  checked={streamEnabled}
                  onChange={onStreamToggle}
                  disabled={disabled}
                />
              </div>

              <div className={styles.optionItem}>
                <L
                  text={`Temperature (${currentOptions.temperature})`}
                  tooltip={tooltips.chatSettings.temperature}
                />
                <InputNumber
                  min={0}
                  max={2}
                  step={0.1}
                  value={currentOptions.temperature}
                  onChange={(value) => onOptionChange("temperature", value)}
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>

              <div className={styles.optionItem}>
                <L
                  text={`Top P (${currentOptions.top_p})`}
                  tooltip={tooltips.chatSettings.topP}
                />
                <InputNumber
                  min={0}
                  max={1}
                  step={0.1}
                  value={currentOptions.top_p}
                  onChange={(value) => onOptionChange("top_p", value)}
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>

              {requestType === RequestFormatEnum.OLLAMA && (
                <div className={styles.optionItem}>
                  <L
                    text={`Top K (${currentOptions.top_k})`}
                    tooltip={tooltips.chatSettings.topK}
                  />
                  <InputNumber
                    min={1}
                    max={100}
                    value={currentOptions.top_k}
                    onChange={(value) => onOptionChange("top_k", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>
              )}
              <div className={styles.optionItem}>
                <L
                  text="Stop Sequences"
                  tooltip={tooltips.chatSettings.stop}
                />
                <Select
                  mode="tags"
                  value={
                    currentOptions.stop?.length
                      ? currentOptions.stop
                      : undefined
                  }
                  onChange={(e) => {
                    console.log(e);
                    onOptionChange("stop", e);
                  }}
                  placeholder="e.g. ###, END"
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>
              <div className={styles.optionItem}>
                <L
                  text={`Max Tokens (${currentOptions.num_predict})`}
                  tooltip={tooltips.chatSettings.numPredict}
                />
                <InputNumber
                  min={1}
                  max={4096}
                  value={currentOptions.num_predict}
                  onChange={(value) => onOptionChange("num_predict", value)}
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>
            </div>

            {/* Advanced Parameters */}
            <div className={styles.optionGroup}>
              <div className={styles.groupTitle}>Advanced</div>
              <SharpDivider className="mt-0" />

              {requestType === RequestFormatEnum.OLLAMA && (
                <>
                  <div className={styles.optionItem}>
                    <L
                      text={`Repeat Penalty (${currentOptions.repeat_penalty})`}
                      tooltip={tooltips.chatSettings.repeatPenalty}
                    />
                    <InputNumber
                      min={0}
                      max={2}
                      step={0.1}
                      value={currentOptions.repeat_penalty}
                      onChange={(value) =>
                        onOptionChange("repeat_penalty", value)
                      }
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Seed (${currentOptions.seed})`}
                      tooltip={tooltips.chatSettings.seed}
                    />
                    <InputNumber
                      value={currentOptions.seed}
                      onChange={(value) => onOptionChange("seed", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Context Length (${currentOptions.num_ctx})`}
                      tooltip={tooltips.chatSettings.numCtx}
                    />
                    <InputNumber
                      min={512}
                      max={8192}
                      value={currentOptions.num_ctx}
                      onChange={(value) => onOptionChange("num_ctx", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Min P (${currentOptions.min_p})`}
                      tooltip={tooltips.chatSettings.minP}
                    />
                    <InputNumber
                      min={0}
                      max={1}
                      step={0.01}
                      value={currentOptions.min_p}
                      onChange={(value) => onOptionChange("min_p", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`TFS Z (${currentOptions.tfs_z})`}
                      tooltip={tooltips.chatSettings.tfsZ}
                    />
                    <InputNumber
                      min={0}
                      max={1}
                      step={0.1}
                      value={currentOptions.tfs_z}
                      onChange={(value) => onOptionChange("tfs_z", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Typical P (${currentOptions.typical_p})`}
                      tooltip={tooltips.chatSettings.typicalP}
                    />
                    <InputNumber
                      min={0}
                      max={1}
                      step={0.1}
                      value={currentOptions.typical_p}
                      onChange={(value) => onOptionChange("typical_p", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Mirostat (${currentOptions.mirostat})`}
                      tooltip={tooltips.chatSettings.mirostat}
                    />
                    <InputNumber
                      min={0}
                      max={2}
                      value={currentOptions.mirostat}
                      onChange={(value) => onOptionChange("mirostat", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Mirostat Tau (${currentOptions.mirostat_tau})`}
                      tooltip={tooltips.chatSettings.mirostatTau}
                    />
                    <InputNumber
                      min={0}
                      max={10}
                      step={0.1}
                      value={currentOptions.mirostat_tau}
                      onChange={(value) =>
                        onOptionChange("mirostat_tau", value)
                      }
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <L
                      text={`Mirostat Eta (${currentOptions.mirostat_eta})`}
                      tooltip={tooltips.chatSettings.mirostatEta}
                    />
                    <InputNumber
                      min={0}
                      max={1}
                      step={0.1}
                      value={currentOptions.mirostat_eta}
                      onChange={(value) =>
                        onOptionChange("mirostat_eta", value)
                      }
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>
                </>
              )}

              <div className={styles.optionItem}>
                <L
                  text={`Presence Penalty (${currentOptions.presence_penalty})`}
                  tooltip={tooltips.chatSettings.presencePenalty}
                />
                <InputNumber
                  min={-2}
                  max={2}
                  step={0.1}
                  value={currentOptions.presence_penalty}
                  onChange={(value) =>
                    onOptionChange("presence_penalty", value)
                  }
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>

              <div className={styles.optionItem}>
                <L
                  text={`Frequency Penalty (${currentOptions.frequency_penalty})`}
                  tooltip={tooltips.chatSettings.frequencyPenalty}
                />
                <InputNumber
                  min={-2}
                  max={2}
                  step={0.1}
                  value={currentOptions.frequency_penalty}
                  onChange={(value) =>
                    onOptionChange("frequency_penalty", value)
                  }
                  style={{ width: "100%" }}
                  disabled={disabled}
                />
              </div>
            </div>

            {/* Performance Parameters */}
            {requestType === RequestFormatEnum.OLLAMA && (
              <div className={styles.optionGroup}>
                <div className={styles.groupTitle}>Performance</div>
                <SharpDivider className="mt-0" />

                <div className={styles.optionItem}>
                  <L
                    text={`Threads (${currentOptions.num_thread})`}
                    tooltip={tooltips.chatSettings.numThread}
                  />
                  <InputNumber
                    min={1}
                    max={32}
                    value={currentOptions.num_thread}
                    onChange={(value) => onOptionChange("num_thread", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text={`Number of GPUs (${currentOptions.num_gpu})`}
                    tooltip={tooltips.chatSettings.numGpu}
                  />
                  <InputNumber
                    min={0}
                    max={100}
                    value={currentOptions.num_gpu}
                    onChange={(value) => onOptionChange("num_gpu", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text={`Number of Batches (${currentOptions.num_batch})`}
                    tooltip={tooltips.chatSettings.numBatch}
                  />
                  <InputNumber
                    min={1}
                    max={512}
                    value={currentOptions.num_batch}
                    onChange={(value) => onOptionChange("num_batch", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text={`Number of Tokens to Keep (${currentOptions.num_keep})`}
                    tooltip={tooltips.chatSettings.numKeep}
                  />
                  <InputNumber
                    min={0}
                    max={100}
                    value={currentOptions.num_keep}
                    onChange={(value) => onOptionChange("num_keep", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text={`Repeat Last N (${currentOptions.repeat_last_n})`}
                    tooltip={tooltips.chatSettings.repeatLastN}
                  />
                  <InputNumber
                    min={-1}
                    max={512}
                    value={currentOptions.repeat_last_n}
                    onChange={(value) => onOptionChange("repeat_last_n", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text={`Main GPU (${currentOptions.main_gpu})`}
                    tooltip={tooltips.chatSettings.mainGpu}
                  />
                  <InputNumber
                    min={0}
                    max={8}
                    value={currentOptions.main_gpu}
                    onChange={(value) => onOptionChange("main_gpu", value)}
                    style={{ width: "100%" }}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L text="Low VRAM" tooltip={tooltips.chatSettings.lowVram} />
                  <Switch
                    checked={currentOptions.low_vram}
                    onChange={(value) => onOptionChange("low_vram", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="Display Thinking"
                    tooltip={tooltips.chatSettings.displayThinking}
                  />
                  <Switch
                    checked={currentOptions.display_thinking}
                    onChange={(value) =>
                      onOptionChange("display_thinking", value)
                    }
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="F16 KV Cache"
                    tooltip={tooltips.chatSettings.f16Kv}
                  />
                  <Switch
                    checked={currentOptions.f16_kv}
                    onChange={(value) => onOptionChange("f16_kv", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="Use Memory Map"
                    tooltip={tooltips.chatSettings.useMmap}
                  />
                  <Switch
                    checked={currentOptions.use_mmap}
                    onChange={(value) => onOptionChange("use_mmap", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="Use Memory Lock"
                    tooltip={tooltips.chatSettings.useMlock}
                  />
                  <Switch
                    checked={currentOptions.use_mlock}
                    onChange={(value) => onOptionChange("use_mlock", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="Vocabulary Only"
                    tooltip={tooltips.chatSettings.vocabOnly}
                  />
                  <Switch
                    checked={currentOptions.vocab_only}
                    onChange={(value) => onOptionChange("vocab_only", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L text="NUMA" tooltip={tooltips.chatSettings.numa} />
                  <Switch
                    checked={currentOptions.numa}
                    onChange={(value) => onOptionChange("numa", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <L
                    text="Penalize Newline"
                    tooltip={tooltips.chatSettings.penalizeNewline}
                  />
                  <Switch
                    checked={currentOptions.penalize_newline}
                    onChange={(value) =>
                      onOptionChange("penalize_newline", value)
                    }
                    disabled={disabled}
                  />
                </div>
              </div>
            )}

            <SharpTooltip title={tooltips.chatSettings.resetDefaults}>
              <SharpButton
                type="default"
                onClick={onResetDefaults}
                className={styles.resetButton}
                block
                disabled={disabled}
              >
                Reset to Defaults
              </SharpButton>
            </SharpTooltip>
          </Form>
        </div>
      </div>
    </div>
  );
};

export default ChatSettings;
