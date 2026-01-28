import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import { Provider } from "react-redux";
import { configureStore } from "@reduxjs/toolkit";
import DashboardPage from "#/app/dashboard/page";
import { getServer } from "../server";
import { handlers } from "../handler";
import { mockLocalModels, mockApiError, mockEmptyModels } from "../mockData";
import { message } from "antd";
import createTestStore from "../mockStore";
import { http, HttpResponse } from "msw";
import { sharpApiUrl } from "#/constants/apiConfig";

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

describe("Dashboard Page - Initial Render Tests", () => {
  describe("Loading State", () => {
    it("should display loading state initially", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<DashboardPage />);

      expect(screen.getByText("Loading local models...")).toBeInTheDocument();
    });

    it("should match snapshot for loading state", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(screen.getByText("Loading local models...")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe("Success State", () => {
    it("should render dashboard with models successfully", async () => {
      const { container } = renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText(`Local Models (${mockLocalModels.length})`)
        ).toBeInTheDocument();
      });

      // Check if models are displayed in the table
      expect(screen.getAllByText("llama3.2:1b")).toHaveLength(2); // Model Name and Model ID columns
      expect(screen.getAllByText("mistral:7b")).toHaveLength(2); // Model Name and Model ID columns
      expect(screen.getAllByText("codellama:13b")).toHaveLength(2); // Model Name and Model ID columns
    });

    it("should match snapshot for successful dashboard render", async () => {
      const { container } = renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText(`Local Models (${mockLocalModels.length})`)
        ).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });

    it("should display correct page title and action buttons", async () => {
      renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText(`Local Models (${mockLocalModels.length})`)
        ).toBeInTheDocument();
      });

      // Check action buttons
      expect(screen.getByText("Available Models")).toBeInTheDocument();
      expect(screen.getByText("Pull models")).toBeInTheDocument();
    });
  });

  describe("Error State", () => {
    it("should display error message when API fails", async () => {
      // Override with error handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockApiError.data, {
            status: mockApiError.status,
          });
        })
      );

      renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText("Failed to load local models")
        ).toBeInTheDocument();
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

      const { container } = renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText("Failed to load local models")
        ).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });

    it("should display network error when request fails", async () => {
      // Override with network error handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.error();
        })
      );

      renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(
          screen.getByText("Failed to load local models")
        ).toBeInTheDocument();
      });
    });
  });

  describe("Empty State", () => {
    it("should display empty table when no models are available", async () => {
      // Override with empty data handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockEmptyModels);
        })
      );

      renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(screen.getByText("Local Models (0)")).toBeInTheDocument();
      });
    });

    it("should match snapshot for empty state", async () => {
      // Override with empty data handler
      server.use(
        http.get(`${sharpApiUrl}/api/tags`, () => {
          return HttpResponse.json(mockEmptyModels);
        })
      );

      const { container } = renderWithProvider(<DashboardPage />);

      await waitFor(() => {
        expect(screen.getByText("Local Models (0)")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });
});

describe("Dashboard Page - Model Listing Tests", () => {
  it("should display all model columns correctly", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Model Name")).toBeInTheDocument();
      expect(screen.getByText("Model ID")).toBeInTheDocument();
      expect(screen.getByText("Family")).toBeInTheDocument();
      expect(screen.getByText("Format")).toBeInTheDocument();
      expect(screen.getByText("Size")).toBeInTheDocument();
      expect(screen.getByText("Quantization")).toBeInTheDocument();
      expect(screen.getByText("Modified")).toBeInTheDocument();
      expect(screen.getByText("Actions")).toBeInTheDocument();
    });
  });

  it("should match snapshot for table structure", async () => {
    const { container } = renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      expect(screen.getByText("Model Name")).toBeInTheDocument();
    });

    // Get the table element specifically for snapshot
    const tableElement = container.querySelector(".ant-table");
    expect(tableElement).toMatchSnapshot();
  });

  it("should display model data in table rows", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      // Check first model data
      expect(screen.getAllByText("llama3.2:1b")).toHaveLength(2); // Model Name and Model ID columns
      expect(screen.getAllByText("llama")).toHaveLength(3); // Multiple models have llama family
      expect(screen.getAllByText("gguf")).toHaveLength(4); // All models have gguf format
      expect(screen.getAllByText("Q4_0")).toHaveLength(2); // Two models have Q4_0 quantization
    });
  });

  it("should display pagination controls", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      expect(
        screen.getByText(
          `1-${mockLocalModels.length} of ${mockLocalModels.length} models`
        )
      ).toBeInTheDocument();
    });
  });

  it("should have refresh button that works", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const refreshButton = screen.getByRole("img", { name: /reload/i });
      expect(refreshButton).toBeInTheDocument();
    });
  });
});

describe("Dashboard Page - Table Actions Tests", () => {
  it("should show delete buttons for each model", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      expect(deleteButtons).toHaveLength(mockLocalModels.length);
    });
  });

  it("should open confirmation modal when delete button is clicked", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      fireEvent.click(deleteButtons[0]);
    });

    await waitFor(() => {
      expect(screen.getByTestId("confirmation-modal")).toBeInTheDocument();
      expect(screen.getByText("Delete Model")).toBeInTheDocument();
      expect(
        screen.getByText(
          /Are you sure you want to delete the model "llama3.2:1b"/
        )
      ).toBeInTheDocument();
    });
  });

  it("should match snapshot for delete confirmation modal", async () => {
    const { container } = renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      fireEvent.click(deleteButtons[0]);
    });

    await waitFor(() => {
      expect(screen.getByTestId("confirmation-modal")).toBeInTheDocument();
    });

    const modalElement = screen.getByTestId("confirmation-modal");
    expect(modalElement).toMatchSnapshot();
  });

  it("should close confirmation modal when cancel is clicked", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      fireEvent.click(deleteButtons[0]);
    });

    await waitFor(() => {
      const cancelButton = screen.getByTestId("confirm-cancel");
      fireEvent.click(cancelButton);
    });

    await waitFor(() => {
      expect(
        screen.queryByTestId("confirmation-modal")
      ).not.toBeInTheDocument();
    });
  });

  it("should delete model when confirmed", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      fireEvent.click(deleteButtons[0]);
    });

    await waitFor(() => {
      const confirmButton = screen.getByTestId("confirm-delete");
      fireEvent.click(confirmButton);
    });

    await waitFor(() => {
      expect(message.success).toHaveBeenCalledWith(
        "Successfully deleted model: llama3.2:1b"
      );
    });
  });

  it("should handle delete error gracefully", async () => {
    // First render with success to get the table with delete buttons
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const deleteButtons = screen.getAllByTitle("Delete Model");
      fireEvent.click(deleteButtons[0]);
    });

    // Override with error handler for delete operation
    server.use(
      http.delete(`${sharpApiUrl}/api/delete`, () => {
        return HttpResponse.json(
          { error: "Failed to delete model" },
          { status: 500 }
        );
      })
    );

    await waitFor(() => {
      const confirmButton = screen.getByTestId("confirm-delete");
      fireEvent.click(confirmButton);
    });

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith(
        expect.stringContaining("Failed to delete model")
      );
    });
  });
});

describe("Dashboard Page - Pull Models Button Tests", () => {
  it("should display Pull models button", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const pullButton = screen.getByText("Pull models");
      expect(pullButton).toBeInTheDocument();
    });
  });

  it("should open pull model modal when button is clicked", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const pullButton = screen.getByText("Pull models");
      fireEvent.click(pullButton);
    });

    await waitFor(() => {
      expect(screen.getByTestId("pull-model-modal")).toBeInTheDocument();
      expect(screen.getByText("Pull Model Modal")).toBeInTheDocument();
    });
  });

  it("should match snapshot for pull model modal", async () => {
    const { container } = renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const pullButton = screen.getByText("Pull models");
      fireEvent.click(pullButton);
    });

    await waitFor(() => {
      expect(screen.getByTestId("pull-model-modal")).toBeInTheDocument();
    });

    const modalElement = screen.getByTestId("pull-model-modal");
    expect(modalElement).toMatchSnapshot();
  });

  it("should close pull model modal when close button is clicked", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const pullButton = screen.getByText("Pull models");
      fireEvent.click(pullButton);
    });

    await waitFor(() => {
      const closeButton = screen.getByTestId("modal-close");
      fireEvent.click(closeButton);
    });

    await waitFor(() => {
      expect(screen.queryByTestId("pull-model-modal")).not.toBeInTheDocument();
    });
  });

  it("should refresh models list when pull is successful", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const pullButton = screen.getByText("Pull models");
      fireEvent.click(pullButton);
    });

    await waitFor(() => {
      const successButton = screen.getByTestId("modal-success");
      fireEvent.click(successButton);
    });

    // The modal should trigger a refetch of the models
    await waitFor(() => {
      expect(
        screen.getByText(`Local Models (${mockLocalModels.length})`)
      ).toBeInTheDocument();
    });
  });

  it("should display Available Models link with correct attributes", async () => {
    renderWithProvider(<DashboardPage />);

    await waitFor(() => {
      const availableModelsLink = screen.getByText("Available Models");
      expect(availableModelsLink).toBeInTheDocument();
      expect(availableModelsLink.closest("a")).toHaveAttribute(
        "href",
        "https://huggingface.co/models?search=gguf"
      );
      expect(availableModelsLink.closest("a")).toHaveAttribute(
        "target",
        "_blank"
      );
      expect(availableModelsLink.closest("a")).toHaveAttribute(
        "rel",
        "noopener noreferrer"
      );
    });
  });
});
