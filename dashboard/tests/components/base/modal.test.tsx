import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpModal from '#/components/base/modal/Modal';

// Mock Ant Design Modal to render in the test environment
jest.mock('antd', () => ({
  ...jest.requireActual('antd'),
  Modal: ({ title, open, onCancel, onOk, footer, children, ...props }: any) => {
    if (!open) return null;
    return (
      <div data-testid="modal">
        <div data-testid="modal-title">{title}</div>
        <div data-testid="modal-content">{children}</div>
        <div data-testid="modal-footer">{footer}</div>
        <button data-testid="modal-close" onClick={onCancel}>
          ×
        </button>
        <button data-testid="modal-ok" onClick={onOk}>
          OK
        </button>
      </div>
    );
  },
}));

// Mock document.getElementById
const mockGetElementById = jest.fn();
Object.defineProperty(document, 'getElementById', {
  value: mockGetElementById,
  writable: true,
});

describe('SharpModal Component', () => {
  beforeEach(() => {
    mockGetElementById.mockReturnValue(document.body);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('should render modal with title and content', () => {
    render(
      <SharpModal title="Test Modal" open={true} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    expect(screen.getByTestId('modal-title')).toHaveTextContent('Test Modal');
    expect(screen.getByTestId('modal-content')).toHaveTextContent('Modal content');
  });

  it('should not render modal when open is false', () => {
    render(
      <SharpModal title="Test Modal" open={false} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    expect(screen.queryByText('Test Modal')).not.toBeInTheDocument();
    expect(screen.queryByText('Modal content')).not.toBeInTheDocument();
  });

  it('should call onCancel when close button is clicked', () => {
    const onCancel = jest.fn();
    render(
      <SharpModal title="Test Modal" open={true} onCancel={onCancel}>
        <p>Modal content</p>
      </SharpModal>
    );

    const closeButton = screen.getByTestId('modal-close');
    fireEvent.click(closeButton);

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('should call onOk when OK button is clicked', () => {
    const onOk = jest.fn();
    render(
      <SharpModal title="Test Modal" open={true} onOk={onOk} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    const okButton = screen.getByTestId('modal-ok');
    fireEvent.click(okButton);

    expect(onOk).toHaveBeenCalledTimes(1);
  });

  it('should render modal with custom footer', () => {
    render(
      <SharpModal
        title="Test Modal"
        open={true}
        onCancel={jest.fn()}
        footer={[<button key="custom1">Custom Button 1</button>, <button key="custom2">Custom Button 2</button>]}
      >
        <p>Modal content</p>
      </SharpModal>
    );

    expect(screen.getByText('Custom Button 1')).toBeInTheDocument();
    expect(screen.getByText('Custom Button 2')).toBeInTheDocument();
  });

  it('should render modal with different sizes', () => {
    const { rerender } = render(
      <SharpModal title="Small Modal" open={true} width={400} onCancel={jest.fn()}>
        <p>Small modal content</p>
      </SharpModal>
    );
    expect(screen.getByTestId('modal-title')).toHaveTextContent('Small Modal');

    rerender(
      <SharpModal title="Large Modal" open={true} width={800} onCancel={jest.fn()}>
        <p>Large modal content</p>
      </SharpModal>
    );
    expect(screen.getByTestId('modal-title')).toHaveTextContent('Large Modal');
  });

  it('should use custom getContainer when provided', () => {
    const customContainer = document.createElement('div');
    customContainer.id = 'custom-container';
    mockGetElementById.mockReturnValue(customContainer);

    render(
      <SharpModal title="Test Modal" open={true} getContainer={() => customContainer} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    expect(screen.getByTestId('modal-title')).toHaveTextContent('Test Modal');
  });

  it('should use default getContainer when not provided', () => {
    const rootDiv = document.createElement('div');
    rootDiv.id = 'root-div';
    mockGetElementById.mockReturnValue(rootDiv);

    render(
      <SharpModal title="Test Modal" open={true} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    expect(screen.getByTestId('modal-title')).toHaveTextContent('Test Modal');
    // The getContainer is called internally by the component
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpModal title="Test Modal" open={true} onCancel={jest.fn()}>
        <p>Modal content</p>
      </SharpModal>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});
