import {
  isRejectedWithValue,
  Middleware,
  MiddlewareAPI,
} from "@reduxjs/toolkit";
import { isNumber } from "lodash";
import { message } from "antd";

export const errorHandler = (er: any) => {
  const error = er?.payload || {};
  const endpointName = er?.meta?.arg?.endpointName;
  const serverErrorMessage = error?.data?.message || error?.data?.detail;
  if (isNumber(error?.status) || isNumber(error?.StatusCode)) {
    switch (error.status || error.StatusCode) {
      case 401:
      case 403:
        if (endpointName !== "login") {
          message.error("Invalid session, login again. ");
          break;
        }
      default:
        message.error(
          serverErrorMessage ? serverErrorMessage : "Something went wrong."
        );
    }
  }
  if (error?.data == "Network Error") {
    message.error("Network Error");
  }
};

export const rtkQueryErrorLogger: Middleware =
  (_api: MiddlewareAPI) => (next: (action: any) => any) => (action: any) => {
    // RTK Query uses `createAsyncThunk` from redux-toolkit under the hood, so we're able to utilize these matchers!
    if (isRejectedWithValue(action)) {
      errorHandler(action);
    }

    return next(action);
  };
