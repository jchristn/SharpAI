import { AdminMethods } from './implementations/AdminMethods';
import { OllamaMethods } from './implementations/OllamaMethods';
import { OpenAIMethods } from './implementations/OpenAIMethods';
import type { IAdminMethods } from './interfaces/IAdminMethods';
import type { IOllamaMethods } from './interfaces/IOllamaMethods';
import type { IOpenAIMethods } from './interfaces/IOpenAIMethods';

/**
 * Logger callback type.
 * @param level - Log level (e.g., "DEBUG", "WARN", "ERROR").
 * @param message - Log message.
 */
export type LoggerCallback = (level: string, message: string) => void;

/**
 * Optional authentication options. The server is open by default; supply a bearer token, an admin API
 * key, or an access-key/secret-key pair. A token is also set automatically by {@link IAdminMethods.login}.
 */
export interface SharpAIAuthOptions {
  apiKey?: string;
  token?: string;
  accessKey?: string;
  secretKey?: string;
}

/**
 * SharpAI SDK for interacting with SharpAI server.
 */
export class SharpAISdk {
  /**
   * Enable or disable logging of request bodies.
   */
  public logRequests: boolean = false;

  /**
   * Enable or disable logging of response bodies.
   */
  public logResponses: boolean = false;

  /**
   * Method to invoke to send log messages.
   */
  public logger: LoggerCallback | null = null;

  /**
   * Endpoint URL for the SharpAI server.
   */
  public readonly endpoint: string;

  /**
   * Timeout in milliseconds for HTTP requests.
   * Default: 300000 (5 minutes).
   */
  public timeoutMs: number = 300000;

  /**
   * Ollama API methods.
   */
  public readonly ollama: IOllamaMethods;

  /**
   * OpenAI API methods.
   */
  public readonly openAI: IOpenAIMethods;

  /**
   * Administrative methods: settings, request history, authentication, audit, and account/RBAC management.
   */
  public readonly admin: IAdminMethods;

  /**
   * Bearer session token, if any. Set automatically after {@link IAdminMethods.login}.
   */
  public token: string | null = null;

  /**
   * Administrator API key (x-api-key), if any.
   */
  public apiKey: string | null = null;

  /**
   * Credential access key (x-access-key), if any.
   */
  public accessKey: string | null = null;

  /**
   * Credential secret key (x-secret-key), if any.
   */
  public secretKey: string | null = null;

  /**
   * Initialize the SharpAI SDK.
   * @param endpoint - SharpAI server endpoint URL.
   * @param auth - Optional authentication options.
   */
  constructor(endpoint: string, auth?: SharpAIAuthOptions) {
    if (!endpoint) {
      throw new Error('Endpoint cannot be null or empty');
    }

    this.endpoint = endpoint.replace(/\/+$/, '');
    this.token = auth?.token ?? null;
    this.apiKey = auth?.apiKey ?? null;
    this.accessKey = auth?.accessKey ?? null;
    this.secretKey = auth?.secretKey ?? null;
    this.ollama = new OllamaMethods(this);
    this.openAI = new OpenAIMethods(this);
    this.admin = new AdminMethods(this);
  }

  /**
   * Build authentication headers from the configured credentials.
   * @returns Header map (possibly empty).
   */
  public authHeaders(): Record<string, string> {
    const headers: Record<string, string> = {};
    if (this.token) headers['Authorization'] = `Bearer ${this.token}`;
    if (this.apiKey) headers['x-api-key'] = this.apiKey;
    if (this.accessKey && this.secretKey) {
      headers['x-access-key'] = this.accessKey;
      headers['x-secret-key'] = this.secretKey;
    }
    return headers;
  }

  /**
   * Send a request with an arbitrary method, optional JSON body, and optional extra headers, returning a
   * typed response or null on failure. Authentication headers are added automatically.
   * @param method - HTTP method.
   * @param url - Full URL.
   * @param options - Optional body and extra headers.
   * @param signal - Optional AbortSignal.
   * @returns Deserialized response or null.
   */
  public async sendAsync<T>(
    method: string,
    url: string,
    options?: { body?: unknown; headers?: Record<string, string> },
    signal?: AbortSignal
  ): Promise<T | null> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }

    const headers: Record<string, string> = { ...this.authHeaders(), ...(options?.headers ?? {}) };
    let body: string | undefined;
    if (options?.body !== undefined && options?.body !== null) {
      headers['Content-Type'] = 'application/json';
      body = JSON.stringify(options.body);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);
      const combinedSignal = signal ? this.combineAbortSignals(signal, controller.signal) : controller.signal;

      const response = await fetch(url, { method, headers, body, signal: combinedSignal });
      clearTimeout(timeoutId);

      const responseData = await response.text();
      if (response.ok) {
        return responseData ? (JSON.parse(responseData) as T) : null;
      }
      this.log('WARN', `Non-success from ${url}: ${response.status}`);
      return null;
    } catch (error) {
      this.log('WARN', `Request to ${url} failed: ${error instanceof Error ? error.message : 'unknown error'}`);
      return null;
    }
  }

  /**
   * Log a message using the configured logger.
   * @param level - Log level.
   * @param message - Message to log.
   */
  public log(level: string, message: string): void {
    if (message && this.logger) {
      this.logger(level, message);
    }
  }

  /**
   * Send a POST request with JSON data and return typed response.
   * @param url - Full URL to send request to.
   * @param data - Object to serialize and send.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Deserialized response or null on failure.
   */
  public async postAsync<T>(
    url: string,
    data: unknown,
    signal?: AbortSignal
  ): Promise<T | null> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }
    if (data === null || data === undefined) {
      throw new Error('Data cannot be null or undefined');
    }

    const json = JSON.stringify(data);

    if (this.logRequests) {
      this.log('DEBUG', `POST request to ${url} with ${json.length} bytes`);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      const combinedSignal = signal
        ? this.combineAbortSignals(signal, controller.signal)
        : controller.signal;

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...this.authHeaders(),
        },
        body: json,
        signal: combinedSignal,
      });

      clearTimeout(timeoutId);

      const responseData = await response.text();

      if (this.logResponses) {
        this.log('DEBUG', `Response from ${url} (status ${response.status}): ${responseData}`);
      }

      if (response.ok) {
        this.log('DEBUG', `Success from ${url}: ${response.status}`);

        if (responseData) {
          this.log('DEBUG', 'Deserializing response body');
          return JSON.parse(responseData) as T;
        } else {
          this.log('DEBUG', 'Empty response body, returning null');
          return null;
        }
      } else {
        this.log('WARN', `Non-success from ${url}: ${response.status}`);
        return null;
      }
    } catch (error) {
      if (error instanceof Error) {
        this.log('WARN', `Request to ${url} failed: ${error.message}`);
      } else {
        this.log('WARN', `Request to ${url} failed`);
      }
      return null;
    }
  }

  /**
   * Send a GET request and return typed response.
   * @param url - Full URL to send request to.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Deserialized response or null on failure.
   */
  public async getAsync<T>(url: string, signal?: AbortSignal): Promise<T | null> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }

    if (this.logRequests) {
      this.log('DEBUG', `GET request to ${url}`);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      const combinedSignal = signal
        ? this.combineAbortSignals(signal, controller.signal)
        : controller.signal;

      const response = await fetch(url, {
        method: 'GET',
        headers: this.authHeaders(),
        signal: combinedSignal,
      });

      clearTimeout(timeoutId);

      const responseData = await response.text();

      if (this.logResponses) {
        this.log('DEBUG', `Response from ${url} (status ${response.status}): ${responseData}`);
      }

      if (response.ok) {
        this.log('DEBUG', `Success from ${url}: ${response.status}`);

        if (responseData) {
          this.log('DEBUG', 'Deserializing response body');
          return JSON.parse(responseData) as T;
        } else {
          this.log('DEBUG', 'Empty response body, returning null');
          return null;
        }
      } else {
        this.log('WARN', `Non-success from ${url}: ${response.status}`);
        return null;
      }
    } catch (error) {
      if (error instanceof Error) {
        this.log('WARN', `Request to ${url} failed: ${error.message}`);
      } else {
        this.log('WARN', `Request to ${url} failed`);
      }
      return null;
    }
  }

  /**
   * Send a DELETE request with JSON data and return typed response.
   * @param url - Full URL to send request to.
   * @param data - Object to serialize and send.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Deserialized response or null on failure.
   */
  public async deleteAsync<T>(
    url: string,
    data: unknown,
    signal?: AbortSignal
  ): Promise<T | null> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }
    if (data === null || data === undefined) {
      throw new Error('Data cannot be null or undefined');
    }

    const json = JSON.stringify(data);

    if (this.logRequests) {
      this.log('DEBUG', `DELETE request to ${url} with ${json.length} bytes`);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      const combinedSignal = signal
        ? this.combineAbortSignals(signal, controller.signal)
        : controller.signal;

      const response = await fetch(url, {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
          ...this.authHeaders(),
        },
        body: json,
        signal: combinedSignal,
      });

      clearTimeout(timeoutId);

      const responseData = await response.text();

      if (this.logResponses) {
        this.log('DEBUG', `Response from ${url} (status ${response.status}): ${responseData}`);
      }

      if (response.ok) {
        this.log('DEBUG', `Success from ${url}: ${response.status}`);

        if (responseData) {
          this.log('DEBUG', 'Deserializing response body');
          return JSON.parse(responseData) as T;
        } else {
          this.log('DEBUG', 'Empty response body, returning null');
          return null;
        }
      } else {
        this.log('WARN', `Non-success from ${url}: ${response.status}`);
        return null;
      }
    } catch (error) {
      if (error instanceof Error) {
        this.log('WARN', `Request to ${url} failed: ${error.message}`);
      } else {
        this.log('WARN', `Request to ${url} failed`);
      }
      return null;
    }
  }

  /**
   * Send a GET request and return raw response as string.
   * @param url - Full URL to send request to.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Raw response data as string or null on failure.
   */
  public async getRawResponse(url: string, signal?: AbortSignal): Promise<string | null> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }

    if (this.logRequests) {
      this.log('DEBUG', `GET request to ${url}`);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      const combinedSignal = signal
        ? this.combineAbortSignals(signal, controller.signal)
        : controller.signal;

      const response = await fetch(url, {
        method: 'GET',
        headers: this.authHeaders(),
        signal: combinedSignal,
      });

      clearTimeout(timeoutId);

      const responseData = await response.text();

      if (this.logResponses) {
        this.log('DEBUG', `Response from ${url} (status ${response.status}): ${responseData}`);
      }

      if (response.ok) {
        this.log('DEBUG', `Success from ${url}: ${response.status}`);
        return responseData;
      } else {
        this.log('WARN', `Non-success from ${url}: ${response.status}`);
        return null;
      }
    } catch (error) {
      if (error instanceof Error) {
        this.log('WARN', `Request to ${url} failed: ${error.message}`);
      } else {
        this.log('WARN', `Request to ${url} failed`);
      }
      return null;
    }
  }

  /**
   * Send a streaming POST request and yield deserialized objects.
   * @param url - Full URL to send request to.
   * @param data - Object to serialize and send.
   * @param signal - Optional AbortSignal for cancellation.
   * @param processLine - Optional function to process each line before deserialization.
   * @returns AsyncGenerator yielding deserialized objects.
   */
  public async *postStreamAsync<T>(
    url: string,
    data: unknown,
    signal?: AbortSignal,
    processLine?: (line: string) => string
  ): AsyncGenerator<T, void, unknown> {
    if (!url) {
      throw new Error('URL cannot be null or empty');
    }
    if (data === null || data === undefined) {
      throw new Error('Data cannot be null or undefined');
    }

    const json = JSON.stringify(data);

    if (this.logRequests) {
      this.log('DEBUG', `POST streaming request to ${url} with ${json.length} bytes`);
    }

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      const combinedSignal = signal
        ? this.combineAbortSignals(signal, controller.signal)
        : controller.signal;

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...this.authHeaders(),
        },
        body: json,
        signal: combinedSignal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        this.log('WARN', `Non-success from ${url}: ${response.status}`);
        return;
      }

      if (!response.body) {
        this.log('WARN', `No response body from ${url}`);
        return;
      }

      this.log('DEBUG', `Success from ${url}: ${response.status}, streaming response`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();

        if (done) {
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');

        // Keep the last incomplete line in the buffer
        buffer = lines.pop() ?? '';

        for (const line of lines) {
          const trimmedLine = line.trim();
          if (!trimmedLine) continue;

          // Skip SSE "[DONE]" marker
          if (trimmedLine === 'data: [DONE]' || trimmedLine === '[DONE]') {
            continue;
          }

          const processedLine = processLine ? processLine(trimmedLine) : trimmedLine;

          // Skip empty processed lines
          if (!processedLine.trim()) continue;

          try {
            const result = JSON.parse(processedLine) as T;

            if (this.logResponses) {
              this.log('DEBUG', `Parsed streaming result: ${processedLine}`);
            }

            yield result;
          } catch (parseError) {
            this.log('DEBUG', `Failed to parse JSON line: ${processedLine}`);
            if (parseError instanceof Error) {
              this.log('DEBUG', `JSON error: ${parseError.message}`);
            }
          }
        }
      }

      // Process any remaining data in the buffer
      if (buffer.trim()) {
        const processedLine = processLine ? processLine(buffer.trim()) : buffer.trim();
        if (processedLine && processedLine !== 'data: [DONE]' && processedLine !== '[DONE]') {
          try {
            const result = JSON.parse(processedLine) as T;
            yield result;
          } catch {
            this.log('DEBUG', `Failed to parse final buffer: ${processedLine}`);
          }
        }
      }
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        this.log('DEBUG', `Request to ${url} was aborted`);
      } else if (error instanceof Error) {
        this.log('WARN', `Streaming request to ${url} failed: ${error.message}`);
      } else {
        this.log('WARN', `Streaming request to ${url} failed`);
      }
    }
  }

  /**
   * Combine multiple AbortSignals into one.
   */
  private combineAbortSignals(signal1: AbortSignal, signal2: AbortSignal): AbortSignal {
    const controller = new AbortController();

    const abort = () => {
      controller.abort();
    };

    if (signal1.aborted || signal2.aborted) {
      controller.abort();
    }

    signal1.addEventListener('abort', abort, { once: true });
    signal2.addEventListener('abort', abort, { once: true });

    return controller.signal;
  }
}
