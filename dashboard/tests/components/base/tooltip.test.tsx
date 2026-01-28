import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpTooltip from '#/components/base/tooltip/Tooltip';

describe('SharpTooltip Component', () => {
  it('should render tooltip with title and children', () => {
    render(
      <SharpTooltip title="Tooltip content">
        <button>Hover me</button>
      </SharpTooltip>
    );

    expect(screen.getByText('Hover me')).toBeInTheDocument();
  });

  it('should render tooltip with different placements', () => {
    const { rerender } = render(
      <SharpTooltip title="Top tooltip" placement="top">
        <button>Top</button>
      </SharpTooltip>
    );
    expect(screen.getByText('Top')).toBeInTheDocument();

    rerender(
      <SharpTooltip title="Bottom tooltip" placement="bottom">
        <button>Bottom</button>
      </SharpTooltip>
    );
    expect(screen.getByText('Bottom')).toBeInTheDocument();

    rerender(
      <SharpTooltip title="Left tooltip" placement="left">
        <button>Left</button>
      </SharpTooltip>
    );
    expect(screen.getByText('Left')).toBeInTheDocument();

    rerender(
      <SharpTooltip title="Right tooltip" placement="right">
        <button>Right</button>
      </SharpTooltip>
    );
    expect(screen.getByText('Right')).toBeInTheDocument();
  });

  it('should handle mouse events', () => {
    render(
      <SharpTooltip title="Hover tooltip">
        <button>Hover me</button>
      </SharpTooltip>
    );

    const button = screen.getByText('Hover me');
    fireEvent.mouseEnter(button);
    fireEvent.mouseLeave(button);

    expect(button).toBeInTheDocument();
  });

  it('should render with custom trigger', () => {
    render(
      <SharpTooltip title="Click tooltip" trigger="click">
        <button>Click me</button>
      </SharpTooltip>
    );

    const button = screen.getByText('Click me');
    fireEvent.click(button);

    expect(button).toBeInTheDocument();
  });

  it('should handle multiple triggers', () => {
    render(
      <SharpTooltip title="Multi trigger tooltip" trigger={['hover', 'click']}>
        <button>Multi trigger</button>
      </SharpTooltip>
    );

    const button = screen.getByText('Multi trigger');
    fireEvent.mouseEnter(button);
    fireEvent.click(button);

    expect(button).toBeInTheDocument();
  });

  it('should render with custom color', () => {
    render(
      <SharpTooltip title="Colored tooltip" color="red">
        <button>Colored</button>
      </SharpTooltip>
    );

    expect(screen.getByText('Colored')).toBeInTheDocument();
  });

  it('should handle onVisibleChange callback', () => {
    const onVisibleChange = jest.fn();
    render(
      <SharpTooltip title="Callback tooltip" onVisibleChange={onVisibleChange}>
        <button>Callback</button>
      </SharpTooltip>
    );

    const button = screen.getByText('Callback');
    fireEvent.mouseEnter(button);

    expect(button).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpTooltip title="Test tooltip" placement="top">
        <button>Test Button</button>
      </SharpTooltip>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});
