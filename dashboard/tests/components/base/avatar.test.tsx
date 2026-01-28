import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpAvatar from '#/components/base/avatar/Avatar';

describe('SharpAvatar Component', () => {
  it('should render avatar with text', () => {
    render(<SharpAvatar>JD</SharpAvatar>);

    expect(screen.getByText('JD')).toBeInTheDocument();
  });

  it('should render avatar with image', () => {
    render(<SharpAvatar src="https://example.com/avatar.jpg" alt="User Avatar" />);

    const avatar = screen.getByAltText('User Avatar');
    expect(avatar).toBeInTheDocument();
    expect(avatar).toHaveAttribute('src', 'https://example.com/avatar.jpg');
  });

  it('should render avatar with different sizes', () => {
    const { rerender } = render(<SharpAvatar size="small">S</SharpAvatar>);
    expect(screen.getByText('S')).toBeInTheDocument();

    rerender(<SharpAvatar size="default">M</SharpAvatar>);
    expect(screen.getByText('M')).toBeInTheDocument();

    rerender(<SharpAvatar size="large">L</SharpAvatar>);
    expect(screen.getByText('L')).toBeInTheDocument();

    rerender(<SharpAvatar size={64}>64</SharpAvatar>);
    expect(screen.getByText('64')).toBeInTheDocument();
  });

  it('should render avatar with different shapes', () => {
    const { rerender } = render(<SharpAvatar shape="circle">C</SharpAvatar>);
    expect(screen.getByText('C')).toBeInTheDocument();

    rerender(<SharpAvatar shape="square">S</SharpAvatar>);
    expect(screen.getByText('S')).toBeInTheDocument();
  });

  it('should render avatar with icon', () => {
    render(<SharpAvatar icon={<span data-testid="user-icon">👤</span>} />);

    expect(screen.getByTestId('user-icon')).toBeInTheDocument();
  });

  it('should handle onError callback', () => {
    const onError = jest.fn();
    render(<SharpAvatar src="https://invalid-url.com/avatar.jpg" onError={onError} alt="Test Avatar" />);

    const avatar = screen.getByAltText('Test Avatar');
    expect(avatar).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpAvatar src="https://example.com/avatar.jpg" size="large" shape="circle" alt="User Avatar" />
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});
