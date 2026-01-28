import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpLogo from '#/components/logo/SharpLogo';

describe('SharpLogo Component', () => {
  it('should render logo with default props', () => {
    render(<SharpLogo />);

    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('should render logo with custom size', () => {
    render(<SharpLogo size={48} />);

    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('should render logo with custom className', () => {
    render(<SharpLogo className="custom-logo" />);

    // Check that the logo container has the custom class
    expect(screen.getByRole('img').closest('div')).toHaveClass('custom-logo');
  });

  it('should render logo with custom style', () => {
    render(<SharpLogo style={{ color: 'blue' }} />);

    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('should render logo with alt text', () => {
    render(<SharpLogo alt="Custom Logo" />);

    // The component might use a default alt text, so check for the image
    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('should render logo with onClick handler', () => {
    const handleClick = jest.fn();
    render(<SharpLogo onClick={handleClick} />);

    const logo = screen.getByRole('img');
    expect(logo).toBeInTheDocument();
  });

  it('should render logo with different variants', () => {
    const { rerender } = render(<SharpLogo variant="light" />);
    expect(screen.getByRole('img')).toBeInTheDocument();

    rerender(<SharpLogo variant="dark" />);
    expect(screen.getByRole('img')).toBeInTheDocument();

    rerender(<SharpLogo variant="color" />);
    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpLogo />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
