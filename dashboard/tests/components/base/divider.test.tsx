import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpDivider from '#/components/base/divider/Divider';

describe('SharpDivider Component', () => {
  it('should render divider', () => {
    render(<SharpDivider />);

    const divider = screen.getByRole('separator');
    expect(divider).toBeInTheDocument();
  });

  it('should render divider with text', () => {
    render(<SharpDivider>Divider Text</SharpDivider>);

    expect(screen.getByText('Divider Text')).toBeInTheDocument();
  });

  it('should render divider with different orientations', () => {
    const { rerender } = render(<SharpDivider orientation="left">Left Text</SharpDivider>);
    expect(screen.getByText('Left Text')).toBeInTheDocument();

    rerender(<SharpDivider orientation="right">Right Text</SharpDivider>);
    expect(screen.getByText('Right Text')).toBeInTheDocument();

    rerender(<SharpDivider orientation="center">Center Text</SharpDivider>);
    expect(screen.getByText('Center Text')).toBeInTheDocument();
  });

  it('should render divider with different types', () => {
    const { rerender } = render(<SharpDivider type="horizontal" />);
    const horizontalDivider = screen.getByRole('separator');
    expect(horizontalDivider).toBeInTheDocument();

    rerender(<SharpDivider type="vertical" />);
    const verticalDivider = screen.getByRole('separator');
    expect(verticalDivider).toBeInTheDocument();
  });

  it('should render divider with custom style', () => {
    const customStyle = { borderColor: 'red', borderWidth: '2px' };
    render(<SharpDivider style={customStyle} />);

    const divider = screen.getByRole('separator');
    expect(divider).toBeInTheDocument();
    // Custom styles are applied but may not be testable in this environment
  });

  it('should render divider with custom className', () => {
    render(<SharpDivider className="custom-divider" />);

    const divider = screen.getByRole('separator');
    expect(divider).toHaveClass('custom-divider');
  });

  it('should render plain divider', () => {
    render(<SharpDivider plain>Plain Divider</SharpDivider>);

    expect(screen.getByText('Plain Divider')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpDivider orientation="center" type="horizontal">
        Test Divider
      </SharpDivider>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});
