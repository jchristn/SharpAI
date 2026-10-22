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

it('shows both axes, doubled plot height, and accessible time/count tooltips',()=>{
  const bucket={start:'2026-08-17T00:00:00Z',end:'2026-08-17T01:00:00Z',success:7,failure:2};
  const onClick=vi.fn();
  const {container}=render(<ActivityChart buckets={[bucket]} onBucketClick={onClick}/>);
  expect(container.querySelector('svg')).toHaveAttribute('height','288');
  expect(screen.getByText('Requests')).toBeVisible();
  expect(screen.getByText('Time (local)')).toBeVisible();
  const bar=screen.getByRole('button');
  fireEvent.focus(bar);
  expect(screen.getByRole('tooltip')).toHaveTextContent('7');
  expect(screen.getByRole('tooltip')).toHaveTextContent('2');
  fireEvent.keyDown(bar,{key:'Enter'});
  expect(onClick).toHaveBeenCalledWith(bucket);
});

it('uses green successes and positions hover details over the plot only while active',()=>{
 const {container}=render(<ActivityChart buckets={[{start:'2026-08-17T00:00:00Z',end:'2026-08-17T01:00:00Z',success:2,failure:0}]}/>);
 expect(screen.queryByRole('tooltip')).toBeNull();
 expect(container.querySelector('rect[fill="var(--sa-success)"]')).not.toBeNull();
 const bar=container.querySelector('g')!;
 fireEvent.mouseEnter(bar);fireEvent.mouseMove(bar,{clientX:80,clientY:40});
 expect(screen.getByRole('tooltip').style.top).toBe('52px');
 fireEvent.mouseLeave(bar);expect(screen.queryByRole('tooltip')).toBeNull();
});
