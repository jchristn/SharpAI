import React from "react";
import { LoadingOutlined } from "@ant-design/icons";
import styles from "./pageLoding.module.css";
import SharpText from "../typograpghy/Text";
import SharpFlex from "../flex/Flex";
import PageContainer from "../pageContainer/PageContainer";

const PageLoading = ({
  message = "Loading...",
  withoutWhiteBG = false,
  className,
  allignLeft = false,
}: {
  message?: string;
  withoutWhiteBG?: boolean;
  className?: string;
  allignLeft?: boolean;
}) => {
  return (
    <PageContainer withoutWhiteBG={withoutWhiteBG} className={className}>
      <SharpFlex
        justify="center"
        align={allignLeft ? "flex-start" : "center"}
        vertical
      >
        <SharpText data-testid="loading-message">{message}</SharpText>
        <LoadingOutlined className={styles.pageLoader} />
      </SharpFlex>
    </PageContainer>
  );
};

export default PageLoading;
