import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusBadge, toneForHttpStatus } from './StatusBadge';

describe('StatusBadge', () => {
  it('renders its label so color is never the sole signal', () => {
    render(<StatusBadge tone="success">OK</StatusBadge>);
    expect(screen.getByText('OK')).toBeInTheDocument();
  });

  it('maps HTTP status codes to tones', () => {
    expect(toneForHttpStatus(200)).toBe('success');
    expect(toneForHttpStatus(404)).toBe('warning');
    expect(toneForHttpStatus(500)).toBe('danger');
    expect(toneForHttpStatus(101)).toBe('neutral');
  });
});
