import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import { Provider } from "react-redux";
import ChatComplition from "#/app/dashboard/completions/page";
import { getServer } from "../server";
import { handlers } from "../handler";
import createTestStore from "../mockStore";

// Mock Chat component to prevent runtime errors
jest.mock("#/components/base/chat/Chat", () => {
  return function MockChat({
    messages,
    onSendMessage,
    isLoading,
    placeholder,
    emptyStateText,
    disabled,
  }: any) {
    return (
      <div className="chatContainer">
        <div className="emptyState">
          <div className="emptyStateContent">
            <h4
              className="ant-typography headingCommonStyles mb"
              style={{ fontWeight: 500 }}
            >
              {emptyStateText}
            </h4>
            <form className="emptyStateForm">
              <textarea
                className="ant-input ant-input-outlined emptyStateInput"
                disabled={disabled}
                placeholder={placeholder}
                rows={1}
              />
              <button
                className="ant-btn ant-btn-primary ant-btn-color-primary ant-btn-variant-solid sendButton"
                disabled={disabled}
                type="submit"
                onClick={(e) => {
                  e.preventDefault();
                  const textarea =
                    e.currentTarget.parentElement?.querySelector("textarea");
                  if (textarea?.value && onSendMessage) {
                    onSendMessage(textarea.value);
                    textarea.value = "";
                  }
                }}
              >
                <span className="ant-btn-icon">
                  <span
                    aria-label="send"
                    className="anticon anticon-send"
                    role="img"
                  >
                    <svg
                      aria-hidden="true"
                      data-icon="send"
                      fill="currentColor"
                      focusable="false"
                      height="1em"
                      viewBox="64 64 896 896"
                      width="1em"
                    >
                      <defs />
                      <path d="M931.4 498.9L94.9 79.5c-3.4-1.7-7.3-2.1-11-1.2a15.99 15.99 0 00-11.7 19.3l86.2 352.2c1.3 5.3 5.2 9.6 10.4 11.3l147.7 50.7-147.6 50.7c-5.2 1.8-9.1 6-10.3 11.3L72.2 926.5c-.9 3.7-.5 7.6 1.2 10.9 3.9 7.9 13.5 11.1 21.5 7.2l836.5-417c3.1-1.5 5.6-4.1 7.2-7.1 3.9-8 .7-17.6-7.2-21.6zM170.8 826.3l50.3-205.6 295.2-101.3c2.3-.8 4.2-2.6 5-5 1.4-4.2-.8-8.7-5-10.2L221.1 403 171 198.2l628 314.9-628.2 313.2z" />
                    </svg>
                  </span>
                </span>
                <span>Send</span>
              </button>
            </form>
          </div>
        </div>
      </div>
    );
  };
});

// Setup MSW server
const server = getServer(handlers);

// Setup and teardown
beforeAll(() => {
  server.listen();
});

afterEach(() => {
  server.resetHandlers();
  jest.clearAllMocks();
});

afterAll(() => {
  server.close();
});

const renderWithProvider = (component: React.ReactElement) => {
  const store = createTestStore();
  return render(<Provider store={store}>{component}</Provider>);
};

describe("Completions Page", () => {
  it("should render completions page successfully", async () => {
    renderWithProvider(<ChatComplition />);

    await waitFor(() => {
      expect(screen.getByText("Completion")).toBeInTheDocument();
    });

    // Check that the page renders the chat completion interface
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Show Settings")).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText("Type your message here...")
    ).toBeInTheDocument();
  });

  it("should render with correct page structure", async () => {
    const { container } = renderWithProvider(<ChatComplition />);

    await waitFor(() => {
      expect(screen.getByText("Completion")).toBeInTheDocument();
    });

    // Check for page container
    const pageContainer = container.querySelector(".pageContainer");
    expect(pageContainer).toBeInTheDocument();
  });

  it("should display empty state initially", async () => {
    renderWithProvider(<ChatComplition />);

    await waitFor(() => {
      expect(
        screen.getByText("Select a model to start chatting")
      ).toBeInTheDocument();
    });
  });

  it("should match snapshot", async () => {
    const { container } = renderWithProvider(<ChatComplition />);

    await waitFor(() => {
      expect(screen.getByText("Completion")).toBeInTheDocument();
    });

    expect(container.firstChild).toMatchSnapshot();
  });
});
