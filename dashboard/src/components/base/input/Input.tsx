import React, { LegacyRef } from 'react';
import { Input } from 'antd';
import { InputProps, InputRef } from 'antd/es/input';

type SharpInputProps = InputProps;

const SharpInput = React.forwardRef((props: SharpInputProps, ref?: LegacyRef<InputRef>) => {
  const { ...rest } = props;
  return <Input ref={ref} {...rest} />;
});

SharpInput.displayName = 'SharpInput';
export default SharpInput;
