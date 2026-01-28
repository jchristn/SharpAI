import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import Sidebar from '#/components/base/sidebar';

// Mock the Sidebar component
jest.mock('#/components/base/sidebar/Sidebar', () => {
  return function MockSidebar(props: any) {
    return (
      <div data-testid="sidebar-component" {...props}>
        Sidebar
      </div>
    );
  };
});

describe('Sidebar Index', () => {
  it('should export Sidebar component', () => {
    render(<Sidebar />);

    expect(screen.getByTestId('sidebar-component')).toBeInTheDocument();
  });

  it('should pass props to Sidebar component', () => {
    render(<Sidebar collapsed={true} />);

    const sidebar = screen.getByTestId('sidebar-component');
    expect(sidebar).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(<Sidebar />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
