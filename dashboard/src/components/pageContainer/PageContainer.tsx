import React from "react";
import classNames from "classnames";
import SharpFlex from "../base/flex/Flex";
import SharpText from "../base/typograpghy/Text";
import styles from "./pageContainer.module.scss";
import { Content } from "antd/es/layout/layout";

const PageContainer = ({
  children,
  className,
  withoutWhiteBG = false,
  id,
  pageTitle,
  pageTitleRightContent,
}: {
  children: React.ReactNode;
  className?: string;
  withoutWhiteBG?: boolean;
  id?: string;
  pageTitle?: React.ReactNode | string;
  pageTitleRightContent?: React.ReactNode;
  showGraphSelector?: boolean;
}) => {
  return (
    <Content
      className={classNames(className, !withoutWhiteBG && styles.pageContainer)}
      id={id}
    >
      {(pageTitle || pageTitleRightContent) && (
        <>
          <SharpFlex
            className={styles.pageTitleContainer}
            wrap
            gap="small"
            align="center"
            justify="space-between"
          >
            <SharpText fontSize={16} weight={600} data-testid="heading">
              {pageTitle}
            </SharpText>
            {pageTitleRightContent}
          </SharpFlex>
        </>
      )}
      <div className={styles.pageContent}>{children}</div>
    </Content>
  );
};

export default PageContainer;
