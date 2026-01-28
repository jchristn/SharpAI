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
import { LocalModel } from "./types";
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
    pullModels: build.mutation<
      void,
      {
        model: string;
        onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
      }
    >({
      query: ({ model, onDownloadProgress }) => ({
        url: "/api/pull",
        method: "POST",
        data: { model },
        onDownloadProgress,
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
  usePullModelsMutation,
  useDeleteModelMutation,
  useGenerateEmbeddingsMutation,
  useCompletionsMutation,
  useChatCompletionsMutation,
  useGenerateEmbeddingsOpenAIMutation,
  useCompletionsOpenAIMutation,
  useChatCompletionsOpenAIMutation,
} = apiSliceInstance;
