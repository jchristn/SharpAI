import { configureStore } from "@reduxjs/toolkit";
import rootReducer from "#/lib/store/rootReducer";
import { apiMiddleWares } from "#/lib/store/rootReducer";

const createTestStore = () => {
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware: any) =>
      getDefaultMiddleware({
        serializableCheck: {
          ignoredActions: [
            "persist/PERSIST",
            "persist/REHYDRATE",
            "api/executeQuery/pending",
            "api/executeQuery/fulfilled",
            "api/executeQuery/rejected",
            "api/executeMutation/pending",
            "api/executeMutation/fulfilled",
            "api/executeMutation/rejected",
            "api/subscriptions/unsubscribeQueryResult",
          ],
          ignoredActionsPaths: ["payload", "meta.baseQueryMeta"],
          ignoredPaths: ["api.queries", "api.mutations"],
        },
      }).concat(apiMiddleWares),
  });
};

export default createTestStore;
