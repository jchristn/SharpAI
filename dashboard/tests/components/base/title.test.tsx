import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpTitle from '#/components/base/typograpghy/Title';

describe('SharpTitle Component', () => {
  it('should render title with text', () => {
    render(<SharpTitle>Main Title</SharpTitle>);

    expect(screen.getByText('Main Title')).toBeInTheDocument();
  });

  it('should render different heading levels', () => {
    const { rerender } = render(<SharpTitle level={1}>H1 Title</SharpTitle>);
    expect(screen.getByText('H1 Title')).toBeInTheDocument();

    rerender(<SharpTitle level={2}>H2 Title</SharpTitle>);
    expect(screen.getByText('H2 Title')).toBeInTheDocument();

    rerender(<SharpTitle level={3}>H3 Title</SharpTitle>);
    expect(screen.getByText('H3 Title')).toBeInTheDocument();

    rerender(<SharpTitle level={4}>H4 Title</SharpTitle>);
    expect(screen.getByText('H4 Title')).toBeInTheDocument();

    rerender(<SharpTitle level={5}>H5 Title</SharpTitle>);
    expect(screen.getByText('H5 Title')).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(<SharpTitle className="custom-class">Custom Title</SharpTitle>);

    expect(screen.getByText('Custom Title')).toBeInTheDocument();
  });

  it('should render with custom style', () => {
    render(<SharpTitle style={{ color: 'blue' }}>Styled Title</SharpTitle>);

    expect(screen.getByText('Styled Title')).toBeInTheDocument();
  });

  it('should render with ellipsis', () => {
    render(<SharpTitle ellipsis>Very long title that should be truncated</SharpTitle>);

    expect(screen.getByText('Very long title that should be truncated')).toBeInTheDocument();
  });

  it('should render with italic style', () => {
    render(<SharpTitle italic>Italic Title</SharpTitle>);

    expect(screen.getByText('Italic Title')).toBeInTheDocument();
  });

  it('should render with underline style', () => {
    render(<SharpTitle underline>Underlined Title</SharpTitle>);

    expect(screen.getByText('Underlined Title')).toBeInTheDocument();
  });

  it('should render with strikethrough style', () => {
    render(<SharpTitle delete>Deleted Title</SharpTitle>);

    expect(screen.getByText('Deleted Title')).toBeInTheDocument();
  });

  it('should render with mark style', () => {
    render(<SharpTitle mark>Marked Title</SharpTitle>);

    expect(screen.getByText('Marked Title')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpTitle>Snapshot Test</SharpTitle>);
    expect(container.firstChild).toMatchSnapshot();
  });
});
