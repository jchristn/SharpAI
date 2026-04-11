import { LocalModel } from "#/lib/reducer/types";

// Size calculation utilities
export const formatSize = (size: number): string => {
  const sizeInGB = (size / (1024 * 1024 * 1024)).toFixed(2);
  return `${sizeInGB} GB`;
};

export const formatSizeInMB = (size: number): string => {
  const sizeInMB = (size / (1024 * 1024)).toFixed(2);
  return `${sizeInMB} MB`;
};

export const formatSizeInKB = (size: number): string => {
  const sizeInKB = (size / 1024).toFixed(2);
  return `${sizeInKB} KB`;
};

// Date formatting utilities
export const formatDate = (date: string): string => {
  return new Date(date).toLocaleDateString();
};

export const formatDateTime = (date: string): string => {
  return new Date(date).toLocaleString();
};
// Error formatting utilities
export const formatError = (error: any): string => {
  if (error?.message) return error.message;
  if (error?.data) return JSON.stringify(error.data);
  return JSON.stringify(error);
};

export function parseJSON<T>(json: string): T | null {
  try {
    return JSON.parse(json);
  } catch (error) {
    console.log(json);
    return null;
  }
}

// Parse an NDJSON (newline-delimited JSON) body into an array of objects.
// Handles partial final lines (e.g. when streaming: the last line may be
// incomplete). Ignores any lines that fail to parse individually rather than
// discarding the entire buffer on a single bad line.
export function parseNdJson<T>(body: string): T[] {
  if (!body) return [];
  const results: T[] = [];
  const lines = body.split(/\r?\n/);
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    try {
      results.push(JSON.parse(trimmed) as T);
    } catch {
      // Partial / in-flight chunk at the tail of a streaming response — ignore.
    }
  }
  return results;
}

// Does this model support embeddings?
// Authoritative via the server-provided `capabilities.embeddings` flag, which
// the server derives from GGUF metadata (`general.architecture` and
// `general.pooling_type`). Models pulled before capability detection was wired
// up may be missing the flag — in that case we show them everywhere rather
// than hide them, so the user can see they need to be re-pulled.
export const isEmbeddingModel = (model: LocalModel): boolean => {
  if (model.capabilities) return model.capabilities.embeddings === true;
  return true;
};

// Does this model support text/chat completions?
// Same rule as `isEmbeddingModel` — server-provided and authoritative.
export const isCompletionModel = (model: LocalModel): boolean => {
  if (model.capabilities) return model.capabilities.completions === true;
  return true;
};

// Filter models to those usable on a given page.
// Embeddings page → embedding models only.
// Completions / Chat Completions page → completion-capable models only.
export const filterModelsForPage = (
  models: LocalModel[] | undefined,
  kind: "embedding" | "completion"
): LocalModel[] => {
  if (!Array.isArray(models)) return [];
  return models.filter((m) =>
    kind === "embedding" ? isEmbeddingModel(m) : isCompletionModel(m)
  );
};
