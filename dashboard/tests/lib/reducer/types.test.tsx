import {
  SliceTags,
  LocalModelDetails,
  LocalModel,
  ChatCompletionOptions,
  ChatMessage,
  ChatCompletionResponse,
  PullModelRequest,
  PullModelResponse,
  EmbeddingRequest,
  EmbeddingResponse,
  ValidationRequest,
  ValidationResponse,
} from '#/lib/reducer/types';

describe('Reducer Types', () => {
  describe('SliceTags', () => {
    it('should have correct enum values', () => {
      expect(SliceTags.SHARP).toBe('sharp');
    });

    it('should be immutable', () => {
      const originalSharp = SliceTags.SHARP;
      expect(SliceTags.SHARP).toBe(originalSharp);
    });

    it('should work with Object.values', () => {
      const values = Object.values(SliceTags);
      expect(values).toContain('sharp');
      expect(values).toHaveLength(1);
    });

    it('should work with Object.entries', () => {
      const entries = Object.entries(SliceTags);
      expect(entries).toContainEqual(['SHARP', 'sharp']);
      expect(entries).toHaveLength(1);
    });
  });

  describe('LocalModelDetails', () => {
    it('should create valid LocalModelDetails object', () => {
      const details: LocalModelDetails = {
        parent_model: 'gpt-3.5-turbo',
        format: 'gguf',
        family: 'gpt',
        families: ['gpt', 'openai'],
        parameter_size: '7B',
        quantization_level: 'Q4_0',
      };

      expect(details.parent_model).toBe('gpt-3.5-turbo');
      expect(details.format).toBe('gguf');
      expect(details.family).toBe('gpt');
      expect(details.families).toEqual(['gpt', 'openai']);
      expect(details.parameter_size).toBe('7B');
      expect(details.quantization_level).toBe('Q4_0');
    });

    it('should handle empty families array', () => {
      const details: LocalModelDetails = {
        parent_model: 'test-model',
        format: 'gguf',
        family: 'test',
        families: [],
        parameter_size: '1B',
        quantization_level: 'Q8_0',
      };

      expect(details.families).toEqual([]);
    });
  });

  describe('LocalModel', () => {
    it('should create valid LocalModel object', () => {
      const model: LocalModel = {
        name: 'test-model',
        model: 'test-model-v1',
        modified_at: '2023-01-01T00:00:00Z',
        size: 1024000,
        digest: 'sha256:abc123',
        details: {
          parent_model: 'gpt-3.5-turbo',
          format: 'gguf',
          family: 'gpt',
          families: ['gpt'],
          parameter_size: '7B',
          quantization_level: 'Q4_0',
        },
      };

      expect(model.name).toBe('test-model');
      expect(model.model).toBe('test-model-v1');
      expect(model.modified_at).toBe('2023-01-01T00:00:00Z');
      expect(model.size).toBe(1024000);
      expect(model.digest).toBe('sha256:abc123');
      expect(model.details).toBeDefined();
    });
  });

  describe('ChatCompletionOptions', () => {
    it('should create valid ChatCompletionOptions object', () => {
      const options: ChatCompletionOptions = {
        model: 'gpt-3.5-turbo',
        temperature: 0.7,
        max_tokens: 1000,
        top_p: 1,
        frequency_penalty: 0,
        presence_penalty: 0,
      };

      expect(options.model).toBe('gpt-3.5-turbo');
      expect(options.temperature).toBe(0.7);
      expect(options.max_tokens).toBe(1000);
      expect(options.top_p).toBe(1);
      expect(options.frequency_penalty).toBe(0);
      expect(options.presence_penalty).toBe(0);
    });
  });

  describe('ChatMessage', () => {
    it('should create valid ChatMessage object', () => {
      const message: ChatMessage = {
        role: 'user',
        content: 'Hello, world!',
      };

      expect(message.role).toBe('user');
      expect(message.content).toBe('Hello, world!');
    });

    it('should handle assistant role', () => {
      const message: ChatMessage = {
        role: 'assistant',
        content: 'Hi there!',
      };

      expect(message.role).toBe('assistant');
      expect(message.content).toBe('Hi there!');
    });
  });

  describe('ChatCompletionResponse', () => {
    it('should create valid ChatCompletionResponse object', () => {
      const response: ChatCompletionResponse = {
        id: 'chat-123',
        object: 'chat.completion',
        created: 1234567890,
        model: 'gpt-3.5-turbo',
        choices: [
          {
            index: 0,
            message: {
              role: 'assistant',
              content: 'Hello!',
            },
            finish_reason: 'stop',
          },
        ],
        usage: {
          prompt_tokens: 10,
          completion_tokens: 5,
          total_tokens: 15,
        },
      };

      expect(response.id).toBe('chat-123');
      expect(response.object).toBe('chat.completion');
      expect(response.created).toBe(1234567890);
      expect(response.model).toBe('gpt-3.5-turbo');
      expect(response.choices).toHaveLength(1);
      expect(response.usage).toBeDefined();
    });
  });

  describe('PullModelRequest', () => {
    it('should create valid PullModelRequest object', () => {
      const request: PullModelRequest = {
        model_name: 'test-model',
      };

      expect(request.model_name).toBe('test-model');
    });
  });

  describe('PullModelResponse', () => {
    it('should create valid PullModelResponse object', () => {
      const response: PullModelResponse = {
        message: 'Model pulled successfully',
        model_name: 'test-model',
      };

      expect(response.message).toBe('Model pulled successfully');
      expect(response.model_name).toBe('test-model');
    });
  });

  describe('EmbeddingRequest', () => {
    it('should create valid EmbeddingRequest object', () => {
      const request: EmbeddingRequest = {
        model: 'embedding-model',
        input: 'Hello, world!',
      };

      expect(request.model).toBe('embedding-model');
      expect(request.input).toBe('Hello, world!');
    });
  });

  describe('EmbeddingResponse', () => {
    it('should create valid EmbeddingResponse object', () => {
      const response: EmbeddingResponse = {
        object: 'list',
        data: [
          {
            object: 'embedding',
            index: 0,
            embedding: [0.1, 0.2, 0.3],
          },
        ],
        model: 'embedding-model',
        usage: {
          prompt_tokens: 5,
          total_tokens: 5,
        },
      };

      expect(response.object).toBe('list');
      expect(response.data).toHaveLength(1);
      expect(response.model).toBe('embedding-model');
      expect(response.usage).toBeDefined();
    });
  });

  describe('ValidationRequest', () => {
    it('should create valid ValidationRequest object', () => {
      const request: ValidationRequest = {
        url: 'http://localhost:8000',
      };

      expect(request.url).toBe('http://localhost:8000');
    });
  });

  describe('ValidationResponse', () => {
    it('should create valid ValidationResponse object', () => {
      const response: ValidationResponse = {
        valid: true,
        message: 'Connection successful',
      };

      expect(response.valid).toBe(true);
      expect(response.message).toBe('Connection successful');
    });

    it('should handle invalid response', () => {
      const response: ValidationResponse = {
        valid: false,
        message: 'Connection failed',
      };

      expect(response.valid).toBe(false);
      expect(response.message).toBe('Connection failed');
    });
  });
});
