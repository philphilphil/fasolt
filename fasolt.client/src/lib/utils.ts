import type { Ref } from 'vue'
import type { Updater } from '@tanstack/vue-table'
import type { ClassValue } from 'clsx'
import { clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function stripMarkdown(text: string): string {
  return text
    .replace(/^```\w*\n?/gm, '')       // code block fences (open)
    .replace(/\n?```$/gm, '')           // code block fences (close)
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1') // images → alt text
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')  // links → text
    .replace(/^#{1,6}\s+/gm, '')        // heading markers
    .replace(/\*\*(.+?)\*\*/g, '$1')    // bold **
    .replace(/__(.+?)__/g, '$1')        // bold __
    .replace(/\*(.+?)\*/g, '$1')        // italic *
    .replace(/(?<!\w)_(.+?)_(?!\w)/g, '$1') // italic _
    .replace(/`([^`]+)`/g, '$1')        // inline code
    .replace(/^>\s+/gm, '')             // blockquotes
    .replace(/^[-*]\s+/gm, '')          // unordered list markers
    .replace(/^\d+\.\s+/gm, '')         // ordered list markers
    .replace(/~~(.+?)~~/g, '$1')        // strikethrough
    .replace(/ {2,}/g, ' ')             // collapse spaces
    .trim()
}

export function valueUpdater<T extends Updater<any>>(updaterOrValue: T, ref: Ref) {
  ref.value = typeof updaterOrValue === 'function' ? updaterOrValue(ref.value) : updaterOrValue
}
