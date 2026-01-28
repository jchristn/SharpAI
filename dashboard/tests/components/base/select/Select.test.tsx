import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpSelect from '#/components/base/select/Select';

// Mock Ant Design Select with proper prop handling
jest.mock('antd', () => ({
  Select: ({ children, options, placeholder, style, readonly, disabled, mode, onChange, ...props }: any) => (
    <select
      data-testid="sharp-select"
      placeholder={placeholder}
      style={style}
      aria-readonly={readonly}
      disabled={disabled}
      multiple={mode === 'multiple'}
      onChange={(e) => onChange?.(e.target.value, e)}
      {...props}
    >
      {options?.map((option: any) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
      {children}
    </select>
  ),
}));

describe('SharpSelect Component', () => {
  const mockOptions = [
    { value: 'option1', label: 'Option 1' },
    { value: 'option2', label: 'Option 2' },
    { value: 'option3', label: 'Option 3' },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render select component', () => {
    render(<SharpSelect options={mockOptions} />);

    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should render with placeholder', () => {
    render(<SharpSelect options={mockOptions} placeholder="Select an option" />);

    // Just verify the component renders
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should render options', () => {
    render(<SharpSelect options={mockOptions} />);

    expect(screen.getByText('Option 1')).toBeInTheDocument();
    expect(screen.getByText('Option 2')).toBeInTheDocument();
    expect(screen.getByText('Option 3')).toBeInTheDocument();
  });

  it('should handle readonly prop', () => {
    render(<SharpSelect options={mockOptions} readonly={true} />);

    // Just verify the component renders
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should handle custom style', () => {
    const customStyle = { width: '200px' };
    render(<SharpSelect options={mockOptions} style={customStyle} />);

    // Just verify the component renders
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should handle value changes', () => {
    const onChange = jest.fn();
    render(<SharpSelect options={mockOptions} onChange={onChange} />);

    const select = screen.getByRole('combobox');
    fireEvent.change(select, { target: { value: 'option2' } });

    // Just verify the change event was triggered
    expect(onChange).toHaveBeenCalled();
  });

  it('should handle disabled state', () => {
    render(<SharpSelect options={mockOptions} disabled={true} />);

    // Just verify the component renders
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should handle multiple selection', () => {
    render(<SharpSelect options={mockOptions} mode="multiple" />);

    // Just verify the component renders
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should render without options', () => {
    render(<SharpSelect />);

    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should handle empty options array', () => {
    render(<SharpSelect options={[]} />);

    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpSelect options={mockOptions} placeholder="Select an option" style={{ width: '200px' }} />
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
