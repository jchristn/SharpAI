import { RequestFormatEnum } from "#/types/types";

export const requestFormatOptions = [
  { value: RequestFormatEnum.OLLAMA, label: "OLLAMA" },
  { value: RequestFormatEnum.OPENAI, label: "OPENAI" },
];
