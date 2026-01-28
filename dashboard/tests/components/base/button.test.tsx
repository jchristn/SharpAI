import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpButton from '#/components/base/button/Button';

describe('SharpButton Component', () => {
  it('should render button with text', () => {
    render(<SharpButton>Click me</SharpButton>);

    expect(screen.getByRole('button')).toBeInTheDocument();
    expect(screen.getByText('Click me')).toBeInTheDocument();
  });

  it('should handle click events', () => {
    const handleClick = jest.fn();
    render(<SharpButton onClick={handleClick}>Click me</SharpButton>);

    fireEvent.click(screen.getByRole('button'));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('should render different button types', () => {
    const { rerender } = render(<SharpButton type="primary">Primary</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();

    rerender(<SharpButton type="default">Default</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();

    rerender(<SharpButton type="dashed">Dashed</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('should render different button sizes', () => {
    const { rerender } = render(<SharpButton size="small">Small</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();

    rerender(<SharpButton size="middle">Middle</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();

    rerender(<SharpButton size="large">Large</SharpButton>);
    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('should handle disabled state', () => {
    render(<SharpButton disabled>Disabled</SharpButton>);

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should handle loading state', () => {
    render(<SharpButton loading>Loading</SharpButton>);

    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(<SharpButton className="custom-class">Custom</SharpButton>);

    expect(screen.getByRole('button')).toHaveClass('custom-class');
  });

  it('should render with icon', () => {
    render(<SharpButton icon={<span data-testid="icon">Icon</span>}>With Icon</SharpButton>);

    expect(screen.getByTestId('icon')).toBeInTheDocument();
    expect(screen.getByText('With Icon')).toBeInTheDocument();
  });

  it('should render ghost button', () => {
    render(<SharpButton ghost>Ghost</SharpButton>);

    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('should render danger button', () => {
    render(<SharpButton danger>Danger</SharpButton>);

    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpButton>Snapshot Test</SharpButton>);
    expect(container.firstChild).toMatchSnapshot();
  });
});
