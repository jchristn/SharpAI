"use client";

import React, { useState } from "react";
import { InputNumber, Switch, Form, Select, Input } from "antd";
import { SettingOutlined, CloseOutlined } from "@ant-design/icons";
import SharpButton from "#/components/base/button/Button";
import { completionOptions } from "../constants";
import styles from "./chatSettings.module.scss";
import SharpSelect from "#/components/base/select/Select";
import { requestFormatOptions } from "#/page/embeddings/constants";
import { RequestFormatEnum } from "#/types/types";
import SharpDivider from "#/components/base/divider/Divider";
import SharpInput from "#/components/base/input/Input";

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
                <label className={styles.optionLabel}>Request Type</label>
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
                  <label className={styles.optionLabel}>
                    Number of messages to retain in context
                  </label>
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
                <label className={styles.optionLabel}>Stream Response</label>
                <Switch
                  checked={streamEnabled}
                  onChange={onStreamToggle}
                  disabled={disabled}
                />
              </div>

              <div className={styles.optionItem}>
                <label className={styles.optionLabel}>
                  Temperature ({currentOptions.temperature})
                </label>
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
                <label className={styles.optionLabel}>
                  Top P ({currentOptions.top_p})
                </label>
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
                  <label className={styles.optionLabel}>
                    Top K ({currentOptions.top_k})
                  </label>
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
                <label className={styles.optionLabel}>Stop Sequences</label>
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
                <label className={styles.optionLabel}>
                  Max Tokens ({currentOptions.num_predict})
                </label>
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
                    <label className={styles.optionLabel}>
                      Repeat Penalty ({currentOptions.repeat_penalty})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Seed ({currentOptions.seed})
                    </label>
                    <InputNumber
                      value={currentOptions.seed}
                      onChange={(value) => onOptionChange("seed", value)}
                      style={{ width: "100%" }}
                      disabled={disabled}
                    />
                  </div>

                  <div className={styles.optionItem}>
                    <label className={styles.optionLabel}>
                      Context Length ({currentOptions.num_ctx})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Min P ({currentOptions.min_p})
                    </label>
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
                    <label className={styles.optionLabel}>
                      TFS Z ({currentOptions.tfs_z})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Typical P ({currentOptions.typical_p})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Mirostat ({currentOptions.mirostat})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Mirostat Tau ({currentOptions.mirostat_tau})
                    </label>
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
                    <label className={styles.optionLabel}>
                      Mirostat Eta ({currentOptions.mirostat_eta})
                    </label>
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
                <label className={styles.optionLabel}>
                  Presence Penalty ({currentOptions.presence_penalty})
                </label>
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
                <label className={styles.optionLabel}>
                  Frequency Penalty ({currentOptions.frequency_penalty})
                </label>
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
                  <label className={styles.optionLabel}>
                    Threads ({currentOptions.num_thread})
                  </label>
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
                  <label className={styles.optionLabel}>
                    Number of GPUs ({currentOptions.num_gpu})
                  </label>
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
                  <label className={styles.optionLabel}>
                    Number of Batches ({currentOptions.num_batch})
                  </label>
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
                  <label className={styles.optionLabel}>
                    Number of Tokens to Keep ({currentOptions.num_keep})
                  </label>
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
                  <label className={styles.optionLabel}>
                    Repeat Last N ({currentOptions.repeat_last_n})
                  </label>
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
                  <label className={styles.optionLabel}>
                    Main GPU ({currentOptions.main_gpu})
                  </label>
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
                  <label className={styles.optionLabel}>Low VRAM</label>
                  <Switch
                    checked={currentOptions.low_vram}
                    onChange={(value) => onOptionChange("low_vram", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>F16 KV Cache</label>
                  <Switch
                    checked={currentOptions.f16_kv}
                    onChange={(value) => onOptionChange("f16_kv", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>Use Memory Map</label>
                  <Switch
                    checked={currentOptions.use_mmap}
                    onChange={(value) => onOptionChange("use_mmap", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>Use Memory Lock</label>
                  <Switch
                    checked={currentOptions.use_mlock}
                    onChange={(value) => onOptionChange("use_mlock", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>Vocabulary Only</label>
                  <Switch
                    checked={currentOptions.vocab_only}
                    onChange={(value) => onOptionChange("vocab_only", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>NUMA</label>
                  <Switch
                    checked={currentOptions.numa}
                    onChange={(value) => onOptionChange("numa", value)}
                    disabled={disabled}
                  />
                </div>

                <div className={styles.optionItem}>
                  <label className={styles.optionLabel}>Penalize Newline</label>
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

            <SharpButton
              type="default"
              onClick={onResetDefaults}
              className={styles.resetButton}
              block
              disabled={disabled}
            >
              Reset to Defaults
            </SharpButton>
          </Form>
        </div>
      </div>
    </div>
  );
};

export default ChatSettings;
