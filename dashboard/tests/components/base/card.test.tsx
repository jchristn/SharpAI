import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import SharpCard from '#/components/base/card/Card';

describe('SharpCard Component', () => {
  it('should render card with title and content', () => {
    render(
      <SharpCard title="Card Title">
        <p>Card content</p>
      </SharpCard>
    );

    expect(screen.getByText('Card Title')).toBeInTheDocument();
    expect(screen.getByText('Card content')).toBeInTheDocument();
  });

  it('should render card without title', () => {
    render(
      <SharpCard>
        <p>Card content only</p>
      </SharpCard>
    );

    expect(screen.getByText('Card content only')).toBeInTheDocument();
  });

  it('should render different card sizes', () => {
    const { rerender } = render(
      <SharpCard size="small">
        <p>Small card</p>
      </SharpCard>
    );
    expect(screen.getByText('Small card')).toBeInTheDocument();

    rerender(
      <SharpCard size="default">
        <p>Default card</p>
      </SharpCard>
    );
    expect(screen.getByText('Default card')).toBeInTheDocument();
  });

  it('should render bordered card', () => {
    render(
      <SharpCard bordered>
        <p>Bordered card</p>
      </SharpCard>
    );

    expect(screen.getByText('Bordered card')).toBeInTheDocument();
  });

  it('should render hoverable card', () => {
    render(
      <SharpCard hoverable>
        <p>Hoverable card</p>
      </SharpCard>
    );

    expect(screen.getByText('Hoverable card')).toBeInTheDocument();
  });

  it('should render loading card', () => {
    render(
      <SharpCard loading>
        <p>Loading card</p>
      </SharpCard>
    );

    // Loading card shows skeleton instead of content
    expect(screen.getByRole('list')).toBeInTheDocument();
  });

  it('should render with custom className', () => {
    render(
      <SharpCard className="custom-class">
        <p>Custom card</p>
      </SharpCard>
    );

    expect(screen.getByText('Custom card')).toBeInTheDocument();
  });

  it('should render with extra actions', () => {
    render(
      <SharpCard title="Card with Actions" extra={<button>Action</button>}>
        <p>Card with extra actions</p>
      </SharpCard>
    );

    expect(screen.getByText('Card with Actions')).toBeInTheDocument();
    expect(screen.getByText('Action')).toBeInTheDocument();
    expect(screen.getByText('Card with extra actions')).toBeInTheDocument();
  });

  it('should render with cover image', () => {
    render(
      <SharpCard cover={<img alt="Cover" src="test.jpg" />}>
        <p>Card with cover</p>
      </SharpCard>
    );

    expect(screen.getByAltText('Cover')).toBeInTheDocument();
    expect(screen.getByText('Card with cover')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <SharpCard title="Snapshot Test">
        <p>Snapshot content</p>
      </SharpCard>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
