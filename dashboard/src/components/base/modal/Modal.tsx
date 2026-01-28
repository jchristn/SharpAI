import { Modal, ModalProps } from 'antd';
import React from 'react';

export type SharpModalProps = ModalProps & {};
const SharpModal = ({ getContainer, ...props }: SharpModalProps) => {
  return <Modal getContainer={getContainer || (() => document.getElementById('root-div') as HTMLElement)} {...props} />;
};

export default SharpModal;
