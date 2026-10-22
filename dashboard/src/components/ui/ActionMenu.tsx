import { useTranslation } from 'react-i18next';
import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import './ui.css';
import { Icon } from '../../app/Icon';

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
export function ActionMenu({ items, label }: ActionMenuProps): JSX.Element {
  const { t } = useTranslation();
  const menuLabel = label ?? t('common.actions');
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState<boolean>(false);
  const [coords, setCoords] = useState<{ top: number; left: number }>({ top: 0, left: 0 });

  useEffect(() => {
    if (!open) return;

    const close = (): void => setOpen(false);
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') { setOpen(false); triggerRef.current?.focus(); }
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const buttons = Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)') ?? []);
        const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
        buttons[(index + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length]?.focus();
      }
    };
    const onDocClick = (event: MouseEvent): void => {
      if (triggerRef.current && !triggerRef.current.contains(event.target as Node) && !menuRef.current?.contains(event.target as Node)) setOpen(false);
    };

    const reposition = (): void => {
      const rect=triggerRef.current?.getBoundingClientRect();
      if(rect) setCoords({top:Math.min(rect.bottom+4,window.innerHeight-100),left:Math.max(8,Math.min(rect.right-160,window.innerWidth-168))});
    };
    menuRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus({preventScroll:true});
    reposition();
    window.addEventListener('scroll', reposition, true);
    window.addEventListener('resize', close);
    window.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onDocClick);
    return () => {
      window.removeEventListener('scroll', reposition, true);
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
      <button ref={triggerRef} type="button" className="sa-btn sa-btn--ghost" onClick={toggle} aria-label={menuLabel} title={menuLabel} aria-haspopup="menu" aria-expanded={open}>
        <Icon name="more" />
      </button>
      {open
        ? createPortal(
            <div ref={menuRef} className="sa-menu" role="menu" aria-label={menuLabel} style={{ top: coords.top, left: coords.left }}>
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
