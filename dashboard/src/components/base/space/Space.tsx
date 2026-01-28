import { Space, SpaceProps } from 'antd';
import React from 'react';

export type SharpSpaceProps = SpaceProps & {};
const SharpSpace = (props: SharpSpaceProps) => {
  return <Space {...props} />;
};

export default SharpSpace;
