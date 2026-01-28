import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import PageLoading from '#/components/base/loading/PageLoading';

describe('PageLoading Component', () => {
  it('should render loading spinner', () => {
    render(<PageLoading />);

    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
  });

  it('should render with default message', () => {
    render(<PageLoading />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('should render with custom message', () => {
    render(<PageLoading message="Please wait..." />);

    expect(screen.getByText('Please wait...')).toBeInTheDocument();
  });

  it('should render with custom size', () => {
    render(<PageLoading size="large" />);

    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(<PageLoading className="custom-loading" />);

    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
  });

  it('should render with custom style', () => {
    render(<PageLoading style={{ color: 'blue' }} />);

    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
  });

  it('should render with different sizes', () => {
    const { rerender } = render(<PageLoading size="small" />);
    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();

    rerender(<PageLoading size="middle" />);
    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();

    rerender(<PageLoading size="large" />);
    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
  });

  it('should render without message when message is empty', () => {
    render(<PageLoading message="" />);

    expect(screen.getByRole('img', { name: /loading/i })).toBeInTheDocument();
    expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
  });

  it('should render with tip text', () => {
    render(<PageLoading message="This may take a while..." />);

    expect(screen.getByText('This may take a while...')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<PageLoading />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
