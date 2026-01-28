import React from 'react';
import { Select, SelectProps } from 'antd';

export interface OptionProps {
  value: string | number;
  label: string;
}

interface SharpSelectProps extends Omit<SelectProps<string | number | string[]>, 'options'> {
  options?: OptionProps[];
  readonly?: boolean;
}

const SharpSelect = (props: SharpSelectProps) => {
  const { options, placeholder, style, readonly, ...restProps } = props;

  return (
    <Select style={style} placeholder={placeholder} {...restProps} aria-readonly={readonly}>
      {options?.map((option: OptionProps) => (
        <Select.Option key={option.value} value={option.value}>
          {option.label}
        </Select.Option>
      ))}
    </Select>
  );
};

export default SharpSelect;
