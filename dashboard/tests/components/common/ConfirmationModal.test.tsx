import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import ConfirmationModal from '#/components/common/ConfirmationModal';

// Mock Ant Design components
jest.mock('antd', () => ({
  Modal: ({ children, title, open, isOpen, ...props }: any) => {
    if (!open && !isOpen) return null;
    return (
      <div data-testid="confirmation-modal" {...props}>
        <div data-testid="modal-title">{title}</div>
        <div data-testid="modal-content">{children}</div>
      </div>
    );
  },
  Button: ({ children, ...props }: any) => (
    <button data-testid="modal-button" {...props}>
      {children}
    </button>
  ),
}));

// Mock Sharp components
jest.mock('#/components/base/flex/Flex', () => {
  return function MockSharpFlex({ children, ...props }: any) {
    return (
      <div data-testid="modal-flex" {...props}>
        {children}
      </div>
    );
  };
});

jest.mock('#/components/base/typograpghy/Text', () => {
  return function MockSharpText({ children, ...props }: any) {
    return (
      <p data-testid="modal-text" {...props}>
        {children}
      </p>
    );
  };
});

jest.mock('#/components/base/typograpghy/Title', () => {
  return function MockSharpTitle({ children, ...props }: any) {
    return (
      <h2 data-testid="modal-title-text" {...props}>
        {children}
      </h2>
    );
  };
});

jest.mock('#/components/base/tooltip/Tooltip', () => {
  return function MockSharpTooltip({ children, ...props }: any) {
    return (
      <div data-testid="modal-tooltip" {...props}>
        {children}
      </div>
    );
  };
});

describe('ConfirmationModal Component', () => {
  const defaultProps = {
    isOpen: true,
    onClose: jest.fn(),
    onConfirm: jest.fn(),
    title: 'Confirm Action',
    message: 'Are you sure you want to proceed?',
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render confirmation modal with default props', () => {
    render(<ConfirmationModal {...defaultProps} />);

    // Just check if the component renders something
    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with custom title and message', () => {
    render(<ConfirmationModal {...defaultProps} title="Delete Item" message="This action cannot be undone" />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with custom button texts', () => {
    render(<ConfirmationModal {...defaultProps} confirmText="Yes, Delete" cancelText="Keep It" />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with danger type', () => {
    render(<ConfirmationModal {...defaultProps} type="danger" title="Dangerous Action" />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with warning type', () => {
    render(<ConfirmationModal {...defaultProps} type="warning" title="Warning" />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with info type', () => {
    render(<ConfirmationModal {...defaultProps} type="info" title="Information" />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should render confirmation modal with loading state', () => {
    render(<ConfirmationModal {...defaultProps} isLoading={true} confirmText="Processing..." />);

    expect(screen.getByTestId('confirmation-modal')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <ConfirmationModal {...defaultProps} type="danger" title="Delete Confirmation" message="Are you sure?" />
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
