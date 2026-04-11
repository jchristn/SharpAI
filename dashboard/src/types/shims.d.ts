declare module "react-markdown-renderer" {
  import * as React from "react";
  interface MarkdownRendererProps {
    markdown: string;
    options?: Record<string, unknown>;
    tablesClass?: string;
  }
  const MarkdownRenderer: React.FC<MarkdownRendererProps>;
  export default MarkdownRenderer;
}
