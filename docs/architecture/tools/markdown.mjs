import MarkdownIt from 'markdown-it';

// Raw HTML is literal source text. Markdown-it also rejects executable schemes.
export const createMarkdown = () => new MarkdownIt({ html: false, linkify: false, typographer: false });

export const slug = text => text.toLowerCase().replace(/<[^>]*>/g, '').replace(/[^\p{L}\p{N}_\-\s]/gu, '').replace(/\s/g, '-');
export const docId = file => `source-${file.replace(/[^a-zA-Z0-9]/g, '-')}`;
export const inlineText = token => (token.children || []).map(c => c.type === 'softbreak' || c.type === 'hardbreak' ? '\n' : c.content).join('');

export function documentHeadings(tokens, file) {
  const counts = new Map();
  const headings = [];
  for (let i = 0; i < tokens.length; i++) {
    if (tokens[i].type !== 'heading_open') continue;
    const title = inlineText(tokens[i + 1]);
    const base = slug(title);
    const count = counts.get(base) || 0;
    counts.set(base, count + 1);
    const fragment = base + (count ? `-${count}` : '');
    const id = `${docId(file)}--${fragment}`;
    tokens[i].attrSet('id', id);
    headings.push({ id, fragment, title, level: Number(tokens[i].tag.slice(1)) });
  }
  return headings;
}
