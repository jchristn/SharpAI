import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import './ui.css';

export interface ActionMenuItem {
  label: string;
  onClick: () => void;
  danger?: boolean;
  disabled?: boolean;
}

export interface ActionMenuProps {
  items: ActionMenuItem[];
  label?: string;
}

/**
 * A portaled row-action menu. The menu is rendered into document.body and positioned under its trigger so
 * it never clips inside an overflow container, and it closes on outside click, Escape, scroll, or resize.
 */
export function ActionMenu({ items, label = 'Actions' }: ActionMenuProps): JSX.Element {
  const triggerRef = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState<boolean>(false);
  const [coords, setCoords] = useState<{ top: number; left: number }>({ top: 0, left: 0 });

  useEffect(() => {
    if (!open) return;

    const close = (): void => setOpen(false);
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') setOpen(false);
    };
    const onDocClick = (event: MouseEvent): void => {
      if (triggerRef.current && !triggerRef.current.contains(event.target as Node)) setOpen(false);
    };

    window.addEventListener('scroll', close, true);
    window.addEventListener('resize', close);
    window.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onDocClick);
    return () => {
      window.removeEventListener('scroll', close, true);
      window.removeEventListener('resize', close);
      window.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onDocClick);
    };
  }, [open]);

  const toggle = (): void => {
    const rect = triggerRef.current?.getBoundingClientRect();
    if (rect) setCoords({ top: rect.bottom + 4, left: Math.max(8, rect.right - 160) });
    setOpen((prev) => !prev);
  };

  return (
    <>
      <button ref={triggerRef} type="button" className="sa-btn sa-btn--ghost" onClick={toggle} aria-haspopup="menu" aria-expanded={open}>
        {label} ▾
      </button>
      {open
        ? createPortal(
            <div className="sa-menu" role="menu" style={{ top: coords.top, left: coords.left }}>
              {items.map((item) => (
                <button
                  key={item.label}
                  type="button"
                  role="menuitem"
                  className={`sa-menu__item ${item.danger ? 'sa-menu__item--danger' : ''}`}
                  disabled={item.disabled}
                  onClick={() => {
                    setOpen(false);
                    item.onClick();
                  }}
                >
                  {item.label}
                </button>
              ))}
            </div>,
            document.body,
          )
        : null}
    </>
  );
}
