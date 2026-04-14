
import React, { useEffect, useState } from "react";
import { message } from "antd";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import SharpSelect from "#/components/base/select/Select";
import SharpText from "#/components/base/typograpghy/Text";
import PageLoading from "#/components/base/loading/PageLoading";
import FallBack from "#/components/base/fallback/FallBack";
import Chat, { Message, ResponseMetadata } from "#/components/base/chat/Chat";
import {
  useGetLocalModelsQuery,
  useCompletionsMutation,
  useCompletionsOpenAIMutation,
} from "#/lib/reducer/apiSlice";
import {
  filterModelsForPage,
  formatError,
  parseJSON,
  parseNdJson,
} from "#/utils/utils";
import { completionOptions } from "./constants";
import { AxiosProgressEvent } from "axios";
import {
  CompletionsOpenAIResponse,
  CompletionsResponse,
} from "#/lib/reducer/types";
import styles from "./completion.module.scss";
import SharpButton from "#/components/base/button/Button";
import { DeleteOutlined, SettingOutlined } from "@ant-design/icons";
import SharpFormItem from "#/components/base/form/FormItem";
import ChatSettings from "./components/ChatSettings";
import SharpDivider from "#/components/base/divider/Divider";
import { RequestFormatEnum } from "#/types/types";
import { ApiBaseQueryResponseWithMetaData } from "#/lib/store/rtk/rtkApiInstance";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import { tooltips, pageDescriptions } from "#/constants/tooltips";

const ComplitionPage = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [selectedModel, setSelectedModel] = useState<string>("");
  const [sidebarCollapsed, setSidebarCollapsed] = useState<boolean>(true);
  const [currentOptions, setCurrentOptions] = useState(completionOptions);
  const [streamEnabled, setStreamEnabled] = useState<boolean>(true);
  const [requestType, setRequestType] = useState<RequestFormatEnum>(
    RequestFormatEnum.OLLAMA
  );
  const [abortController, setAbortController] =
    useState<AbortController | null>(null);

  const {
    data: localModels,
    isLoading: modelsLoading,
    isError: modelsError,
    error: modelsErrorData,
  } = useGetLocalModelsQuery(undefined, { refetchOnMountOrArgChange: true });

  const [generateChatCompletions, { isLoading }] = useCompletionsMutation();

  const [
    generateChatCompletionsOpenAI,
    { isLoading: generatingChatCompletionsOpenAI },
  ] = useCompletionsOpenAIMutation();
  const generatingChatCompletions =
    isLoading || generatingChatCompletionsOpenAI;

  // Auto-select the only completion model if exactly one exists
  useEffect(() => {
    const matches = filterModelsForPage(localModels, "completion");
    if (matches.length === 1 && !selectedModel) {
      setSelectedModel(matches[0].name);
    }
  }, [localModels, selectedModel]);
  const handleSendMessage = (userMessage: string) => {
    if (!selectedModel) {
      message.error("Please select a model first!");
      return false;
    }

    void submitMessage(userMessage);
    return true;
  };

  const submitMessage = async (userMessage: string) => {
    // Add user message
    const userMsg: Message = {
      id: Date.now().toString(),
      content: userMessage,
      type: "user",
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMsg]);

    // Create assistant message
    const assistantMsg: Message = {
      id: (Date.now() + 1).toString(),
      content: "",
      type: "assistant",
      timestamp: new Date(),
    };

    // Timing tracking
    const requestStartTime = Date.now();
    let firstTokenTime: number | null = null;
    let streamEndTime: number | null = null;
    let response:
      | ApiBaseQueryResponseWithMetaData<CompletionsOpenAIResponse>
      | ApiBaseQueryResponseWithMetaData<CompletionsResponse>
      | null = null;

    try {
      const abortController = new AbortController();
      setAbortController(abortController);
      let assistantResponse = "";
      let responseMetadata: ResponseMetadata = {};
      if (requestType === RequestFormatEnum.OPENAI) {
        response = await generateChatCompletionsOpenAI({
          data: {
            model: selectedModel,
            prompt: userMessage,
            stream: streamEnabled,
            max_tokens: currentOptions.num_predict,
            temperature: currentOptions.temperature,
            top_p: currentOptions.top_p,
            n: 1,
            presence_penalty: currentOptions.presence_penalty,
            frequency_penalty: currentOptions.frequency_penalty,
            stop: currentOptions.stop,
          },
          signal: abortController.signal,
          onDownloadProgress: streamEnabled
            ? (pe: AxiosProgressEvent) => {
                // Track first token time
                if (firstTokenTime === null) {
                  firstTokenTime = Date.now();
                }
                pe.event.currentTarget.responseText
                  ?.split("data: ")
                  .forEach((item: string) => {
                    if (item.trim()) {
                      const itemData: CompletionsOpenAIResponse | null =
                        parseJSON(item);
                      const data = itemData?.choices[0]?.text;
                      if (data) {
                        assistantResponse += data;
                        setMessages((prev) => {
                          const newMessages = [...prev];
                          const existingAssistantIndex = newMessages.findIndex(
                            (msg) => msg.id === assistantMsg.id
                          );

                          if (existingAssistantIndex >= 0) {
                            newMessages[existingAssistantIndex] = {
                              ...assistantMsg,
                              content: assistantResponse,
                              requestType: RequestFormatEnum.OPENAI,
                            };
                          } else {
                            newMessages.push({
                              ...assistantMsg,
                              content: assistantResponse,
                              requestType: RequestFormatEnum.OPENAI,
                            });
                          }

                          return newMessages;
                        });
                      }
                    }
                  });
              }
            : undefined,
        }).unwrap();
      } else {
        response = await generateChatCompletions({
          data: {
            model: selectedModel,
            prompt: userMessage,
            stream: streamEnabled,
            options: currentOptions,
          },
          signal: abortController.signal,
          onDownloadProgress: streamEnabled
            ? (pe: AxiosProgressEvent) => {
                // Track first token time
                if (firstTokenTime === null) {
                  firstTokenTime = Date.now();
                }

                const responseText =
                  (pe.event?.currentTarget as XMLHttpRequest)?.responseText ??
                  (pe.event?.target as XMLHttpRequest)?.responseText ??
                  "";
                const data = parseNdJson<CompletionsResponse>(responseText);
                assistantResponse = data.map((item) => item.response ?? "").join("");
                if (assistantResponse?.trim()) {
                  // Update the assistant message in real-time
                  setMessages((prev) => {
                    const newMessages = [...prev];
                    const existingAssistantIndex = newMessages.findIndex(
                      (msg) => msg.id === assistantMsg.id
                    );

                    if (existingAssistantIndex >= 0) {
                      newMessages[existingAssistantIndex] = {
                        ...assistantMsg,
                        content: assistantResponse,
                        requestType: RequestFormatEnum.OLLAMA,
                      };
                    } else {
                      newMessages.push({
                        ...assistantMsg,
                        content: assistantResponse,
                        requestType: RequestFormatEnum.OLLAMA,
                      });
                    }

                    return newMessages;
                  });
                }
              }
            : undefined,
        }).unwrap();
      }

      // Track stream end time
      streamEndTime = Date.now();
      // Prepare metadata
      responseMetadata = {
        body: response?.data,
        headers: response?.headers, // Headers would be available from the HTTP response wrapper if needed
        status: {
          code: response?.status || 0, // Successful completion
          text: response?.statusText || "",
          ...(streamEnabled && {
            timeToFirstToken: firstTokenTime
              ? firstTokenTime - requestStartTime
              : undefined,
            totalStreamingTime:
              firstTokenTime && streamEndTime
                ? streamEndTime - firstTokenTime
                : undefined,
          }),
        },
      };
      // Handle non-streaming response
      if (!streamEnabled && response) {
        assistantResponse =
          (response as any)?.data?.response ||
          (response as any)?.data?.choices?.[0]?.text ||
          "";

        setMessages((prev) => [
          ...prev,
          {
            ...assistantMsg,
            content: assistantResponse,
            metadata: responseMetadata,
            requestType:
              requestType === RequestFormatEnum.OPENAI
                ? RequestFormatEnum.OPENAI
                : RequestFormatEnum.OLLAMA,
          },
        ]);
      } else if (streamEnabled) {
        // Update the final streaming message with metadata
        setMessages((prev) => {
          const newMessages = [...prev];
          const existingAssistantIndex = newMessages.findIndex(
            (msg) => msg.id === assistantMsg.id
          );

          if (existingAssistantIndex >= 0) {
            newMessages[existingAssistantIndex] = {
              ...newMessages[existingAssistantIndex],
              metadata: responseMetadata,
            };
          }

          return newMessages;
        });
      }

      message.success("Response generated successfully!");
    } catch (error) {
      message.error(`Failed to generate response: ${formatError(error)}`);

      // Remove the failed assistant message
      // setMessages((prev) => prev.filter((msg) => msg.id !== assistantMsg.id));
    } finally {
      setAbortController(null);
    }
  };

  const clearChat = () => {
    setMessages([]);
  };

  const handleOptionChange = (
    key: keyof typeof completionOptions,
    value: any
  ) => {
    setCurrentOptions((prev) => ({
      ...prev,
      [key]: value,
    }));
  };

  const resetToDefaults = () => {
    setCurrentOptions(completionOptions);
  };

  const toggleSidebar = () => {
    console.log("Toggle sidebar - current state:", sidebarCollapsed);
    setSidebarCollapsed(!sidebarCollapsed);
  };

  if (modelsLoading) {
    return <PageLoading message="Loading models..." />;
  }

  if (modelsError) {
    return (
      <FallBack>
        <div style={{ textAlign: "center" }}>
          <p>Failed to load models</p>
          <p style={{ fontSize: "0.9em", color: "#666", marginTop: "8px" }}>
            {formatError(modelsErrorData)}
          </p>
        </div>
      </FallBack>
    );
  }

  const modelOptions = filterModelsForPage(localModels, "completion").map(
    (model) => ({
      value: model.name,
      label: model.name,
    })
  );

  return (
    <PageContainer
      pageTitleRightContent={
        <SharpFormItem
          label="Model"
          className="mb-0"
          tooltip={tooltips.completionsCommon.model}
        >
          <SharpSelect
            value={selectedModel}
            onChange={(value) => setSelectedModel(value as string)}
            placeholder="Select a model"
            options={modelOptions}
            style={{ width: 250 }}
            disabled={generatingChatCompletions}
          />
        </SharpFormItem>
      }
      pageSubtitle={pageDescriptions.completions}
      pageTitle={
        <div className={styles.chatTitle}>
          <SharpText className={styles.chatName}>Completion</SharpText>
          {messages.length > 0 && (
            <>
              <SharpDivider type="vertical" className="ml-sm mr-sm" />
              <SharpTooltip title={tooltips.completionsCommon.clearChat}>
                <SharpButton
                  type="link"
                  icon={<DeleteOutlined />}
                  onClick={clearChat}
                  className={styles.clearButton}
                  disabled={generatingChatCompletions}
                >
                  Clear Chat
                </SharpButton>
              </SharpTooltip>
            </>
          )}
          <SharpDivider type="vertical" className="ml-sm mr-sm" />
          <SharpTooltip title={tooltips.completionsCommon.settings}>
            <SharpButton
              type="link"
              icon={<SettingOutlined />}
              onClick={toggleSidebar}
              disabled={generatingChatCompletions}
            >
              {sidebarCollapsed ? "Show Settings" : "Hide Settings"}
            </SharpButton>
          </SharpTooltip>
        </div>
      }
      className=""
    >
      <div className={styles.chatPage}>
        <div className={styles.mainContent}>
          <Chat
            onStop={() => {
              abortController?.abort();
            }}
            messages={messages}
            onSendMessage={handleSendMessage}
            isLoading={generatingChatCompletions}
            placeholder="Type your message here..."
            emptyStateText={
              selectedModel
                ? "Start a conversation to generate completion."
                : "Select a model to start chatting"
            }
            disabled={generatingChatCompletions}
            noteText={`Add role markers ([User]/[Assistant]) to your prompts to tell the model to respond to your input rather than continue your prompt. Check documentation and best practices for your selected model for the recommended template.`}
          />
        </div>
        <ChatSettings
          currentOptions={currentOptions}
          onOptionChange={handleOptionChange}
          onResetDefaults={resetToDefaults}
          isCollapsed={sidebarCollapsed}
          onToggle={toggleSidebar}
          disabled={generatingChatCompletions}
          streamEnabled={streamEnabled}
          onStreamToggle={setStreamEnabled}
          onRequestTypeChange={setRequestType}
        />
      </div>
    </PageContainer>
  );
};

export default ComplitionPage;
