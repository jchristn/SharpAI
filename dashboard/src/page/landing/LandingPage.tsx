"use client";

import React, { useEffect, useState } from "react";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import styles from "./style.module.scss";
import SharpLogo from "#/components/logo/SharpLogo";
import SharpFlex from "#/components/base/flex/Flex";
import { useValidateConnectivityMutation } from "#/lib/reducer/apiSlice";
import { useRouter } from "next/navigation";
import Link from "next/link";
import SharpText from "#/components/base/typograpghy/Text";
import SharpButton from "#/components/base/button/Button";
import SharpTitle from "#/components/base/typograpghy/Title";
import PageLoading from "#/components/base/loading/PageLoading";
import SharpInput from "#/components/base/input/Input";
import { sharpApiUrl } from "#/constants/apiConfig";
import { Form, Input } from "antd";
import {
  ArrowRightOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { changeAxiosBaseUrl } from "#/lib/store/rtk/rtkApiInstance";
import toast from "react-hot-toast";
import { localStorageKeys } from "#/constants/constant";
import SharpDivider from "#/components/base/divider/Divider";

const LandingPage = () => {
  const [validateConnectivity, { data, isLoading, isSuccess, isError }] =
    useValidateConnectivityMutation();
  const [hasValidated, setHasValidated] = useState(false);
  const [sharpAPIUrl, setSharpAPIUrl] = useState(sharpApiUrl);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const router = useRouter();
  const [isFormSubmitted, setIsFormSubmitted] = useState(false);
  useEffect(() => {
    // Trigger connectivity validation on component mount
    validateConnectivity({});
    setHasValidated(true);
  }, [validateConnectivity]);

  const handleSubmit = async () => {
    const values = await form.validateFields();
    setLoading(true);
    setIsFormSubmitted(true);
    const newURL = values.sharpAPIUrl;
    console.log(newURL);
    try {
      if (newURL) {
        changeAxiosBaseUrl(newURL);
        const result = await validateConnectivity({});
        localStorage.setItem(localStorageKeys.sharpAPIUrl, newURL);
        setHasValidated(true);
        // Navigate to dashboard on successful form submission
        if (result.data) {
          router.push("/dashboard");
        }
      } else {
        toast.error("Something went wrong.");
        setHasValidated(false);
      }
    } catch (err) {
      console.log(err);
      toast.error("Invalid instance url.");
      setHasValidated(false);
    }
    setLoading(false);
  };

  return (
    <PageContainer className={styles.landingPage}>
      <SharpFlex justify="center" align="" vertical>
        <SharpLogo height={50} className={`${styles.landingLogo}`} />
        <SharpDivider />
        <Form
          initialValues={{ sharpAPIUrl }}
          layout="vertical"
          form={form}
          onFinish={handleSubmit}
          requiredMark={false}
          style={{ width: "fit-content" }}
        >
          <SharpFlex align="center" gap={0}>
            <Form.Item
              label="SharpAI Server URL"
              name="sharpAPIUrl"
              rules={[
                { required: true, message: "Please enter a valid SharpAPIUrl" },
              ]}
              required
              style={{ width: "400px" }}
            >
              <Input.Search
                size="large"
                loading={loading}
                autoFocus
                disabled={loading}
                value={sharpAPIUrl}
                onChange={(e: any) => setSharpAPIUrl(e.target.value)}
                enterButton={<ArrowRightOutlined />}
                onSearch={handleSubmit}
              />
            </Form.Item>
          </SharpFlex>
        </Form>
        {(isLoading || loading) && <PageLoading allignLeft />}

        {isSuccess && hasValidated && !loading && !isFormSubmitted && (
          <SharpText className="text-color-success mt">
            <CheckCircleOutlined /> Your SharpAI node is operational.
          </SharpText>
        )}

        {isError && hasValidated && (
          <SharpText className="text-color-error mt">
            <CloseCircleOutlined /> Unable to connect to SharpAI services
          </SharpText>
        )}
      </SharpFlex>
    </PageContainer>
  );
};

export default LandingPage;
