import MarkdownIt from 'markdown-it';

// Raw HTML is literal source text. Markdown-it also rejects executable schemes.
export const createMarkdown = () => new MarkdownIt({ html: false, linkify: false, typographer: false });
