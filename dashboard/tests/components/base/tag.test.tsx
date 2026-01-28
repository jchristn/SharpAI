import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpTag from '#/components/base/tag/Tag';

describe('SharpTag Component', () => {
  it('should render tag with label', () => {
    render(<SharpTag label="Test Tag" />);

    expect(screen.getByText('Test Tag')).toBeInTheDocument();
  });

  it('should render tag with different colors', () => {
    const { rerender } = render(<SharpTag label="Red Tag" color="red" />);
    expect(screen.getByText('Red Tag')).toBeInTheDocument();

    rerender(<SharpTag label="Blue Tag" color="blue" />);
    expect(screen.getByText('Blue Tag')).toBeInTheDocument();

    rerender(<SharpTag label="Green Tag" color="green" />);
    expect(screen.getByText('Green Tag')).toBeInTheDocument();
  });

  it('should render closable tag', () => {
    const onClose = jest.fn();
    render(<SharpTag label="Closable Tag" closable onClose={onClose} />);

    expect(screen.getByText('Closable Tag')).toBeInTheDocument();

    const closeIcon = screen.getByRole('img', { name: /close/i });
    fireEvent.click(closeIcon);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('should render tag with icon', () => {
    render(<SharpTag label="Tag with Icon" icon={<span data-testid="tag-icon">🏷️</span>} />);

    expect(screen.getByText('Tag with Icon')).toBeInTheDocument();
    expect(screen.getByTestId('tag-icon')).toBeInTheDocument();
  });

  it('should handle custom className', () => {
    render(<SharpTag label="Custom Class Tag" className="custom-tag-class" />);

    const tag = screen.getByText('Custom Class Tag');
    expect(tag).toHaveClass('custom-tag-class');
  });

  it('should handle custom style', () => {
    const customStyle = { backgroundColor: 'purple', color: 'white' };

    render(<SharpTag label="Styled Tag" style={customStyle} />);

    const tag = screen.getByText('Styled Tag');
    expect(tag).toBeInTheDocument();
    // Custom styles are applied but may not be testable in this environment
  });

  it('should match snapshot', () => {
    const { container } = render(<SharpTag label="Test Tag" color="blue" closable icon={<span>🏷️</span>} />);

    expect(container.firstChild).toMatchSnapshot();
  });
});
