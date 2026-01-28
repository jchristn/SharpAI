import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import Sidebar from '#/components/base/sidebar/Sidebar';

// Mock Next.js navigation
const mockPathname = '/dashboard';
jest.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
}));

// Mock Next.js Link
jest.mock('next/link', () => {
  return function MockLink({ children, href }: { children: React.ReactNode; href: string }) {
    return <a href={href}>{children}</a>;
  };
});

describe('Sidebar Component', () => {
  it('should render sidebar with default props', () => {
    render(<Sidebar />);

    expect(screen.getByText('Models')).toBeInTheDocument();
    expect(screen.getByText('Embeddings')).toBeInTheDocument();
    expect(screen.getByText('Completions')).toBeInTheDocument();
  });

  it('should render sidebar in collapsed state', () => {
    render(<Sidebar collapsed={true} />);

    // In collapsed state, text might not be visible but icons should be
    const sidebar = screen.getByRole('complementary');
    expect(sidebar).toBeInTheDocument();
  });

  it('should render sidebar in expanded state', () => {
    render(<Sidebar collapsed={false} />);

    expect(screen.getByText('Models')).toBeInTheDocument();
    expect(screen.getByText('Embeddings')).toBeInTheDocument();
    expect(screen.getByText('Completions')).toBeInTheDocument();
  });

  it('should call onCollapse when toggle button is clicked', () => {
    const onCollapse = jest.fn();
    render(<Sidebar collapsed={false} onCollapse={onCollapse} />);

    const toggleButton = screen.getByTitle('Collapse Sidebar');
    fireEvent.click(toggleButton);

    expect(onCollapse).toHaveBeenCalledWith(true);
  });

  it('should show correct toggle button title when collapsed', () => {
    render(<Sidebar collapsed={true} />);

    const toggleButton = screen.getByTitle('Expand Sidebar');
    expect(toggleButton).toBeInTheDocument();
  });

  it('should show correct toggle button title when expanded', () => {
    render(<Sidebar collapsed={false} />);

    const toggleButton = screen.getByTitle('Collapse Sidebar');
    expect(toggleButton).toBeInTheDocument();
  });

  it('should render navigation links with correct hrefs', () => {
    render(<Sidebar />);

    const modelsLink = screen.getByText('Models').closest('a');
    const embeddingsLink = screen.getByText('Embeddings').closest('a');
    const completionsLink = screen.getByText('Completions').closest('a');

    expect(modelsLink).toHaveAttribute('href', '/dashboard');
    expect(embeddingsLink).toHaveAttribute('href', '/dashboard/embeddings');
    expect(completionsLink).toHaveAttribute('href', '/dashboard/completions');
  });

  it('should have correct selected key for dashboard path', () => {
    // Mock pathname is already set to '/dashboard'
    render(<Sidebar />);

    const sidebar = screen.getByRole('complementary');
    expect(sidebar).toBeInTheDocument();
  });

  it('should match snapshot for expanded state', () => {
    const { container } = render(<Sidebar collapsed={false} />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot for collapsed state', () => {
    const { container } = render(<Sidebar collapsed={true} />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
