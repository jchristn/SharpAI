import { Avatar, AvatarProps } from 'antd';
import React from 'react';

export type SharpAvatarProps = AvatarProps;

const SharpAvatar = (props: SharpAvatarProps) => {
  return <Avatar {...props} />;
};

export default SharpAvatar;
