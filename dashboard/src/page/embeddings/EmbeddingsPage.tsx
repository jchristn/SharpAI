
import React, { useEffect, useState } from "react";
import { Col, Form, message, Row, Tag } from "antd";
import PageContainer from "#/components/base/pageContainer/PageContainer";
import SharpSelect from "#/components/base/select/Select";
import SharpInput from "#/components/base/input/Input";
import SharpFormItem from "#/components/base/form/FormItem";
import SharpButton from "#/components/base/button/Button";
import SharpFlex from "#/components/base/flex/Flex";
import SharpText from "#/components/base/typograpghy/Text";
import PageLoading from "#/components/base/loading/PageLoading";
import FallBack from "#/components/base/fallback/FallBack";
import {
  useGetLocalModelsQuery,
  useGenerateEmbeddingsMutation,
  useGenerateEmbeddingsOpenAIMutation,
} from "#/lib/reducer/apiSlice";
import { filterModelsForPage, formatError } from "#/utils/utils";
import SharpTitle from "#/components/base/typograpghy/Title";
import styles from "./embed.module.scss";
import SharpCard from "#/components/base/card/Card";
import CopyText from "#/components/common/CopyText";
import { requestFormatOptions } from "./constants";
import { RequestFormatEnum } from "#/types/types";
import {
  GenerateEmbeddingsOpenAIResponse,
  GenerateEmbeddingsResponse,
} from "#/lib/reducer/types";
import SharpTooltip from "#/components/base/tooltip/Tooltip";
import { tooltips, pageDescriptions } from "#/constants/tooltips";

interface FormValues {
  model: string;
  requestFormat: RequestFormatEnum;
}

const EmbeddingsPage = () => {
  const [form] = Form.useForm<FormValues>();
  const [embeddings, setEmbeddings] = useState<
    { text: string; embedding: number[] }[]
  >([]);
  const [inputValues, setInputValues] = useState<string[]>([]);
  const [inputDraft, setInputDraft] = useState("");

  const {
    data: localModels,
    isLoading: modelsLoading,
    isError: modelsError,
    error: modelsErrorData,
  } = useGetLocalModelsQuery(undefined, { refetchOnMountOrArgChange: true });

  const [generateEmbeddings, { isLoading }] = useGenerateEmbeddingsMutation();
  const [generateEmbeddingsOpenAI, { isLoading: generatingEmbeddingsOpenAI }] =
    useGenerateEmbeddingsOpenAIMutation();
  const generatingEmbeddings = isLoading || generatingEmbeddingsOpenAI;

  // Auto-select the only embedding model if exactly one exists
  useEffect(() => {
    const matches = filterModelsForPage(localModels, "embedding");
    if (matches.length === 1 && !form.getFieldValue("model")) {
      form.setFieldValue("model", matches[0].name);
    }
  }, [localModels, form]);
  const getCurrentInputs = () => {
    const draft = inputDraft.trim();
    return draft ? [...inputValues, draft] : inputValues;
  };

  const commitInputDraft = () => {
    const draft = inputDraft.trim();
    if (!draft) return;

    setInputValues((current) => [...current, draft]);
    setInputDraft("");
  };

  const removeInputValue = (index: number) => {
    setInputValues((current) => current.filter((_, idx) => idx !== index));
  };

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      commitInputDraft();
    }
  };

  const handleSubmit = async (values: FormValues) => {
    const inputs = getCurrentInputs();

    if (inputs.length === 0) {
      message.error("Please enter input text!");
      return;
    }

    if (inputDraft.trim()) {
      setInputValues(inputs);
      setInputDraft("");
    }

    try {
      let response:
        | GenerateEmbeddingsResponse
        | GenerateEmbeddingsOpenAIResponse
        | null = null;
      if (values.requestFormat === RequestFormatEnum.OLLAMA) {
        response = await generateEmbeddings({
          model: values.model,
          input: inputs,
        }).unwrap();

        setEmbeddings(
          response.embeddings.map((embedding, index) => ({
            text: inputs[index],
            embedding,
          }))
        );
      } else {
        response = await generateEmbeddingsOpenAI({
          model: values.model,
          input: inputs,
        }).unwrap();
        setEmbeddings(
          response.data.map((embedding, index) => ({
            text: inputs[index],
            embedding: embedding.embedding,
          }))
        );
      }

      message.success("Embeddings generated successfully!");
    } catch (error) {
      message.error(`Failed to generate embeddings: ${formatError(error)}`);
    }
  };

  const clearResults = () => {
    setEmbeddings([]);
  };
  if (modelsLoading) {
    return <PageLoading message="Loading models..." />;
  }

  if (modelsError) {
    return (
      <FallBack>
        <div style={{ textAlign: "center" }}>
          <p>Failed to load models</p>
          <p style={{ fontSize: "0.9em", color: "#666", marginTop: "8px" }}>
            {formatError(modelsErrorData)}
          </p>
        </div>
      </FallBack>
    );
  }

  const modelOptions = filterModelsForPage(localModels, "embedding").map(
    (model) => ({
      value: model.name,
      label: model.name,
    })
  );

  return (
    <PageContainer
      pageTitle="Embeddings"
      pageSubtitle={pageDescriptions.embeddings}
    >
      <SharpFlex vertical gap={24}>
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          initialValues={{ requestFormat: RequestFormatEnum.OLLAMA }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <SharpFormItem
                label="Model"
                name="model"
                tooltip={tooltips.embeddings.model}
                rules={[{ required: true, message: "Please select a model!" }]}
                style={{ flex: 1 }}
              >
                <SharpSelect
                  placeholder="Select a model"
                  options={modelOptions}
                  style={{ width: "100%" }}
                />
              </SharpFormItem>
            </Col>
            <Col span={12}>
              <SharpFormItem
                label="Request Format"
                name="requestFormat"
                tooltip={tooltips.embeddings.requestFormat}
                rules={[
                  {
                    required: true,
                    message: "Please select a request format!",
                  },
                ]}
                style={{ flex: 1 }}
              >
                <SharpSelect
                  placeholder="Select a request format"
                  options={requestFormatOptions}
                  style={{ width: "100%" }}
                />
              </SharpFormItem>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={12}>
              <SharpFormItem
                label="Input"
                tooltip={tooltips.embeddings.input}
                required
                style={{ flex: 1 }}
              >
                <div className={styles.inputTagEditor}>
                  {inputValues.map((value, index) => (
                    <Tag
                      key={`${value}-${index}`}
                      closable
                      className={styles.inputTag}
                      onClose={(event) => {
                        event.preventDefault();
                        removeInputValue(index);
                      }}
                    >
                      <span className={styles.inputTagText}>{value}</span>
                    </Tag>
                  ))}
                  <SharpInput
                    value={inputDraft}
                    onChange={(event) => setInputDraft(event.target.value)}
                    onKeyDown={handleInputKeyDown}
                    placeholder="Enter text to generate embeddings for..."
                    className={styles.inputTagField}
                  />
                </div>
              </SharpFormItem>
            </Col>
          </Row>
          <SharpFormItem className="mt-sm">
            <SharpFlex gap={12}>
              <SharpTooltip title={tooltips.embeddings.generate}>
                <SharpButton
                  type="primary"
                  htmlType="submit"
                  loading={generatingEmbeddings}
                >
                  Generate Embeddings
                </SharpButton>
              </SharpTooltip>
              {embeddings && (
                <SharpTooltip title={tooltips.embeddings.clear}>
                  <SharpButton onClick={clearResults}>
                    Clear Results
                  </SharpButton>
                </SharpTooltip>
              )}
            </SharpFlex>
          </SharpFormItem>
          <SharpText type="secondary">
            Note: You can enter multiple inputs separated by enter key.
          </SharpText>
        </Form>

        {Boolean(embeddings?.length > 0) && (
          <div>
            <SharpText strong>Generated Embeddings:</SharpText>
            <SharpFlex gap={12} vertical className="mt">
              {embeddings.map((embedding, index) => (
                <SharpCard
                  title={
                    <SharpFlex align="center" justify="space-between" gap={12}>
                      <SharpTitle level={5} className="mb-0">
                        Input {index + 1} : {embedding.text}
                      </SharpTitle>
                      <CopyText
                        displayText="Copy embeddings"
                        text={JSON.stringify(embedding.embedding)}
                      />
                    </SharpFlex>
                  }
                >
                  <div className={styles.vectorEmbeddings} key={index}>
                    {JSON.stringify(embedding.embedding)}
                  </div>
                </SharpCard>
              ))}
            </SharpFlex>
          </div>
        )}
      </SharpFlex>
    </PageContainer>
  );
};

export default EmbeddingsPage;
