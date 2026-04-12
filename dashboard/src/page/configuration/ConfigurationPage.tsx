
import React, { useEffect, useState } from "react";
import {
  Collapse,
  Form,
  Input,
  InputNumber,
  Switch,
  Select,
  message,
  Space,
  Button,
} from "antd";
import {
  MinusCircleOutlined,
  PlusOutlined,
  SaveOutlined,
  UndoOutlined,
  WarningOutlined,
} from "@ant-design/icons";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import SharpButton from "#/components/base/button/Button";
import SharpFlex from "#/components/base/flex/Flex";
import SharpText from "#/components/base/typograpghy/Text";
import PageLoading from "#/components/base/loading/PageLoading";
import FallBack from "#/components/base/fallback/FallBack";
import {
  useGetSettingsQuery,
  useUpdateSettingsMutation,
} from "#/lib/reducer/apiSlice";
import { SharpAISettings } from "#/lib/reducer/types";
import { formatError } from "#/utils/utils";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import { tooltips, pageDescriptions } from "#/constants/tooltips";

const { Panel } = Collapse;
const t = tooltips.config;

const ConfigurationPage = () => {
  const [form] = Form.useForm();
  const { data: settings, isLoading, isError, error, refetch } = useGetSettingsQuery();
  const [updateSettings, { isLoading: isSaving }] = useUpdateSettingsMutation();
  const [hasChanges, setHasChanges] = useState(false);

  useEffect(() => {
    if (settings) {
      form.setFieldsValue(settingsToFormValues(settings));
      setHasChanges(false);
    }
  }, [settings, form]);

  const settingsToFormValues = (s: SharpAISettings) => ({
    ...s,
    Runtime: {
      ...s.Runtime,
      ForceBackend: s.Runtime.ForceBackend ?? "auto",
    },
    QuantizationPriorityEntries: Object.entries(s.QuantizationPriority || {}).map(
      ([key, value]) => ({ key, value })
    ),
  });

  const formValuesToSettings = (values: any): SharpAISettings => {
    const { QuantizationPriorityEntries, ...rest } = values;
    const qp: Record<string, number> = {};
    if (QuantizationPriorityEntries) {
      for (const entry of QuantizationPriorityEntries) {
        if (entry?.key) qp[entry.key] = entry.value ?? 0;
      }
    }
    return {
      ...rest,
      Runtime: {
        ...rest.Runtime,
        ForceBackend:
          rest.Runtime.ForceBackend === "auto"
            ? null
            : rest.Runtime.ForceBackend,
      },
      QuantizationPriority: qp,
    };
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      const payload = formValuesToSettings(values);
      await updateSettings(payload).unwrap();
      message.success("Settings saved successfully");
      refetch();
      setHasChanges(false);
    } catch (err: any) {
      if (err?.errorFields) {
        message.error("Please fix form validation errors");
      } else {
        message.error(`Failed to save settings: ${formatError(err)}`);
      }
    }
  };

  const handleReset = () => {
    if (settings) {
      form.setFieldsValue(settingsToFormValues(settings));
      setHasChanges(false);
    }
  };

  if (isLoading) {
    return <PageLoading message="Loading settings..." />;
  }

  if (isError) {
    return (
      <FallBack>
        <div style={{ textAlign: "center" }}>
          <p>Failed to load settings</p>
          <p style={{ fontSize: "0.9em", color: "#666", marginTop: "8px" }}>
            {formatError(error)}
          </p>
        </div>
      </FallBack>
    );
  }

  const restartWarning = (
    <SharpText style={{ fontSize: 12, color: "var(--ant-color-warning)" }}>
      <WarningOutlined /> Changes require server restart
    </SharpText>
  );

  return (
    <PageContainer
      pageTitle={<SharpText>Configuration</SharpText>}
      pageSubtitle={pageDescriptions.configuration}
      pageTitleRightContent={
        <SharpFlex align="center" gap="12">
          <SharpTooltip title={t.reset}>
            <SharpButton
              icon={<UndoOutlined />}
              onClick={handleReset}
              disabled={!hasChanges}
            >
              Reset
            </SharpButton>
          </SharpTooltip>
          <SharpTooltip title={t.save}>
            <SharpButton
              type="primary"
              icon={<SaveOutlined />}
              onClick={handleSave}
              loading={isSaving}
              disabled={!hasChanges}
            >
              Save
            </SharpButton>
          </SharpTooltip>
        </SharpFlex>
      }
    >
      <Form
        form={form}
        layout="vertical"
        onValuesChange={() => setHasChanges(true)}
        autoComplete="off"
      >
        {/* Read-only info */}
        <SharpFlex gap={24} style={{ marginBottom: 16 }}>
          <SharpText style={{ fontSize: 13, color: "var(--ant-color-text-secondary)" }}>
            Version: {settings?.SoftwareVersion}
          </SharpText>
          <SharpText style={{ fontSize: 13, color: "var(--ant-color-text-secondary)" }}>
            Created: {settings?.CreatedUtc ? new Date(settings.CreatedUtc).toLocaleString() : "—"}
          </SharpText>
        </SharpFlex>

        <Collapse
          defaultActiveKey={[
            "logging",
            "storage",
            "database",
            "huggingface",
            "rest",
            "runtime",
            "debug",
            "quantization",
          ]}
          style={{ marginBottom: 16 }}
        >
          {/* Logging */}
          <Panel header="Logging" key="logging">
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Log Directory"
                name={["Logging", "LogDirectory"]}
                tooltip={t.loggingDirectory}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
              <Form.Item
                label="Log Filename"
                name={["Logging", "LogFilename"]}
                tooltip={t.loggingFilename}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
              <Form.Item
                label="Minimum Severity"
                name={["Logging", "MinimumSeverity"]}
                tooltip={t.loggingSeverity}
                style={{ minWidth: 150 }}
              >
                <InputNumber min={0} max={7} />
              </Form.Item>
            </SharpFlex>
            <SharpFlex gap={24}>
              <Form.Item
                label="Console Logging"
                name={["Logging", "ConsoleLogging"]}
                tooltip={t.loggingConsole}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Enable Colors"
                name={["Logging", "EnableColors"]}
                tooltip={t.loggingColors}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </SharpFlex>

            <SharpTooltip title={t.syslogServers}>
              <SharpText
                style={{
                  fontWeight: 600,
                  marginBottom: 8,
                  display: "block",
                  cursor: "help",
                }}
              >
                Syslog Servers
              </SharpText>
            </SharpTooltip>
            <Form.List name={["Logging", "Servers"]}>
              {(fields, { add, remove }) => {
                const addNew = () =>
                  add({
                    Hostname: "127.0.0.1",
                    Port: 514,
                    RandomizePorts: false,
                    MinimumPort: 65000,
                    MaximumPort: 65535,
                  });
                return (
                  <>
                    {fields.map(({ key, name, ...restField }, idx) => (
                      <SharpFlex
                        key={key}
                        gap={8}
                        align="baseline"
                        style={{ marginBottom: 8 }}
                      >
                        <SharpTooltip title={t.syslogHostname}>
                          <Form.Item
                            {...restField}
                            name={[name, "Hostname"]}
                            rules={[
                              { required: true, message: "Required" },
                            ]}
                            style={{ flex: 1 }}
                          >
                            <Input placeholder="Hostname" />
                          </Form.Item>
                        </SharpTooltip>
                        <SharpTooltip title={t.syslogPort}>
                          <Form.Item
                            {...restField}
                            name={[name, "Port"]}
                            rules={[
                              { required: true, message: "Required" },
                            ]}
                          >
                            <InputNumber
                              placeholder="Port"
                              min={0}
                              max={65535}
                            />
                          </Form.Item>
                        </SharpTooltip>
                        <SharpTooltip title={t.syslogRandomizePorts}>
                          <Form.Item
                            {...restField}
                            name={[name, "RandomizePorts"]}
                            valuePropName="checked"
                          >
                            <Switch
                              checkedChildren="Random"
                              unCheckedChildren="Fixed"
                            />
                          </Form.Item>
                        </SharpTooltip>
                        <SharpTooltip title={t.syslogMinPort}>
                          <Form.Item
                            {...restField}
                            name={[name, "MinimumPort"]}
                          >
                            <InputNumber
                              placeholder="Min Port"
                              min={0}
                              max={65535}
                            />
                          </Form.Item>
                        </SharpTooltip>
                        <SharpTooltip title={t.syslogMaxPort}>
                          <Form.Item
                            {...restField}
                            name={[name, "MaximumPort"]}
                          >
                            <InputNumber
                              placeholder="Max Port"
                              min={0}
                              max={65535}
                            />
                          </Form.Item>
                        </SharpTooltip>
                        <SharpTooltip title="Remove this syslog server">
                          <MinusCircleOutlined
                            onClick={() => remove(name)}
                            style={{ color: "#ef4444" }}
                          />
                        </SharpTooltip>
                        {idx === fields.length - 1 && (
                          <SharpTooltip title="Add another syslog server">
                            <Button
                              type="text"
                              shape="circle"
                              size="small"
                              icon={<PlusOutlined />}
                              onClick={addNew}
                            />
                          </SharpTooltip>
                        )}
                      </SharpFlex>
                    ))}
                    {fields.length === 0 && (
                      <SharpTooltip title="Add a syslog server">
                        <Button
                          type="text"
                          shape="circle"
                          size="small"
                          icon={<PlusOutlined />}
                          onClick={addNew}
                        />
                      </SharpTooltip>
                    )}
                  </>
                );
              }}
            </Form.List>
          </Panel>

          {/* Storage */}
          <Panel header="Storage" key="storage">
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Temp Directory"
                name={["Storage", "TempDirectory"]}
                tooltip={t.tempDirectory}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
              <Form.Item
                label="Models Directory"
                name={["Storage", "ModelsDirectory"]}
                tooltip={t.modelsDirectory}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
            </SharpFlex>
          </Panel>

          {/* Database */}
          <Panel header={<SharpFlex gap={8} align="center">Database {restartWarning}</SharpFlex>} key="database">
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Type"
                name={["Database", "Type"]}
                tooltip={t.dbType}
                style={{ minWidth: 150 }}
              >
                <Select
                  options={[
                    { label: "SQLite", value: "Sqlite" },
                    { label: "MySQL", value: "Mysql" },
                    { label: "PostgreSQL", value: "Postgresql" },
                    { label: "SQL Server", value: "SqlServer" },
                  ]}
                />
              </Form.Item>
              <Form.Item
                label="Filename"
                name={["Database", "Filename"]}
                tooltip={t.dbFilename}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
              <Form.Item
                label="Port"
                name={["Database", "Port"]}
                tooltip={t.dbPort}
                style={{ minWidth: 120 }}
              >
                <InputNumber min={0} max={65535} />
              </Form.Item>
            </SharpFlex>
            <SharpFlex gap={24}>
              <Form.Item
                label="Require Encryption"
                name={["Database", "RequireEncryption"]}
                tooltip={t.dbRequireEncryption}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Debug Queries"
                name={["Database", "Debug", "EnableForQueries"]}
                tooltip={t.dbDebugQueries}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Debug Results"
                name={["Database", "Debug", "EnableForResults"]}
                tooltip={t.dbDebugResults}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </SharpFlex>
          </Panel>

          {/* HuggingFace */}
          <Panel header="HuggingFace" key="huggingface">
            <Form.Item
              label="API Key"
              name={["HuggingFace", "ApiKey"]}
              tooltip={t.hfApiKey}
              style={{ maxWidth: 500 }}
            >
              <Input.Password />
            </Form.Item>
          </Panel>

          {/* REST Server */}
          <Panel header={<SharpFlex gap={8} align="center">REST Server {restartWarning}</SharpFlex>} key="rest">
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Hostname"
                name={["Rest", "Hostname"]}
                tooltip={t.restHostname}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input />
              </Form.Item>
              <Form.Item
                label="Port"
                name={["Rest", "Port"]}
                tooltip={t.restPort}
                style={{ minWidth: 120 }}
              >
                <InputNumber min={0} max={65535} />
              </Form.Item>
            </SharpFlex>

            <SharpText style={{ fontWeight: 600, marginTop: 12, marginBottom: 8, display: "block" }}>
              IO Settings
            </SharpText>
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Stream Buffer Size"
                name={["Rest", "IO", "StreamBufferSize"]}
                tooltip={t.restStreamBuffer}
              >
                <InputNumber min={0} />
              </Form.Item>
              <Form.Item
                label="Max Requests"
                name={["Rest", "IO", "MaxRequests"]}
                tooltip={t.restMaxRequests}
              >
                <InputNumber min={0} />
              </Form.Item>
              <Form.Item
                label="Read Timeout (ms)"
                name={["Rest", "IO", "ReadTimeoutMs"]}
                tooltip={t.restReadTimeout}
              >
                <InputNumber min={0} />
              </Form.Item>
              <Form.Item
                label="Max Header Size"
                name={["Rest", "IO", "MaxIncomingHeadersSize"]}
                tooltip={t.restMaxHeaders}
              >
                <InputNumber min={0} />
              </Form.Item>
              <Form.Item
                label="Keep-Alive"
                name={["Rest", "IO", "EnableKeepAlive"]}
                tooltip={t.restKeepAlive}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </SharpFlex>

            <SharpText style={{ fontWeight: 600, marginTop: 12, marginBottom: 8, display: "block" }}>
              SSL
            </SharpText>
            <SharpFlex gap={24}>
              <Form.Item
                label="Enable SSL"
                name={["Rest", "Ssl", "Enable"]}
                tooltip={t.restSslEnable}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Mutual Auth"
                name={["Rest", "Ssl", "MutuallyAuthenticate"]}
                tooltip={t.restSslMutual}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Accept Invalid Certs"
                name={["Rest", "Ssl", "AcceptInvalidAcertificates"]}
                tooltip={t.restSslAcceptInvalid}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </SharpFlex>
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="PFX Certificate File"
                name={["Rest", "Ssl", "PfxCertificateFile"]}
                tooltip={t.restSslPfxFile}
                style={{ flex: 1, minWidth: 240 }}
              >
                <Input placeholder="e.g. certs/server.pfx" allowClear />
              </Form.Item>
              <Form.Item
                label="PFX Password"
                name={["Rest", "Ssl", "PfxCertificatePassword"]}
                tooltip={t.restSslPfxPassword}
                style={{ flex: 1, minWidth: 240 }}
              >
                <Input.Password placeholder="PFX file password" allowClear />
              </Form.Item>
            </SharpFlex>

            <SharpText style={{ fontWeight: 600, marginTop: 12, marginBottom: 8, display: "block" }}>
              Debug
            </SharpText>
            <SharpFlex gap={24}>
              <Form.Item
                label="Access Control"
                name={["Rest", "Debug", "AccessControl"]}
                tooltip={t.restDebugAccessControl}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Routing"
                name={["Rest", "Debug", "Routing"]}
                tooltip={t.restDebugRouting}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Requests"
                name={["Rest", "Debug", "Requests"]}
                tooltip={t.restDebugRequests}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label="Responses"
                name={["Rest", "Debug", "Responses"]}
                tooltip={t.restDebugResponses}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </SharpFlex>
          </Panel>

          {/* Runtime */}
          <Panel header="Runtime" key="runtime">
            <SharpFlex gap={16} wrap>
              <Form.Item
                label="Force Backend"
                name={["Runtime", "ForceBackend"]}
                tooltip={t.runtimeForceBackend}
                style={{ minWidth: 200 }}
              >
                <Select
                  options={[
                    { label: "Auto-detect", value: "auto" },
                    { label: "CPU", value: "cpu" },
                    { label: "CUDA (GPU)", value: "cuda" },
                    { label: "Metal (Apple GPU)", value: "metal" },
                  ]}
                />
              </Form.Item>
              <Form.Item
                label="CPU Backend Path"
                name={["Runtime", "CpuBackendPath"]}
                tooltip={t.runtimeCpuBackendPath}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input placeholder="Auto-detect" allowClear />
              </Form.Item>
              <Form.Item
                label="GPU Backend Path"
                name={["Runtime", "GpuBackendPath"]}
                tooltip={t.runtimeGpuBackendPath}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input placeholder="Auto-detect" allowClear />
              </Form.Item>
              <Form.Item
                label="Metal Backend Path"
                name={["Runtime", "MetalBackendPath"]}
                tooltip={t.runtimeMetalBackendPath}
                style={{ flex: 1, minWidth: 200 }}
              >
                <Input placeholder="Auto-detect" allowClear />
              </Form.Item>
            </SharpFlex>
            <Form.Item
              label="Enable Native Logging"
              name={["Runtime", "EnableNativeLogging"]}
              tooltip={t.runtimeNativeLogging}
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
          </Panel>

          {/* Debug */}
          <Panel header="Debug" key="debug">
            <Form.Item
              label="Log Request Body"
              name={["Debug", "RequestBody"]}
              tooltip={t.debugRequestBody}
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
          </Panel>

          {/* Quantization Priority */}
          <Panel header="Quantization Priority" key="quantization">
            <SharpText style={{ fontSize: 13, color: "var(--ant-color-text-secondary)", marginBottom: 12, display: "block" }}>
              {t.quantizationPriority}
            </SharpText>
            <Form.List name="QuantizationPriorityEntries">
              {(fields, { add, remove }) => (
                <>
                  {fields.map(({ key, name, ...restField }) => (
                    <SharpFlex key={key} gap={8} align="baseline" style={{ marginBottom: 8 }}>
                      <SharpTooltip title={t.quantizationKey}>
                        <Form.Item
                          {...restField}
                          name={[name, "key"]}
                          rules={[{ required: true, message: "Key required" }]}
                          style={{ flex: 1 }}
                        >
                          <Input placeholder="Quantization type (e.g., Q4_K_M)" />
                        </Form.Item>
                      </SharpTooltip>
                      <SharpTooltip title={t.quantizationValue}>
                        <Form.Item
                          {...restField}
                          name={[name, "value"]}
                          rules={[{ required: true, message: "Priority required" }]}
                        >
                          <InputNumber placeholder="Priority" />
                        </Form.Item>
                      </SharpTooltip>
                      <SharpTooltip title="Remove this entry">
                        <MinusCircleOutlined
                          onClick={() => remove(name)}
                          style={{ color: "#ef4444" }}
                        />
                      </SharpTooltip>
                    </SharpFlex>
                  ))}
                  <SharpTooltip title="Add a new quantization priority entry">
                    <Button
                      type="dashed"
                      onClick={() => add({ key: "", value: 0 })}
                      icon={<PlusOutlined />}
                      style={{ width: "100%" }}
                    >
                      Add Entry
                    </Button>
                  </SharpTooltip>
                </>
              )}
            </Form.List>
          </Panel>
        </Collapse>
      </Form>
    </PageContainer>
  );
};

export default ConfigurationPage;
