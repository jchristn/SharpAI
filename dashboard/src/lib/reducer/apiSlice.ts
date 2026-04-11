import { BaseQueryFn, EndpointBuilder } from "@reduxjs/toolkit/query";
import apiSlice, {
  ApiBaseQueryArgs,
  ApiBaseQueryResponseWithMetaData,
} from "../store/rtk/rtkApiInstance";
import {
  ChatCompletionsPaylaod,
  CompletionsPaylaod,
  CompletionsResponse,
  CompletionsOpenAIResponse,
  CompletionsOpenAIPayload,
  GenerateEmbeddingsOpenAIResponse,
  GenerateEmbeddingsPaylaod,
  GenerateEmbeddingsResponse,
  SliceTags,
  ChatCompletionsOpenAIPayload,
} from "./types";
import { LocalModel, RunningModelsResponse, SharpAISettings } from "./types";
import { AxiosProgressEvent } from "axios";

const enhancedSdk = apiSlice.enhanceEndpoints({
  addTagTypes: [],
});

const apiSliceInstance = enhancedSdk.injectEndpoints({
  endpoints: (
    build: EndpointBuilder<
      BaseQueryFn<ApiBaseQueryArgs, unknown, unknown>,
      SliceTags,
      "api"
    >
  ) => ({
    validateConnectivity: build.mutation({
      query: () => ({
        url: "/",
      }),
    }),
    getLocalModels: build.query<LocalModel[], void>({
      query: () => ({
        url: "/api/tags",
      }),
    }),
    getRunningModels: build.query<RunningModelsResponse, void>({
      query: () => ({
        url: "/api/ps",
      }),
    }),
    pullModels: build.mutation<
      void,
      {
        model: string;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
        signal?: AbortSignal;
      }
    >({
      query: ({ model, onDownloadProgress, signal }) => ({
        url: "/api/pull",
        method: "POST",
        data: { model },
        onDownloadProgress,
        signal,
      }),
    }),
    getSettings: build.query<SharpAISettings, void>({
      query: () => ({
        url: "/api/settings",
      }),
    }),
    updateSettings: build.mutation<SharpAISettings, SharpAISettings>({
      query: (settings) => ({
        url: "/api/settings",
        method: "PUT",
        data: settings,
      }),
    }),
    deleteModel: build.mutation<void, { model: string }>({
      query: ({ model }) => ({
        url: "/api/delete",
        method: "DELETE",
        data: { name: model },
      }),
    }),
    generateEmbeddings: build.mutation<
      GenerateEmbeddingsResponse,
      GenerateEmbeddingsPaylaod
    >({
      query: ({ model, input }) => ({
        url: "/api/embed",
        method: "POST",
        data: { model, input },
      }),
    }),
    completions: build.mutation<
      ApiBaseQueryResponseWithMetaData<CompletionsResponse>,
      {
        data: CompletionsPaylaod;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
        signal?: AbortSignal;
      }
    >({
      query: ({ data, onDownloadProgress, signal }) => ({
        url: "/api/generate",
        method: "POST",
        data,
        onDownloadProgress,
        inclMetaData: true,
        signal,
      }),
    }),
    chatCompletions: build.mutation<
      ApiBaseQueryResponseWithMetaData<CompletionsResponse>,
      {
        data: ChatCompletionsPaylaod;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
        signal?: AbortSignal;
      }
    >({
      query: ({ data, onDownloadProgress, signal }) => ({
        url: "/api/chat",
        method: "POST",
        data,
        onDownloadProgress,
        inclMetaData: true,
        signal,
      }),
    }),
    generateEmbeddingsOpenAI: build.mutation<
      GenerateEmbeddingsOpenAIResponse,
      GenerateEmbeddingsPaylaod
    >({
      query: ({ model, input }) => ({
        url: "/v1/embeddings",
        method: "POST",
        data: { model, input },
      }),
    }),
    completionsOpenAI: build.mutation<
      ApiBaseQueryResponseWithMetaData<CompletionsOpenAIResponse>,
      {
        data: CompletionsOpenAIPayload;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
        signal?: AbortSignal;
      }
    >({
      query: ({ data, onDownloadProgress, signal }) => ({
        url: "/v1/completions",
        method: "POST",
        data,
        onDownloadProgress,
        inclMetaData: true,
        signal,
      }),
    }),
    chatCompletionsOpenAI: build.mutation<
      ApiBaseQueryResponseWithMetaData<CompletionsOpenAIResponse>,
      {
        data: ChatCompletionsOpenAIPayload;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
        signal?: AbortSignal;
      }
    >({
      query: ({ data, onDownloadProgress, signal }) => ({
        url: "/v1/chat/completions",
        method: "POST",
        data,
        onDownloadProgress,
        inclMetaData: true,
        signal,
      }),
    }),
  }),
});

export const {
  useValidateConnectivityMutation,
  useGetLocalModelsQuery,
  useGetRunningModelsQuery,
  useGetSettingsQuery,
  useUpdateSettingsMutation,
  usePullModelsMutation,
  useDeleteModelMutation,
  useGenerateEmbeddingsMutation,
  useCompletionsMutation,
  useChatCompletionsMutation,
  useGenerateEmbeddingsOpenAIMutation,
  useCompletionsOpenAIMutation,
  useChatCompletionsOpenAIMutation,
} = apiSliceInstance;
