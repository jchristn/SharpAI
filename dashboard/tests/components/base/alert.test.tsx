import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpAlert from '#/components/base/alert/Alert';

describe('SharpAlert Component', () => {
  it('should render alert with default props', () => {
    render(<SharpAlert message="Test alert message" />);

    expect(screen.getByText('Test alert message')).toBeInTheDocument();
  });

  it('should render alert with different types', () => {
    const { rerender } = render(<SharpAlert message="Success message" type="success" />);
    expect(screen.getByText('Success message')).toBeInTheDocument();

    rerender(<SharpAlert message="Error message" type="error" />);
    expect(screen.getByText('Error message')).toBeInTheDocument();

    rerender(<SharpAlert message="Warning message" type="warning" />);
    expect(screen.getByText('Warning message')).toBeInTheDocument();

    rerender(<SharpAlert message="Info message" type="info" />);
    expect(screen.getByText('Info message')).toBeInTheDocument();
  });

  it('should render alert with description', () => {
    render(<SharpAlert message="Main message" description="This is a detailed description" />);

    expect(screen.getByText('Main message')).toBeInTheDocument();
    expect(screen.getByText('This is a detailed description')).toBeInTheDocument();
  });

  it('should render alert with showIcon', () => {
    render(<SharpAlert message="Alert with icon" showIcon />);

    expect(screen.getByText('Alert with icon')).toBeInTheDocument();
    // Check for icon presence
    const alertElement = screen.getByRole('alert');
    expect(alertElement).toBeInTheDocument();
  });

  it('should render alert with closable', () => {
    render(<SharpAlert message="Closable alert" closable />);

    expect(screen.getByText('Closable alert')).toBeInTheDocument();
    // Check for close button
    const closeButton = screen.getByRole('button', { name: /close/i });
    expect(closeButton).toBeInTheDocument();
  });

  it('should render alert with custom action', () => {
    render(<SharpAlert message="Alert with action" action={<button>Custom Action</button>} />);

    expect(screen.getByText('Alert with action')).toBeInTheDocument();
    expect(screen.getByText('Custom Action')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpAlert message="Test alert" type="success" showIcon closable />);

    expect(container.firstChild).toMatchSnapshot();
  });
});
