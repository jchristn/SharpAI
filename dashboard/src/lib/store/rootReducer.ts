import { UnknownAction, combineReducers } from "@reduxjs/toolkit";
import { rtkQueryErrorLogger } from "./rtkApiMiddlewear";
import apiSlice from "./rtk/rtkApiInstance";

const rootReducer = combineReducers({
  [apiSlice.reducerPath]: apiSlice.reducer,
});

export const apiMiddleWares = [rtkQueryErrorLogger, apiSlice.middleware];

const resettableRootReducer = (
  state: ReturnType<typeof rootReducer> | undefined,
  action: UnknownAction
) => {
  return rootReducer(state, action);
};

export default resettableRootReducer;
