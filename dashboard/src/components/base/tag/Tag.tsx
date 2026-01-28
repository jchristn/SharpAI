import { Tag, TagProps } from 'antd';
import React from 'react';

type SharpTagProps = TagProps & {
  label: string;
};

const SharpTag = ({ label, ...props }: SharpTagProps) => {
  return <Tag {...props}>{label}</Tag>;
};

export default SharpTag;
