import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'

const md = new MarkdownIt({
  html: false,
  linkify: true,
  typographer: false,
})

// Override image rendering to show alt-text placeholder
md.renderer.rules.image = (tokens, idx) => {
  const raw = tokens[idx].content || tokens[idx].children?.reduce((s, t) => s + t.content, '') || 'image'
  const alt = md.utils.escapeHtml(raw)
  return `<span class="inline-flex items-center gap-1 rounded bg-muted px-2 py-1 text-xs text-muted-foreground">[${alt}]</span>`
}

export function useMarkdown() {
  function render(content: string): string {
    return DOMPurify.sanitize(md.render(content))
  }

  function stripFrontmatter(content: string): string {
    if (!content.startsWith('---\n') && !content.startsWith('---\r\n')) return content
    const end = content.indexOf('\n---', 3)
    if (end === -1) return content
    // Skip past the closing --- and the newline after it
    const afterFrontmatter = content.indexOf('\n', end + 4)
    if (afterFrontmatter === -1) return ''
    return content.slice(afterFrontmatter + 1)
  }

  return { render, stripFrontmatter }
}
