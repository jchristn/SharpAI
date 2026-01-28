import { LocalModel } from '#/lib/reducer/types';

export const mockLocalModels: LocalModel[] = [
  {
    name: 'llama3.2:1b',
    model: 'llama3.2:1b',
    modified_at: '2024-01-15T10:30:00Z',
    size: 1234567890,
    digest: 'sha256:abc123def456',
    details: {
      parent_model: 'llama3.2',
      format: 'gguf',
      family: 'llama',
      families: ['llama'],
      parameter_size: '1B',
      quantization_level: 'Q4_0',
    },
  },
  {
    name: 'llama3.2:3b',
    model: 'llama3.2:3b',
    modified_at: '2024-01-14T08:15:00Z',
    size: 2345678901,
    digest: 'sha256:def456ghi789',
    details: {
      parent_model: 'llama3.2',
      format: 'gguf',
      family: 'llama',
      families: ['llama'],
      parameter_size: '3B',
      quantization_level: 'Q4_0',
    },
  },
  {
    name: 'mistral:7b',
    model: 'mistral:7b',
    modified_at: '2024-01-13T14:45:00Z',
    size: 4567890123,
    digest: 'sha256:ghi789jkl012',
    details: {
      parent_model: 'mistral',
      format: 'gguf',
      family: 'mistral',
      families: ['mistral'],
      parameter_size: '7B',
      quantization_level: 'Q4_K_M',
    },
  },
  {
    name: 'codellama:13b',
    model: 'codellama:13b',
    modified_at: '2024-01-12T16:20:00Z',
    size: 7890123456,
    digest: 'sha256:jkl012mno345',
    details: {
      parent_model: 'codellama',
      format: 'gguf',
      family: 'llama',
      families: ['llama'],
      parameter_size: '13B',
      quantization_level: 'Q4_K_M',
    },
  },
];

export const mockEmptyModels: LocalModel[] = [];

export const mockApiError = {
  status: 500,
  data: {
    message: 'Internal Server Error',
    error: 'Failed to fetch models from Ollama server',
  },
};

export const mockNetworkError = {
  status: 'FETCH_ERROR',
  error: 'Network request failed',
};

export const mockDeleteSuccess = {
  success: true,
  message: 'Model deleted successfully',
};

export const mockPullSuccess = {
  success: true,
  message: 'Model pulled successfully',
};

export const mockChatCompletionSuccess = {
  model: 'llama3.2:1b',
  created_at: '2024-01-15T10:30:00Z',
  response: 'Hello! How can I help you today?',
  done: true,
};

export const mockChatCompletionStream = [
  {
    model: 'llama3.2:1b',
    created_at: '2024-01-15T10:30:00Z',
    response: 'Hello',
    done: false,
  },
  {
    model: 'llama3.2:1b',
    created_at: '2024-01-15T10:30:01Z',
    response: ' there',
    done: false,
  },
  {
    model: 'llama3.2:1b',
    created_at: '2024-01-15T10:30:02Z',
    response: '!',
    done: true,
  },
];

export const mockChatCompletionError = {
  status: 500,
  data: {
    message: 'Internal Server Error',
    error: 'Failed to generate chat completion',
  },
};
