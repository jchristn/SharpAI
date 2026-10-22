import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

/** Model output uses Markdown with raw HTML disabled. Load its parser only for chat responses. */
export default function ChatMarkdown({text}:{text:string}):JSX.Element {
  return <Markdown remarkPlugins={[remarkGfm]}>{text}</Markdown>;
}
