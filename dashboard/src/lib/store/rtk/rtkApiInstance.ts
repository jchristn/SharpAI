import { createApi } from "@reduxjs/toolkit/query/react";

import { sharpApiUrl } from "#/constants/apiConfig";
import { keepUnusedDataFor } from "#/constants/constant";
import axios, { AxiosProgressEvent } from "axios";

export const axiosInstance = axios.create({
  baseURL: sharpApiUrl,
  // onDownloadProgress: (progressEvent) => {
  //   console.log(progressEvent, "chk onDownloadProgress");
  // },
});

export const changeAxiosBaseUrl = (url: string) => {
  axiosInstance.defaults.baseURL = url;
};

export const setAuthToken = (token: string) => {
  axiosInstance.defaults.headers.common["Authorization"] = `Bearer ${token}`;
};

export interface ApiBaseQueryArgs {
  url: string;
  method?: string;
  data?: any;
  headers?: any;
  onDownloadProgress?: (progressEvent: AxiosProgressEvent) => void;
  inclMetaData?: boolean;
  signal?: AbortSignal;
}

export interface ApiBaseQueryResponseWithMetaData<T> {
  data: T;
  headers?: any;
  status?: number;
  statusText?: string;
}

export const axiosBaseQuery = async ({
  url,
  method,
  data,
  headers,
  onDownloadProgress,
  inclMetaData = false,
  signal,
}: ApiBaseQueryArgs) => {
  try {
    const response = await axiosInstance({
      url,
      method: method || "GET",
      data,
      headers,
      onDownloadProgress,
      signal,
    });
    return inclMetaData
      ? {
          data: {
            data: response.data,
            headers: response?.headers,
            status: response?.status,
            statusText: response?.statusText,
          },
        }
      : {
          data: response.data,
        };
  } catch (error: any) {
    return {
      error: error?.response?.data || error,
    };
  }
};

const apiSlice = createApi({
  reducerPath: "api",
  baseQuery: axiosBaseQuery,
  tagTypes: [],
  endpoints: () => ({}),
  keepUnusedDataFor: keepUnusedDataFor,
});

export default apiSlice;
