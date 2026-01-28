import { Dropdown, DropDownProps } from 'antd';

const SharpDropdown = (props: DropDownProps) => {
  const { children, ...rest } = props;
  return <Dropdown {...rest}>{children}</Dropdown>;
};

export default SharpDropdown;
