import React from "react";
import SharpFlex from "../base/flex/Flex";
import SharpText from "../base/typograpghy/Text";
import classNames from "classnames";

const SharpLogo = ({
  height = 35,
  onlyIcon = false,
  className,
}: {
  height?: number;
  onlyIcon?: boolean;
  className?: string;
}) => {
  return (
    <>
      {onlyIcon ? (
        <img
          src="/images/sharp-logo.png"
          alt="Sharp AI"
          height={height}
          className={className}
        />
      ) : (
        <SharpFlex
          align="center"
          gap={8}
          className={classNames(className, "fade-in")}
        >
          <img src="/images/sharp-logo.png" alt="Sharp AI" height={height} />
          <SharpText fontSize={height / 1.8} weight={600}>
            SharpAI
          </SharpText>
        </SharpFlex>
      )}
    </>
  );
};

export default SharpLogo;
