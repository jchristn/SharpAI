import React from "react";
import classNames from "classnames";
import SharpFlex from "../flex/Flex";
import SharpText from "../typograpghy/Text";
import styles from "./pageContainer.module.scss";
import { Content } from "antd/es/layout/layout";

const PageContainer = ({
  children,
  className,
  withoutWhiteBG = false,
  id,
  pageTitle,
  pageSubtitle,
  pageTitleRightContent,
  style,
}: {
  children: React.ReactNode;
  className?: string;
  withoutWhiteBG?: boolean;
  id?: string;
  pageTitle?: React.ReactNode | string;
  pageSubtitle?: React.ReactNode | string;
  pageTitleRightContent?: React.ReactNode;
  showGraphSelector?: boolean;
  style?: React.CSSProperties;
}) => {
  return (
    <Content
      className={classNames(className, !withoutWhiteBG && styles.pageContainer)}
      id={id}
      style={style}
    >
      {(pageTitle || pageTitleRightContent) && (
        <SharpFlex
          className={styles.pageTitleContainer}
          wrap
          gap="small"
          align="center"
          justify="space-between"
        >
          <div>
            <SharpText fontSize={16} weight={600} data-testid="heading">
              {pageTitle}
            </SharpText>
            {pageSubtitle && (
              <div className={styles.pageSubtitle}>
                <SharpText
                  fontSize={12}
                  style={{ color: "var(--ant-color-text-secondary)" }}
                >
                  {pageSubtitle}
                </SharpText>
              </div>
            )}
          </div>
          {pageTitleRightContent}
        </SharpFlex>
      )}
      <div className={styles.pageContent}>{children}</div>
    </Content>
  );
};

export default PageContainer;
