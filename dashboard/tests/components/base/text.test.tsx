import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpText from '#/components/base/typograpghy/Text';

describe('SharpText Component', () => {
  it('should render text content', () => {
    render(<SharpText>Hello World</SharpText>);

    expect(screen.getByText('Hello World')).toBeInTheDocument();
  });

  it('should render with different types', () => {
    const { rerender } = render(<SharpText type="secondary">Secondary</SharpText>);
    expect(screen.getByText('Secondary')).toBeInTheDocument();

    rerender(<SharpText type="success">Success</SharpText>);
    expect(screen.getByText('Success')).toBeInTheDocument();

    rerender(<SharpText type="warning">Warning</SharpText>);
    expect(screen.getByText('Warning')).toBeInTheDocument();

    rerender(<SharpText type="danger">Danger</SharpText>);
    expect(screen.getByText('Danger')).toBeInTheDocument();
  });

  it('should render with different sizes', () => {
    const { rerender } = render(<SharpText size="small">Small</SharpText>);
    expect(screen.getByText('Small')).toBeInTheDocument();

    rerender(<SharpText size="middle">Middle</SharpText>);
    expect(screen.getByText('Middle')).toBeInTheDocument();

    rerender(<SharpText size="large">Large</SharpText>);
    expect(screen.getByText('Large')).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(<SharpText className="custom-class">Custom</SharpText>);

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('should render with custom style', () => {
    render(<SharpText style={{ color: 'red' }}>Styled</SharpText>);

    expect(screen.getByText('Styled')).toBeInTheDocument();
  });

  it('should render with ellipsis', () => {
    render(<SharpText ellipsis>Long text that should be truncated</SharpText>);

    expect(screen.getByText('Long text that should be truncated')).toBeInTheDocument();
  });

  it('should render with code style', () => {
    render(<SharpText code>Code text</SharpText>);

    expect(screen.getByText('Code text')).toBeInTheDocument();
  });

  it('should render with keyboard style', () => {
    render(<SharpText keyboard>Keyboard text</SharpText>);

    expect(screen.getByText('Keyboard text')).toBeInTheDocument();
  });

  it('should render with mark style', () => {
    render(<SharpText mark>Marked text</SharpText>);

    expect(screen.getByText('Marked text')).toBeInTheDocument();
  });

  it('should render with strong style', () => {
    render(<SharpText strong>Strong text</SharpText>);

    expect(screen.getByText('Strong text')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpText>Snapshot Test</SharpText>);
    expect(container.firstChild).toMatchSnapshot();
  });
});
