import { Button, ButtonProps } from 'antd';

interface SharpButtonProps extends ButtonProps {
  weight?: number;
}

const SharpButton = (props: SharpButtonProps) => {
  const { weight, icon, ...rest } = props;
  return <Button {...rest} icon={icon} style={{ fontWeight: weight }} />;
};

export default SharpButton;
