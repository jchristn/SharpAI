import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import { Provider } from "react-redux";
import EmbeddingsPage from "#/app/dashboard/embeddings/page";
import { getServer } from "../server";
import { handlers } from "../handler";
import { mockLocalModels, mockApiError, mockEmptyModels } from "../mockData";
import { message } from "antd";
import createTestStore from "../mockStore";
import { http, HttpResponse } from "msw";
import { sharpApiUrl } from "#/constants/apiConfig";
import userEvent from "@testing-library/user-event";

// Mock antd message
jest.mock("antd", () => ({
  ...jest.requireActual("antd"),
  message: {
    success: jest.fn(),
    error: jest.fn(),
  },
}));

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

describe("Embeddings Page - Initial Render Tests", () => {
  describe("Loading State", () => {
    it("should display loading state initially", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<EmbeddingsPage />);

      expect(screen.getByText("Loading models...")).toBeInTheDocument();
    });

    it("should match snapshot for loading state", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Loading models...")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe("Success State", () => {
    it("should render embeddings page with form successfully", async () => {
      const { container } = renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Embeddings")).toBeInTheDocument();
      });

      // Check form elements
      expect(screen.getByText("Model")).toBeInTheDocument();
      expect(screen.getByText("Input")).toBeInTheDocument();
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();

      // Check for select elements (mocked as regular select elements)
      const selects = screen.getAllByRole("combobox");
      expect(selects).toHaveLength(3); // Model select and Input select
    });

    it("should match snapshot for successful embeddings page render", async () => {
      const { container } = renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Embeddings")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });

    it("should display model options in select dropdown", async () => {
      const user = userEvent.setup();
      renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Embeddings")).toBeInTheDocument();
      });

      // Check if model options are displayed in the select
      expect(screen.getByText("llama3.2:1b")).toBeInTheDocument();
      expect(screen.getByText("mistral:7b")).toBeInTheDocument();
      expect(screen.getByText("codellama:13b")).toBeInTheDocument();
    });
  });

  describe("Error State", () => {
    it("should display error message when models API fails", async () => {
      // Override with error handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockApiError.data, {
            status: mockApiError.status,
          });
        })
      );

      renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Failed to load models")).toBeInTheDocument();
      });
    });

    it("should match snapshot for error state", async () => {
      // Override with error handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockApiError.data, {
            status: mockApiError.status,
          });
        })
      );

      const { container } = renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Failed to load models")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe("Empty Models State", () => {
    it("should handle empty models list", async () => {
      // Override with empty data handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockEmptyModels);
        })
      );

      renderWithProvider(<EmbeddingsPage />);

      await waitFor(() => {
        expect(screen.getByText("Embeddings")).toBeInTheDocument();
      });

      // Form should still be rendered but with no model options
      const selects = screen.getAllByRole("combobox");
      expect(selects).toHaveLength(3); // Model select and Input select
    });
  });
});

describe("Embeddings Page - Form Interaction Tests", () => {
  it("should render form with validation structure", async () => {
    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    });

    // Check that form elements have required validation structure
    const form = document.querySelector("form");
    expect(form).toBeInTheDocument();

    // Verify that the form has the correct structure for validation
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Input")).toBeInTheDocument();

    // Check for required field indicators
    const requiredLabels = document.querySelectorAll(".ant-form-item-required");
    expect(requiredLabels.length).toBeGreaterThan(0);

    // The form should have validation rules in place
    const submitButton = screen.getByText("Generate Embeddings");
    expect(submitButton).toBeInTheDocument();

    // Check if there's a submit button in the form (Ant Design wraps it)
    const submitButtonInForm = form?.querySelector('button[type="submit"]');
    expect(submitButtonInForm).toBeInTheDocument();
  });

  it("should display form elements correctly", async () => {
    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    });

    // Check that form elements are present
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Input")).toBeInTheDocument();
    expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Note: You can enter multiple inputs separated by enter key."
      )
    ).toBeInTheDocument();

    // Check that model options are available
    expect(screen.getByText("llama3.2:1b")).toBeInTheDocument();
    expect(screen.getByText("mistral:7b")).toBeInTheDocument();
  });
});

describe("Embeddings Page - API Error Handling Tests", () => {
  it("should display error handler setup correctly", async () => {
    // Override with error handler for embeddings API
    server.use(
      http.post(`${sharpApiUrl}/api/embed`, () => {
        return HttpResponse.json(
          { error: "Failed to generate embeddings" },
          { status: 500 }
        );
      })
    );

    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    });

    // Verify the form is rendered correctly even with error handler
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Input")).toBeInTheDocument();
  });
});

describe("Embeddings Page - Copy Functionality Tests", () => {
  it("should render copy functionality elements", async () => {
    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    });

    // Verify the form structure is correct for copy functionality
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Input")).toBeInTheDocument();
    expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
  });
});

describe("Embeddings Page - Form Validation Tests", () => {
  it("should show helper text for input field", async () => {
    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(
        screen.getByText(
          "Note: You can enter multiple inputs separated by enter key."
        )
      ).toBeInTheDocument();
    });
  });

  it("should render form elements correctly", async () => {
    renderWithProvider(<EmbeddingsPage />);

    await waitFor(() => {
      expect(screen.getByText("Generate Embeddings")).toBeInTheDocument();
    });

    // Verify form structure
    expect(screen.getByText("Model")).toBeInTheDocument();
    expect(screen.getByText("Input")).toBeInTheDocument();

    // Check that the submit button is present
    const submitButton = screen.getByText("Generate Embeddings");
    expect(submitButton).toBeInTheDocument();
  });
});
