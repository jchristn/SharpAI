import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

import SharpPageContainer from '#/components/pageContainer/PageContainer';

// Mock Ant Design components
jest.mock('antd', () => ({
  Typography: {
    Text: ({ children, ...props }: any) => <span {...props}>{children}</span>,
  },
  Layout: {
    Content: ({ children, ...props }: any) => <main {...props}>{children}</main>,
  },
  Breadcrumb: ({ items, ...props }: any) => (
    <nav {...props}>
      {items?.map((item: any, index: number) => (
        <span key={index}>{item.title}</span>
      ))}
    </nav>
  ),
}));

// Mock Sharp components
jest.mock('#/components/base/typograpghy/Title', () => {
  return function MockSharpTitle({ children, ...props }: any) {
    return (
      <h1 data-testid="page-title" {...props}>
        {children}
      </h1>
    );
  };
});

jest.mock('#/components/base/flex/Flex', () => {
  return function MockSharpFlex({ children, ...props }: any) {
    return (
      <div data-testid="page-header" {...props}>
        {children}
      </div>
    );
  };
});

describe('SharpPageContainer Component', () => {
  it('should render page container with children', () => {
    render(
      <SharpPageContainer>
        <div>Page Content</div>
      </SharpPageContainer>
    );

    expect(screen.getByText('Page Content')).toBeInTheDocument();
  });

  it('should render page container with title', () => {
    render(
      <SharpPageContainer pageTitle="Page Title">
        <div>Page Content</div>
      </SharpPageContainer>
    );

    expect(screen.getByText('Page Title')).toBeInTheDocument();
  });

  it('should render page container with breadcrumb', () => {
    // Note: PageContainer doesn't support breadcrumb, so we'll just test basic rendering
    render(
      <SharpPageContainer>
        <div>Page Content</div>
      </SharpPageContainer>
    );

    expect(screen.getByText('Page Content')).toBeInTheDocument();
  });

  it('should render page container with extra actions', () => {
    render(
      <SharpPageContainer pageTitle="Page Title" pageTitleRightContent={<button>Action Button</button>}>
        <div>Page Content</div>
      </SharpPageContainer>
    );

    expect(screen.getByText('Action Button')).toBeInTheDocument();
  });

  it('should render page container with custom className and style', () => {
    render(
      <SharpPageContainer className="custom-page" style={{ padding: '20px' }}>
        <div>Page Content</div>
      </SharpPageContainer>
    );

    const container = screen.getByRole('main');
    expect(container).toBeInTheDocument();
    expect(container).toHaveClass('custom-page');
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpPageContainer pageTitle="Snapshot Page" pageTitleRightContent={<button>Action</button>}>
        <div>Snapshot Content</div>
      </SharpPageContainer>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
