import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpSpace from '#/components/base/space/Space';

describe('SharpSpace Component', () => {
  it('should render space with children', () => {
    render(
      <SharpSpace>
        <button>Button 1</button>
        <button>Button 2</button>
      </SharpSpace>
    );

    expect(screen.getByText('Button 1')).toBeInTheDocument();
    expect(screen.getByText('Button 2')).toBeInTheDocument();
  });

  it('should render space with different directions', () => {
    const { rerender } = render(
      <SharpSpace direction="horizontal">
        <button>H1</button>
        <button>H2</button>
      </SharpSpace>
    );
    expect(screen.getByText('H1')).toBeInTheDocument();

    rerender(
      <SharpSpace direction="vertical">
        <button>V1</button>
        <button>V2</button>
      </SharpSpace>
    );
    expect(screen.getByText('V1')).toBeInTheDocument();
  });

  it('should render space with different sizes', () => {
    const { rerender } = render(
      <SharpSpace size="small">
        <button>S1</button>
        <button>S2</button>
      </SharpSpace>
    );
    expect(screen.getByText('S1')).toBeInTheDocument();

    rerender(
      <SharpSpace size="middle">
        <button>M1</button>
        <button>M2</button>
      </SharpSpace>
    );
    expect(screen.getByText('M1')).toBeInTheDocument();

    rerender(
      <SharpSpace size="large">
        <button>L1</button>
        <button>L2</button>
      </SharpSpace>
    );
    expect(screen.getByText('L1')).toBeInTheDocument();
  });

  it('should render space with custom size', () => {
    render(
      <SharpSpace size={20}>
        <button>C1</button>
        <button>C2</button>
      </SharpSpace>
    );

    expect(screen.getByText('C1')).toBeInTheDocument();
    expect(screen.getByText('C2')).toBeInTheDocument();
  });

  it('should render space with wrap', () => {
    render(
      <SharpSpace wrap>
        <button>W1</button>
        <button>W2</button>
        <button>W3</button>
      </SharpSpace>
    );

    expect(screen.getByText('W1')).toBeInTheDocument();
    expect(screen.getByText('W2')).toBeInTheDocument();
    expect(screen.getByText('W3')).toBeInTheDocument();
  });

  it('should render space with align', () => {
    render(
      <SharpSpace align="center">
        <button>A1</button>
        <button>A2</button>
      </SharpSpace>
    );

    expect(screen.getByText('A1')).toBeInTheDocument();
    expect(screen.getByText('A2')).toBeInTheDocument();
  });

  it('should render space with custom className', () => {
    render(
      <SharpSpace className="custom-space">
        <button>Class1</button>
        <button>Class2</button>
      </SharpSpace>
    );

    const spaceElement = screen.getByText('Class1').closest('.ant-space');
    expect(spaceElement).toHaveClass('custom-space');
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpSpace direction="horizontal" size="middle" align="center">
        <button>Button 1</button>
        <button>Button 2</button>
      </SharpSpace>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});
