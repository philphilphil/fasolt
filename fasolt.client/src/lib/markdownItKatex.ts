import type MarkdownIt from 'markdown-it'
import { renderMath } from './katex'

const ESC = 0x5c // \
const DOLLAR = 0x24 // $
const PAREN_OPEN = 0x28 // (
const PAREN_CLOSE = 0x29 // )
const BRACKET_OPEN = 0x5b // [
const NEWLINE = 0x0a

function isWhitespaceCharCode(code: number): boolean {
  return code === 0x20 || code === 0x09 || code === NEWLINE || code === 0x0d
}

function inlineMath(state: any, silent: boolean): boolean {
  const start = state.pos
  const max = state.posMax
  const src = state.src as string
  if (start >= max) return false
  const code = src.charCodeAt(start)

  // \( ... \)
  if (code === ESC && start + 1 < max && src.charCodeAt(start + 1) === PAREN_OPEN) {
    let pos = start + 2
    let found = -1
    while (pos < max - 1) {
      if (src.charCodeAt(pos) === ESC && src.charCodeAt(pos + 1) === PAREN_CLOSE) {
        found = pos
        break
      }
      pos++
    }
    if (found < 0) return false
    if (!silent) {
      const token = state.push('math_inline', 'math', 0)
      token.markup = '\\(\\)'
      token.content = src.slice(start + 2, found)
    }
    state.pos = found + 2
    return true
  }

  // $ ... $  (not $$, not escaped, no newlines, non-space adjacent to delimiters)
  if (code === DOLLAR) {
    if (start > 0 && src.charCodeAt(start - 1) === ESC) return false
    if (start + 1 < max && src.charCodeAt(start + 1) === DOLLAR) return false

    const afterOpen = start + 1
    if (afterOpen >= max) return false
    if (isWhitespaceCharCode(src.charCodeAt(afterOpen))) return false

    let pos = afterOpen
    let found = -1
    while (pos < max) {
      const c = src.charCodeAt(pos)
      if (c === NEWLINE) return false
      if (c === DOLLAR && src.charCodeAt(pos - 1) !== ESC) {
        if (pos > afterOpen && !isWhitespaceCharCode(src.charCodeAt(pos - 1))) {
          found = pos
          break
        }
      }
      pos++
    }
    if (found < 0) return false
    if (!silent) {
      const token = state.push('math_inline', 'math', 0)
      token.markup = '$'
      token.content = src.slice(start + 1, found)
    }
    state.pos = found + 1
    return true
  }

  return false
}

function blockMath(state: any, startLine: number, endLine: number, silent: boolean): boolean {
  const startPos = state.bMarks[startLine] + state.tShift[startLine]
  const lineMax = state.eMarks[startLine]
  if (startPos + 1 >= lineMax) return false

  const src = state.src as string
  const c0 = src.charCodeAt(startPos)
  const c1 = src.charCodeAt(startPos + 1)

  let closeStr = ''
  if (c0 === DOLLAR && c1 === DOLLAR) {
    closeStr = '$$'
  } else if (c0 === ESC && c1 === BRACKET_OPEN) {
    closeStr = '\\]'
  } else {
    return false
  }

  if (silent) return true

  const openLen = 2
  const sameLineRest = src.slice(startPos + openLen, lineMax)
  let content = ''
  let nextLine = startLine

  const sameLineCloseIdx = sameLineRest.indexOf(closeStr)
  if (sameLineCloseIdx >= 0) {
    content = sameLineRest.slice(0, sameLineCloseIdx)
    nextLine = startLine + 1
  } else {
    content = sameLineRest + '\n'
    nextLine = startLine + 1
    let foundClose = false
    while (nextLine < endLine) {
      const lineStart = state.bMarks[nextLine] + state.tShift[nextLine]
      const lineEnd = state.eMarks[nextLine]
      const line = src.slice(lineStart, lineEnd)
      const closeIdx = line.indexOf(closeStr)
      if (closeIdx >= 0) {
        content += line.slice(0, closeIdx)
        nextLine++
        foundClose = true
        break
      }
      content += line + '\n'
      nextLine++
    }
    if (!foundClose) return false
  }

  state.line = nextLine
  const token = state.push('math_block', 'math', 0)
  token.block = true
  token.content = content.trim()
  token.markup = closeStr
  token.map = [startLine, nextLine]
  return true
}

export default function katexPlugin(md: MarkdownIt) {
  md.inline.ruler.before('escape', 'math_inline', inlineMath)
  md.block.ruler.after('blockquote', 'math_block', blockMath, {
    alt: ['paragraph', 'reference', 'blockquote', 'list'],
  })
  md.renderer.rules.math_inline = (tokens: any[], idx: number) =>
    renderMath(tokens[idx].content, false)
  md.renderer.rules.math_block = (tokens: any[], idx: number) =>
    `<div class="math-block">${renderMath(tokens[idx].content, true)}</div>`
}
