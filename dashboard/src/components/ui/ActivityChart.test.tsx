import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ActivityChart } from './ActivityChart';
import type { ActivityBucket } from './ActivityChart';

describe('ActivityChart', () => {
  const buckets: ActivityBucket[] = [
    { start: '2026-08-17T00:00:00Z', end: '2026-08-17T01:00:00Z', success: 3, failure: 1 },
    { start: '2026-08-17T01:00:00Z', end: '2026-08-17T02:00:00Z', success: 5, failure: 0 },
  ];

  it('renders an empty state with no buckets', () => {
    render(<ActivityChart buckets={[]} />);
    expect(screen.getByText('No activity in range.')).toBeInTheDocument();
  });

  it('renders a bar group per bucket and reports clicks', () => {
    const onClick = vi.fn();
    const { container } = render(<ActivityChart buckets={buckets} onBucketClick={onClick} />);
    const groups = container.querySelectorAll('svg g');
    expect(groups.length).toBe(2);
    fireEvent.click(groups[0]);
    expect(onClick).toHaveBeenCalledWith(buckets[0]);
  });
});
