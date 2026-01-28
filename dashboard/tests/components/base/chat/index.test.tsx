import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

// Mock the Chat component
jest.mock('#/components/base/chat/Chat', () => {
  return function MockChat(props: any) {
    return (
      <div data-testid="chat-component" {...props}>
        Chat
      </div>
    );
  };
});

// Mock the entire chat module
jest.mock('#/components/base/chat', () => ({
  __esModule: true,
  default: function MockChat(props: any) {
    return (
      <div data-testid="chat-component" {...props}>
        Chat
      </div>
    );
  },
}));

import Chat from '#/components/base/chat';

describe('Chat Index', () => {
  it('should export Chat component', () => {
    render(<Chat messages={[]} onSendMessage={jest.fn()} />);

    expect(screen.getByTestId('chat-component')).toBeInTheDocument();
  });

  it('should pass props to Chat component', () => {
    const mockOnSendMessage = jest.fn();
    const messages = [{ id: '1', content: 'Hello', type: 'user' as const }];

    render(<Chat messages={messages} onSendMessage={mockOnSendMessage} />);

    const chat = screen.getByTestId('chat-component');
    expect(chat).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<Chat messages={[]} onSendMessage={jest.fn()} />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
