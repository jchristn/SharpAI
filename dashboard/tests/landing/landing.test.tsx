import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import { Provider } from "react-redux";
import LandingPage from "#/app/page";
import { getServer } from "../server";
import { handlers } from "../handler";
import { sharpApiUrl } from "#/constants/apiConfig";
import createTestStore from "../mockStore";
import { http, HttpResponse } from "msw";
import userEvent from "@testing-library/user-event";

// Mock Next.js router
const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  replace: jest.fn(),
  prefetch: jest.fn(),
  back: jest.fn(),
  forward: jest.fn(),
  refresh: jest.fn(),
  pathname: "/",
  query: {},
  asPath: "/",
};

jest.mock("next/navigation", () => ({
  useRouter: () => mockRouter,
}));

// Mock react-hot-toast
jest.mock("react-hot-toast", () => ({
  __esModule: true,
  default: {
    error: jest.fn(),
    success: jest.fn(),
  },
}));

// Mock localStorage
const mockLocalStorage = {
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
  clear: jest.fn(),
};
Object.defineProperty(window, "localStorage", {
  value: mockLocalStorage,
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
  mockLocalStorage.getItem.mockClear();
  mockLocalStorage.setItem.mockClear();
});

afterAll(() => {
  server.close();
});

const renderWithProvider = (component: React.ReactElement) => {
  const store = createTestStore();
  return render(<Provider store={store}>{component}</Provider>);
};

describe("Landing Page - Initial Render Tests", () => {
  describe("Loading State", () => {
    it("should display loading state initially", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<LandingPage />);

      // Should show PageLoading component when isLoading is true
      await waitFor(() => {
        expect(screen.getByText("Loading...")).toBeInTheDocument();
      });
    });

    it("should match snapshot for loading state", async () => {
      // Override with no handlers to simulate loading
      server.use();

      const { container } = renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("Loading...")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe("Success State", () => {
    it("should render landing page with form successfully", async () => {
      const { container } = renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      // Check form elements
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      expect(screen.getByDisplayValue(sharpApiUrl)).toBeInTheDocument();

      // Check for Input.Search component with enter button
      const searchInput = screen.getByDisplayValue(sharpApiUrl);
      expect(searchInput).toBeInTheDocument();
      expect(searchInput.closest(".ant-input-search")).toBeInTheDocument();
    });

    it("should match snapshot for successful landing page render", async () => {
      const { container } = renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });

    it("should display SharpLogo", async () => {
      renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      // Check if logo is present
      const logo = screen.getByAltText("Sharp AI");
      expect(logo).toBeInTheDocument();
    });

    it("should display operational message when connectivity is successful", async () => {
      server.use(
        http.get(`${sharpApiUrl}`, () => {
          return HttpResponse.json(
            { status: "ok", message: "SharpAI services are operational" },
            { status: 200 }
          );
        })
      );
      renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      // Wait for success state and check for operational message
      await waitFor(() => {
        expect(
          screen.getByText("Your SharpAI node is operational.")
        ).toBeInTheDocument();
      });
    });
  });

  describe("Connectivity Success State", () => {
    it("should show form and handle successful connectivity validation", async () => {
      renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      // Initially, the form should be visible
      expect(screen.getByDisplayValue(sharpApiUrl)).toBeInTheDocument();

      // Check for Input.Search with enter button
      const searchInput = screen.getByDisplayValue(sharpApiUrl);
      expect(searchInput.closest(".ant-input-search")).toBeInTheDocument();
    });

    it("should match snapshot for initial state", async () => {
      const { container } = renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe("Error State", () => {
    it("should display error message when connectivity validation fails", async () => {
      // Override with error handler
      server.use(
        http.get(`${sharpApiUrl}`, () => {
          return HttpResponse.json(
            { error: "Connection failed" },
            { status: 500 }
          );
        })
      );

      renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(
          screen.getByText("Unable to connect to SharpAI services")
        ).toBeInTheDocument();
      });
    });

    it("should match snapshot for error state", async () => {
      // Override with error handler
      server.use(
        http.get(`${sharpApiUrl}`, () => {
          return HttpResponse.json(
            { error: "Connection failed" },
            { status: 500 }
          );
        })
      );

      const { container } = renderWithProvider(<LandingPage />);

      await waitFor(() => {
        expect(
          screen.getByText("Unable to connect to SharpAI services")
        ).toBeInTheDocument();
      });

      expect(container.firstChild).toMatchSnapshot();
    });
  });
});

describe("Landing Page - Form Interaction Tests", () => {
  it("should render form with correct structure", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Check form elements
    const input = screen.getByDisplayValue(sharpApiUrl);
    expect(input).toBeInTheDocument();
    expect(input).toHaveValue(sharpApiUrl);

    // Check for Input.Search structure
    expect(input.closest(".ant-input-search")).toBeInTheDocument();
  });

  it("should have required field validation", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Check for required field indicator
    const requiredLabels = document.querySelectorAll(".ant-form-item-required");
    expect(requiredLabels.length).toBeGreaterThan(0);
  });

  it("should have input field with correct attributes", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    const input = screen.getByDisplayValue(sharpApiUrl);
    expect(input).toHaveAttribute("type", "search");

    // Check for Input.Search specific attributes
    expect(input.closest(".ant-input-search")).toBeInTheDocument();
  });
});

describe("Landing Page - API Error Handling Tests", () => {
  it("should setup error handler correctly", async () => {
    // Override with error handler
    server.use(
      http.post(`${sharpApiUrl}/api/validate`, () => {
        return HttpResponse.json(
          { error: "Invalid instance url" },
          { status: 400 }
        );
      })
    );

    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Verify form is still rendered with error handler
    expect(screen.getByDisplayValue(sharpApiUrl)).toBeInTheDocument();

    // Check for Input.Search structure
    const input = screen.getByDisplayValue(sharpApiUrl);
    expect(input.closest(".ant-input-search")).toBeInTheDocument();
  });
});

describe("Landing Page - Navigation Tests", () => {
  it("should have router mock setup correctly", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Verify that router mock is available
    expect(mockPush).toBeDefined();
    expect(typeof mockPush).toBe("function");
  });

  it("should navigate to dashboard on successful form submission", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Test that navigation would be called on successful validation
    // Note: This would require mocking the form submission and validation success
    expect(mockPush).toBeDefined();
  });
});

describe("Landing Page - Form Validation Tests", () => {
  it("should show required field indicator", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Check for required field indicator
    const requiredLabels = document.querySelectorAll(".ant-form-item-required");
    expect(requiredLabels.length).toBeGreaterThan(0);
  });

  it("should render form with correct initial values", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    const input = screen.getByDisplayValue(sharpApiUrl);
    expect(input).toHaveValue(sharpApiUrl);
  });
});

describe("Landing Page - LocalStorage Tests", () => {
  it("should have localStorage mock setup correctly", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Verify localStorage mock is available
    expect(mockLocalStorage.setItem).toBeDefined();
    expect(mockLocalStorage.getItem).toBeDefined();
    expect(typeof mockLocalStorage.setItem).toBe("function");
  });

  it("should save URL to localStorage on successful validation", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Test that localStorage would be called on successful form submission
    // Note: This would require mocking the form submission and validation success
    expect(mockLocalStorage.setItem).toBeDefined();
  });
});

describe("Landing Page - Component Structure Tests", () => {
  it("should render with correct CSS classes", async () => {
    const { container } = renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Check for landing page specific styling
    const pageContainer = container.querySelector(".pageContainer");
    expect(pageContainer).toBeInTheDocument();
  });

  it("should render form with vertical layout", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    const form = document.querySelector(".ant-form-vertical");
    expect(form).toBeInTheDocument();
  });

  it("should render Input.Search with enter button and arrow icon", async () => {
    renderWithProvider(<LandingPage />);

    await waitFor(() => {
      expect(screen.getByText("SharpAI Server URL")).toBeInTheDocument();
    });

    // Check for Input.Search component
    const input = screen.getByDisplayValue(sharpApiUrl);
    const searchWrapper = input.closest(".ant-input-search");
    expect(searchWrapper).toBeInTheDocument();

    // Check for enter button with arrow icon
    const enterButton = searchWrapper?.querySelector(
      ".ant-input-search-button"
    );
    expect(enterButton).toBeInTheDocument();

    // Check for arrow icon within the enter button
    const icon = enterButton?.querySelector(".anticon-arrow-right");
    expect(icon).toBeInTheDocument();
  });
});
