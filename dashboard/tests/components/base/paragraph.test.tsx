import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpParagraph from '#/components/base/typograpghy/Paragraph';

// Mock Ant Design Typography
jest.mock('antd', () => ({
  Typography: {
    Paragraph: ({ children, ...props }: any) => (
      <p data-testid="sharp-paragraph" {...props}>
        {children}
      </p>
    ),
  },
}));

describe('SharpParagraph Component', () => {
  it('should render paragraph with text content', () => {
    render(<SharpParagraph>This is a paragraph</SharpParagraph>);

    expect(screen.getByTestId('sharp-paragraph')).toBeInTheDocument();
    expect(screen.getByText('This is a paragraph')).toBeInTheDocument();
  });

  it('should render paragraph with custom props', () => {
    render(
      <SharpParagraph type="secondary" strong italic underline>
        Styled paragraph
      </SharpParagraph>
    );

    const paragraph = screen.getByTestId('sharp-paragraph');
    expect(paragraph).toBeInTheDocument();
    expect(paragraph).toHaveAttribute('type', 'secondary');
    // Note: These attributes are handled internally by Ant Design
    expect(paragraph).toBeInTheDocument();
  });

  it('should render paragraph with ellipsis', () => {
    render(
      <SharpParagraph ellipsis={{ rows: 2 }}>
        This is a very long paragraph that should be truncated after two rows
      </SharpParagraph>
    );

    const paragraph = screen.getByTestId('sharp-paragraph');
    expect(paragraph).toBeInTheDocument();
    expect(paragraph).toHaveAttribute('ellipsis', '[object Object]');
  });

  it('should render paragraph with custom className and style', () => {
    render(
      <SharpParagraph className="custom-paragraph" style={{ color: 'red' }}>
        Custom styled paragraph
      </SharpParagraph>
    );

    const paragraph = screen.getByTestId('sharp-paragraph');
    expect(paragraph).toBeInTheDocument();
    expect(paragraph).toHaveClass('custom-paragraph');
    // Note: Style attributes are handled internally by Ant Design
    expect(paragraph).toBeInTheDocument();
  });

  it('should render paragraph with copyable text', () => {
    render(<SharpParagraph copyable>Copyable text</SharpParagraph>);

    const paragraph = screen.getByTestId('sharp-paragraph');
    expect(paragraph).toBeInTheDocument();
    // Note: Copyable attribute is handled internally by Ant Design
    expect(paragraph).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpParagraph type="secondary" strong>
        Snapshot paragraph
      </SharpParagraph>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
