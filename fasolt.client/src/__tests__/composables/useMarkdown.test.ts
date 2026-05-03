import { describe, it, expect } from 'vitest'
import { useMarkdown } from '@/composables/useMarkdown'

const { render } = useMarkdown()

describe('useMarkdown — markdown', () => {
  it('renders plain markdown', () => {
    expect(render('**bold**')).toContain('<strong>bold</strong>')
  })

  it('does not interpret math delimiters when no math is present', () => {
    const html = render('a regular paragraph')
    expect(html).not.toContain('katex')
  })
})

describe('useMarkdown — KaTeX inline', () => {
  it('renders \\( ... \\) inline math', () => {
    const html = render('derivative of \\(\\sin(x)\\) is cool')
    expect(html).toContain('class="katex"')
  })

  it('renders $ ... $ inline math', () => {
    const html = render('Euler: $e^{i\\pi} + 1 = 0$')
    expect(html).toContain('class="katex"')
  })

  it('does not match a single $ in prose', () => {
    const html = render('It costs $5 and $10')
    expect(html).not.toContain('class="katex"')
  })

  it('does not match escaped \\$', () => {
    const html = render('cost is \\$5 here')
    expect(html).not.toContain('class="katex"')
  })
})

describe('useMarkdown — KaTeX block', () => {
  it('renders $$ ... $$ block math', () => {
    const html = render('$$\nE = mc^2\n$$')
    expect(html).toContain('class="math-block"')
    expect(html).toContain('class="katex"')
  })

  it('renders \\[ ... \\] block math', () => {
    const html = render('\\[\nE = mc^2\n\\]')
    expect(html).toContain('class="math-block"')
  })

  it('renders inline $$ ... $$ on a single line', () => {
    const html = render('$$E = mc^2$$')
    expect(html).toContain('class="math-block"')
  })
})

describe('useMarkdown — KaTeX extensions', () => {
  it('renders mhchem chemistry via \\ce', () => {
    const html = render('water: $\\ce{H2O}$')
    expect(html).toContain('class="katex"')
  })

  it('renders physics-style \\dv macro', () => {
    const html = render('$\\dv{x}{t}$')
    expect(html).toContain('class="katex"')
  })

  it('renders physics-style \\pdv macro', () => {
    const html = render('$\\pdv{f}{x}$')
    expect(html).toContain('class="katex"')
  })
})

describe('useMarkdown — KaTeX security', () => {
  it('does not render \\href links (trust:false)', () => {
    const html = render('$\\href{https://evil.example}{click}$')
    // KaTeX produces an error span when an unknown/blocked command is used
    expect(html).not.toContain('href=')
  })

  it('does not crash on malformed math (throwOnError:false)', () => {
    const html = render('$\\unknownmacro{x}$')
    expect(html).toBeTypeOf('string')
  })
})
