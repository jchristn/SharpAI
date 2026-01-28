import { Alert, AlertProps } from "antd";
import React from "react";

export type SharpAlertPropsProps = AlertProps;

const SharpAlert = (props: SharpAlertPropsProps) => {
  return <Alert {...props} />;
};

export default SharpAlert;
