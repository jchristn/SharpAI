import { Message } from "#/components/base/chat";

export const messageToPayloadMessages = (
  messages: Message[],
  numberOfMessagesToKeep: number
) => {
  return messages.slice(-numberOfMessagesToKeep).map((message) => ({
    role: message.type === "user" ? "user" : "system",
    content: message.content,
  }));
};

export const messageToPayloadMessagesOpenAI = (
  messages: Message[],
  numberOfMessagesToKeep: number
) => {
  return messages.slice(-numberOfMessagesToKeep).map((message) => ({
    role: message.type === "user" ? "user" : "system",
    content: message.content,
    name: message.type === "user" ? "john_doe" : "system_instructor",
  }));
};
