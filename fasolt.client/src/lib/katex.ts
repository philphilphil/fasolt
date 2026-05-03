import katex from 'katex'
import 'katex/contrib/mhchem'

const macros: Record<string, string> = {
  '\\dv': '\\frac{d#1}{d#2}',
  '\\pdv': '\\frac{\\partial #1}{\\partial #2}',
  '\\abs': '\\left|#1\\right|',
  '\\norm': '\\left\\|#1\\right\\|',
}

export function renderMath(src: string, displayMode: boolean): string {
  return katex.renderToString(src, {
    displayMode,
    throwOnError: false,
    errorColor: 'hsl(var(--destructive))',
    strict: 'warn',
    trust: false,
    maxSize: 10,
    maxExpand: 1000,
    macros,
    output: 'html',
  })
}
