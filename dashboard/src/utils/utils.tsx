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
