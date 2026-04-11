import React from "react";
import SharpFlex from "../flex/Flex";
import styles from "./chat.module.scss";
import { Input } from "antd";
import SharpInput from "../input/Input";
import SharpButton from "../button/Button";
import { SendOutlined, StopOutlined } from "@ant-design/icons";
import { LoadingOutlined, InfoCircleOutlined } from "@ant-design/icons";
import SharpTitle from "../typograpghy/Title";
import MarkdownRenderer from "react-markdown-renderer";
import ResponseDetailsModal from "./ResponseDetailsModal";
import { RequestFormatEnum } from "#/types/types";
import SharpTag from "../tag/Tag";
import SharpText from "../typograpghy/Text";
import SharpTooltip from "../tooltip/Tooltip";

export interface ResponseMetadata {
  body?: any;
  headers?: Record<string, string>;
  status?: {
    code: number;
    text: string;
    timeToFirstToken?: number; // in milliseconds
    totalStreamingTime?: number; // in milliseconds
  };
}

export interface Message {
  id: string;
  content: string;
  type: "user" | "assistant";
  requestType?: RequestFormatEnum;
  timestamp?: Date;
  metadata?: ResponseMetadata;
}

interface ChatProps {
  messages: Message[];
  onSendMessage: (message: string) => void;
  isLoading?: boolean;
  placeholder?: string;
  emptyStateText?: string;
  disabled?: boolean;
  onStop?: () => void;
  noteText?: string;
}

const Chat: React.FC<ChatProps> = ({
  messages,
  onSendMessage,
  isLoading = false,
  placeholder = "Type your message...",
  emptyStateText = "Start a conversation",
  disabled = false,
  onStop,
  noteText,
}) => {
  const [inputValue, setInputValue] = React.useState("");
  const messagesEndRef = React.useRef<HTMLDivElement>(null);
  const messagesContainerRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<any>(null);
  const emptyInputRef = React.useRef<any>(null);
  const wasLoadingRef = React.useRef<boolean>(false);
  const [isUserScrolling, setIsUserScrolling] = React.useState(false);
  const [modalVisible, setModalVisible] = React.useState(false);
  const [selectedMetadata, setSelectedMetadata] =
    React.useState<ResponseMetadata | null>(null);

  // When generation finishes, return focus to whichever input is mounted.
  React.useEffect(() => {
    if (wasLoadingRef.current && !isLoading) {
      const target =
        messages.length > 0 ? inputRef.current : emptyInputRef.current;
      // Slight delay to let antd re-enable the disabled attribute first
      setTimeout(() => target?.focus?.(), 0);
    }
    wasLoadingRef.current = isLoading;
  }, [isLoading, messages.length]);

  const handleInfoClick = (metadata: ResponseMetadata) => {
    setSelectedMetadata(metadata);
    setModalVisible(true);
  };

  const handleModalClose = () => {
    setModalVisible(false);
    setSelectedMetadata(null);
  };

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  const isAtBottom = () => {
    if (!messagesContainerRef.current) return true;
    const { scrollTop, scrollHeight, clientHeight } =
      messagesContainerRef.current;
    return scrollHeight - scrollTop - clientHeight < 50; // 50px threshold
  };

  const handleScroll = () => {
    if (!messagesContainerRef.current) return;

    const atBottom = isAtBottom();
    setIsUserScrolling(!atBottom);
  };

  React.useEffect(() => {
    // Only auto-scroll if user hasn't manually scrolled up
    if (!isUserScrolling) {
      scrollToBottom();
    }
  }, [messages, isLoading, isUserScrolling]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputValue.trim() && !disabled) {
      onSendMessage(inputValue);
      setInputValue("");
      // Reset scroll state when user sends a message to ensure auto-scroll
      setIsUserScrolling(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e);
    }
  };

  return (
    <div className={styles.chatContainer}>
      {messages.length === 0 ? (
        <div className={styles.emptyState}>
          <div className={styles.emptyStateContent}>
            <SharpTitle level={4} weight={500} className="mb">
              {emptyStateText}
            </SharpTitle>
            <form onSubmit={handleSubmit} className={styles.emptyStateForm}>
              <Input.TextArea
                ref={emptyInputRef}
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyPress}
                placeholder={placeholder}
                disabled={disabled || isLoading}
                className={styles.emptyStateInput}
                rows={1}
                autoFocus
              />
              <SharpButton
                loading={isLoading}
                type="primary"
                icon={<SendOutlined />}
                htmlType="submit"
                disabled={!inputValue.trim() || disabled || isLoading}
                className={styles.sendButton}
              >
                {isLoading ? "Sending..." : "Send"}
              </SharpButton>
              {noteText && (
                <SharpText fontSize={12} style={{ color: "#64748b" }}>
                  Note: {noteText}
                </SharpText>
              )}
            </form>
          </div>
        </div>
      ) : (
        <>
          <div
            className={styles.messagesContainer}
            ref={messagesContainerRef}
            onScroll={handleScroll}
          >
            {messages.map((message) => (
              <div
                key={message.id}
                className={`${styles.messageWrapper} ${
                  message.type === "user"
                    ? styles.userMessage
                    : styles.assistantMessage
                }`}
              >
                <div className={styles.messageContent}>
                  {message.type === "user" ? (
                    <div className={styles.messageText}>
                      <div>
                        <b>
                          <small>User</small>
                        </b>
                      </div>
                      {message.content}
                    </div>
                  ) : (
                    <div>
                      <div>
                        <b>
                          <small>
                            Assistant
                            {` (${
                              message.requestType === RequestFormatEnum.OPENAI
                                ? "OpenAI"
                                : "Ollama"
                            })`}
                          </small>
                        </b>
                      </div>
                      <MarkdownRenderer markdown={message.content} />
                    </div>
                  )}
                  {message.timestamp && (
                    <div
                      className={styles.messageTimestamp}
                      style={{ display: "flex", alignItems: "center", gap: 8 }}
                    >
                      {message.timestamp.toLocaleTimeString()}
                      {message.type === "assistant" && message.metadata && (
                        <InfoCircleOutlined
                          onClick={() => handleInfoClick(message.metadata!)}
                          style={{
                            color: "#1890ff",
                            cursor: "pointer",
                            fontSize: "12px",
                          }}
                          title="View response details"
                        />
                      )}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {isLoading && (
              <div
                className={`${styles.messageWrapper} ${styles.assistantMessage}`}
              >
                <div className={styles.messageContent}>
                  <div className={styles.loadingIndicator}>
                    <span className={styles.typingDots}>
                      <span />
                      <span />
                      <span />
                    </span>
                    <span>Thinking…</span>
                  </div>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>
          <form onSubmit={handleSubmit} className={styles.inputForm}>
            <SharpFlex gap={8}>
              <SharpInput
                ref={inputRef}
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyPress}
                placeholder={placeholder}
                disabled={disabled || isLoading}
              />
              {isLoading ? (
                <SharpTooltip title="Stop">
                  <SharpButton
                    type="primary"
                    icon={<StopOutlined />}
                    onClick={onStop}
                    className={styles.sendButton}
                  />
                </SharpTooltip>
              ) : (
                <SharpButton
                  loading={isLoading}
                  type="primary"
                  icon={<SendOutlined />}
                  htmlType="submit"
                  disabled={!inputValue.trim() || disabled || isLoading}
                  className={styles.sendButton}
                />
              )}
            </SharpFlex>
            <SharpText
              className="mt d-block"
              fontSize={11}
              style={{
                color: "#64748b",
                textAlign: "center",
                marginTop: 8,
              }}
            >
              AI can make mistakes. Fact check all answers.
            </SharpText>
            {noteText && (
              <SharpText
                className="mt d-block"
                fontSize={12}
                style={{ color: "#64748b" }}
              >
                Note: {noteText}
              </SharpText>
            )}
          </form>
        </>
      )}

      {/* Response Details Modal */}
      <ResponseDetailsModal
        visible={modalVisible}
        metadata={selectedMetadata}
        onClose={handleModalClose}
      />
    </div>
  );
};

export default Chat;
