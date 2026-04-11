import { CopyOutlined } from "@ant-design/icons";
import { message } from "antd";
import SharpFlex from "../base/flex/Flex";
import SharpText from "../base/typograpghy/Text";
import SharpButton from "../base/button/Button";

const CopyText = ({
  text,
  className,
  displayText,
}: {
  text: string;
  className?: string;
  displayText?: string;
}) => {
  return (
    <SharpFlex align="center" gap={10} className={className}>
      <SharpButton
        icon={<CopyOutlined />}
        className="p-0"
        type="link"
        onClick={() => {
          navigator.clipboard.writeText(text);
          message.success("Copied to clipboard");
        }}
      >
        {displayText}
      </SharpButton>
    </SharpFlex>
  );
};

export default CopyText;
