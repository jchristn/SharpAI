import { Card, CardProps } from 'antd';
import React from 'react';

export type SharpCardProps = CardProps & {};
const SharpCard = (props: SharpCardProps) => {
  return <Card {...props} />;
};

export default SharpCard;
