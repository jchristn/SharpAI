import './ui.css';

export interface FilterOption {
  label: string;
  value: string;
}

export interface FilterField {
  key: string;
  label: string;
  type: 'text' | 'select';
  options?: FilterOption[];
  placeholder?: string;
}

export interface FilterBarProps {
  fields: FilterField[];
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
  onApply?: () => void;
  onClear?: () => void;
}

/** A backend-driven filter row: labeled text/select inputs plus Apply/Clear. Emits values to the caller. */
export function FilterBar({ fields, values, onChange, onApply, onClear }: FilterBarProps): JSX.Element {
  return (
    <div className="sa-filterbar">
      {fields.map((field) => (
        <label key={field.key} className="sa-filterbar__field">
          <span className="sa-filterbar__label">{field.label}</span>
          {field.type === 'select' ? (
            <select
              value={values[field.key] ?? ''}
              onChange={(event) => onChange(field.key, event.target.value)}
            >
              <option value="">Any</option>
              {(field.options ?? []).map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          ) : (
            <input
              type="text"
              value={values[field.key] ?? ''}
              placeholder={field.placeholder}
              onChange={(event) => onChange(field.key, event.target.value)}
            />
          )}
        </label>
      ))}
      <div className="sa-filterbar__actions">
        {onApply ? (
          <button type="button" className="sa-btn sa-btn--primary" onClick={onApply}>
            Apply
          </button>
        ) : null}
        {onClear ? (
          <button type="button" className="sa-btn" onClick={onClear}>
            Clear
          </button>
        ) : null}
      </div>
    </div>
  );
}
