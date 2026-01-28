import { sharpApiUrl } from '#/constants/apiConfig';
import { http, HttpResponse } from 'msw';
import {
  mockLocalModels,
  mockEmptyModels,
  mockApiError,
  mockDeleteSuccess,
  mockPullSuccess,
  mockChatCompletionSuccess,
  mockChatCompletionStream,
  mockChatCompletionError,
} from './mockData';

export const handlers = [
  // Get local models - success
  http.get(`${sharpApiUrl}/api/tags`, () => {
    return HttpResponse.json(mockLocalModels);
  }),

  // Delete model - success
  http.delete(`${sharpApiUrl}/api/delete`, async ({ request }) => {
    const body = await request.json();
    const modelName = (body as any)?.name;

    if (!modelName) {
      return HttpResponse.json({ error: 'Model name is required' }, { status: 400 });
    }

    return HttpResponse.json(mockDeleteSuccess);
  }),

  // Pull model - success
  http.post(`${sharpApiUrl}/api/pull`, async ({ request }) => {
    const body = await request.json();
    const modelName = (body as any)?.model;

    if (!modelName) {
      return HttpResponse.json({ error: 'Model name is required' }, { status: 400 });
    }

    return HttpResponse.json(mockPullSuccess);
  }),

  // Generate embeddings - success
  http.post(`${sharpApiUrl}/api/embed`, async ({ request }) => {
    const body = await request.json();
    const { model, input } = body as any;

    if (!model || !input) {
      return HttpResponse.json({ error: 'Model and input are required' }, { status: 400 });
    }

    const inputArray = Array.isArray(input) ? input : [input];
    const embeddings = inputArray.map(
      () => Array.from({ length: 384 }, () => Math.random() * 2 - 1) // Mock 384-dimensional embeddings
    );

    return HttpResponse.json({
      model,
      embeddings,
    });
  }),

  // Validate connectivity - success
  http.post(`${sharpApiUrl}/api/validate`, () => {
    return HttpResponse.json({
      status: 'ok',
      message: 'SharpAI services are operational',
    });
  }),

  // Generate chat completions - success
  http.post(`${sharpApiUrl}/api/generate`, async ({ request }) => {
    const body = await request.json();
    const { model, prompt, stream } = body as any;

    if (!model || !prompt) {
      return HttpResponse.json({ error: 'Model and prompt are required' }, { status: 400 });
    }

    if (stream) {
      // Return streaming response
      const streamData = mockChatCompletionStream.map((item) => JSON.stringify(item)).join('\n');
      return HttpResponse.text(streamData);
    } else {
      // Return single response
      return HttpResponse.json(mockChatCompletionSuccess);
    }
  }),
];
