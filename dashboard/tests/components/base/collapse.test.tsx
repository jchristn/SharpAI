import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpCollapse from '#/components/base/collapse/Collpase';

describe('SharpCollapse Component', () => {
  const mockItems = [
    {
      key: '1',
      label: 'Panel 1',
      children: <p>Content of Panel 1</p>,
    },
    {
      key: '2',
      label: 'Panel 2',
      children: <p>Content of Panel 2</p>,
    },
  ];

  it('should render collapse with items', () => {
    render(<SharpCollapse items={mockItems} />);

    expect(screen.getByText('Panel 1')).toBeInTheDocument();
    expect(screen.getByText('Panel 2')).toBeInTheDocument();
  });

  it('should expand panel when clicked', () => {
    render(<SharpCollapse items={mockItems} />);

    const panel1 = screen.getByText('Panel 1');
    fireEvent.click(panel1);

    expect(screen.getByText('Content of Panel 1')).toBeInTheDocument();
  });

  it('should handle multiple panels', () => {
    render(<SharpCollapse items={mockItems} />);

    const panel1 = screen.getByText('Panel 1');
    const panel2 = screen.getByText('Panel 2');

    fireEvent.click(panel1);
    fireEvent.click(panel2);

    expect(screen.getByText('Content of Panel 1')).toBeInTheDocument();
    expect(screen.getByText('Content of Panel 2')).toBeInTheDocument();
  });

  it('should handle default active keys', () => {
    render(<SharpCollapse items={mockItems} defaultActiveKey={['1']} />);

    expect(screen.getByText('Content of Panel 1')).toBeInTheDocument();
  });

  it('should handle controlled active keys', () => {
    const { rerender } = render(<SharpCollapse items={mockItems} activeKey={['1']} />);

    expect(screen.getByText('Content of Panel 1')).toBeInTheDocument();

    rerender(<SharpCollapse items={mockItems} activeKey={['2']} />);
    expect(screen.getByText('Content of Panel 2')).toBeInTheDocument();
  });

  it('should handle onChange callback', () => {
    const onChange = jest.fn();
    render(<SharpCollapse items={mockItems} onChange={onChange} />);

    const panel1 = screen.getByText('Panel 1');
    fireEvent.click(panel1);

    expect(onChange).toHaveBeenCalledWith(['1']);
  });

  it('should render with different sizes', () => {
    const { rerender } = render(<SharpCollapse items={mockItems} size="small" />);
    expect(screen.getByText('Panel 1')).toBeInTheDocument();

    rerender(<SharpCollapse items={mockItems} size="large" />);
    expect(screen.getByText('Panel 1')).toBeInTheDocument();
  });

  it('should render with ghost mode', () => {
    render(<SharpCollapse items={mockItems} ghost />);

    expect(screen.getByText('Panel 1')).toBeInTheDocument();
    expect(screen.getByText('Panel 2')).toBeInTheDocument();
  });

  it('should render with bordered', () => {
    render(<SharpCollapse items={mockItems} bordered={false} />);

    expect(screen.getByText('Panel 1')).toBeInTheDocument();
    expect(screen.getByText('Panel 2')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpCollapse items={mockItems} defaultActiveKey={['1']} size="middle" />);

    expect(container.firstChild).toMatchSnapshot();
  });
});
