import './ui.css';

export interface ActivityBucket {
  start: string;
  end: string;
  success: number;
  failure: number;
}

export interface ActivityChartProps {
  buckets: ActivityBucket[];
  height?: number;
  onBucketClick?: (bucket: ActivityBucket) => void;
}

/**
 * A hand-rolled SVG activity chart (no charting library). Each bar stacks success (bottom) over failure
 * (top) and reports exact counts on hover; clicking a bar reports its bucket so callers can filter by that
 * time range. Bars stretch to fill the available width.
 */
export function ActivityChart({ buckets, height = 96, onBucketClick }: ActivityChartProps): JSX.Element {
  if (buckets.length === 0) {
    return <div className="sa-chart sa-chart--empty">No activity in range.</div>;
  }

  const unit = 10;
  const barWidth = 8;
  const viewWidth = buckets.length * unit;
  const viewHeight = 100;
  const maxTotal = Math.max(1, ...buckets.map((b) => b.success + b.failure));

  return (
    <div className="sa-chart">
      <svg
        width="100%"
        height={height}
        viewBox={`0 0 ${viewWidth} ${viewHeight}`}
        preserveAspectRatio="none"
        role="img"
        aria-label="Request activity over time"
      >
        {buckets.map((bucket, index) => {
          const total = bucket.success + bucket.failure;
          const totalHeight = (total / maxTotal) * viewHeight;
          const failureHeight = (bucket.failure / maxTotal) * viewHeight;
          const successHeight = totalHeight - failureHeight;
          const x = index * unit;
          const clickable = onBucketClick !== undefined;
          return (
            <g
              key={bucket.start}
              onClick={clickable ? () => onBucketClick(bucket) : undefined}
              style={clickable ? { cursor: 'pointer' } : undefined}
            >
              <title>{`${bucket.start}\n${bucket.success} ok, ${bucket.failure} failed`}</title>
              <rect x={x} y={viewHeight - successHeight} width={barWidth} height={successHeight} fill="var(--sa-primary)" />
              <rect x={x} y={viewHeight - totalHeight} width={barWidth} height={failureHeight} fill="var(--sa-danger)" />
            </g>
          );
        })}
      </svg>
    </div>
  );
}
