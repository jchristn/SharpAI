import "@testing-library/jest-dom";

// Mock next/navigation
jest.mock("next/navigation", () => ({
  useRouter() {
    return {
      push: jest.fn(),
      replace: jest.fn(),
      prefetch: jest.fn(),
    };
  },
  usePathname() {
    return "";
  },
}));


// Mock antd message
jest.mock("antd", () => ({
  ...jest.requireActual("antd"),
  message: {
    success: jest.fn(),
    error: jest.fn(),
  },
}));

// Mock the PullModelModal component
jest.mock("#/page/dashboard-home/PullModelModal", () => {
  return function MockPullModelModal({
    isOpen,
    onClose,
    onSuccess,
  }) {
    return isOpen ? (
      <div data-testid="pull-model-modal">
        <h2>Pull Model Modal</h2>
        <button onClick={onClose} data-testid="modal-close">
          Close
        </button>
        <button onClick={onSuccess} data-testid="modal-success">
          Success
        </button>
      </div>
    ) : null;
  };
});

// Mock ConfirmationModal
jest.mock("#/components/common/ConfirmationModal", () => {
  return function MockConfirmationModal({
    isOpen,
    onClose,
    onConfirm,
    title,
    message,
    isLoading,
  }) {
    return isOpen ? (
      <div data-testid="confirmation-modal">
        <h2>{title}</h2>
        <p>{message}</p>
        <button
          onClick={onClose}
          data-testid="confirm-cancel"
          disabled={isLoading}
        >
          Cancel
        </button>
        <button
          onClick={onConfirm}
          data-testid="confirm-delete"
          disabled={isLoading}
        >
          {isLoading ? "Deleting..." : "Delete"}
        </button>
      </div>
    ) : null;
  };
});


// Patch unsupported selector behavior in Ant Design
Object.defineProperty(window, "getComputedStyle", {
  value: () => ({
    getPropertyValue: () => "",
  }),
});

// jest.useFakeTimers();

beforeAll(() => {
  jest.useFakeTimers("modern");
  jest.setSystemTime(new Date("2023-01-01T12:00:00Z"));
});

afterAll(() => {
  jest.useRealTimers();
});

// Mock rc-util scrollbar size logic
jest.mock("rc-util/lib/getScrollBarSize", () => ({
  __esModule: true,
  getTargetScrollBarSize: () => ({
    width: 0,
    height: 0,
  }),
}));

// eslint-disable-next-line react/display-name
jest.mock("#/components/base/select/Select", () => (props) => {
  return (
    <select
      data-testid={props["data-testid"]}
      value={props.value}
      onChange={(e) => props.onChange(e.target.value)}
    >
      {props?.options?.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
});

window.matchMedia =
  window.matchMedia ||
  function () {
    return {
      matches: false,
      addListener: () => {},
      removeListener: () => {},
    };
  };

class BroadcastChannelMock {
  constructor(channelName) {
    this.name = channelName;
    this.onmessage = null;
  }

  postMessage(message) {
    if (this.onmessage) {
      this.onmessage({ data: message });
    }
  }

  close() {
    // No-op
  }
}

global.BroadcastChannel = BroadcastChannelMock;

global.TransformStream = class {
  constructor() {
    this.readable = {};
    this.writable = {};
  }
};
