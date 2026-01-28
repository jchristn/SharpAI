import { Form, FormItemProps } from 'antd';
import React from 'react';
const SharpFormItem = (props: FormItemProps) => {
  const { ...rest } = props;
  return <Form.Item {...rest}></Form.Item>;
};

export default SharpFormItem;
