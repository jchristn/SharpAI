import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpFlex from '#/components/base/flex/Flex';

describe('SharpFlex Component', () => {
  it('should render flex container with children', () => {
    render(
      <SharpFlex>
        <div>Child 1</div>
        <div>Child 2</div>
      </SharpFlex>
    );

    expect(screen.getByText('Child 1')).toBeInTheDocument();
    expect(screen.getByText('Child 2')).toBeInTheDocument();
  });

  it('should render with different directions', () => {
    const { rerender } = render(
      <SharpFlex direction="row">
        <div>Row</div>
      </SharpFlex>
    );
    expect(screen.getByText('Row')).toBeInTheDocument();

    rerender(
      <SharpFlex direction="column">
        <div>Column</div>
      </SharpFlex>
    );
    expect(screen.getByText('Column')).toBeInTheDocument();
  });

  it('should render with different alignments', () => {
    const { rerender } = render(
      <SharpFlex align="start">
        <div>Start</div>
      </SharpFlex>
    );
    expect(screen.getByText('Start')).toBeInTheDocument();

    rerender(
      <SharpFlex align="center">
        <div>Center</div>
      </SharpFlex>
    );
    expect(screen.getByText('Center')).toBeInTheDocument();

    rerender(
      <SharpFlex align="end">
        <div>End</div>
      </SharpFlex>
    );
    expect(screen.getByText('End')).toBeInTheDocument();
  });

  it('should render with different justifications', () => {
    const { rerender } = render(
      <SharpFlex justify="start">
        <div>Justify Start</div>
      </SharpFlex>
    );
    expect(screen.getByText('Justify Start')).toBeInTheDocument();

    rerender(
      <SharpFlex justify="center">
        <div>Justify Center</div>
      </SharpFlex>
    );
    expect(screen.getByText('Justify Center')).toBeInTheDocument();

    rerender(
      <SharpFlex justify="end">
        <div>Justify End</div>
      </SharpFlex>
    );
    expect(screen.getByText('Justify End')).toBeInTheDocument();
  });

  it('should render with gap', () => {
    render(
      <SharpFlex gap={16}>
        <div>Gap 16</div>
      </SharpFlex>
    );

    expect(screen.getByText('Gap 16')).toBeInTheDocument();
  });

  it('should render with wrap', () => {
    render(
      <SharpFlex wrap>
        <div>Wrap</div>
      </SharpFlex>
    );

    expect(screen.getByText('Wrap')).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(
      <SharpFlex className="custom-class">
        <div>Custom</div>
      </SharpFlex>
    );

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('should render with custom style', () => {
    render(
      <SharpFlex style={{ backgroundColor: 'red' }}>
        <div>Styled</div>
      </SharpFlex>
    );

    expect(screen.getByText('Styled')).toBeInTheDocument();
  });

  it('should render vertical flex', () => {
    render(
      <SharpFlex vertical>
        <div>Vertical</div>
      </SharpFlex>
    );

    expect(screen.getByText('Vertical')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpFlex>
        <div>Snapshot Test</div>
      </SharpFlex>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
