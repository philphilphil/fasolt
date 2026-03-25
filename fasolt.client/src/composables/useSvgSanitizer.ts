import DOMPurify, { type Config } from 'dompurify'

const SVG_CONFIG: Config = {
  USE_PROFILES: { svg: true, svgFilters: true },
  ADD_TAGS: [],
  FORBID_TAGS: ['foreignObject', 'script', 'style'],
  FORBID_ATTR: ['style'],
  ALLOW_DATA_ATTR: false,
}
// Note: xlink:href is NOT forbidden because internal #fragment references
// (e.g., <use href="#id">) are legitimate. The server-side sanitizer
// already strips external href values — this is defense-in-depth only.

export function sanitizeSvg(svg: string): string {
  return DOMPurify.sanitize(svg, SVG_CONFIG) as string
}
