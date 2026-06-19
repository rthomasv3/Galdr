export const isMac = navigator.userAgent.toLowerCase().includes('mac')

const PARTS = isMac
  ? { mod: '⌘', alt: '⌥', shift: '⇧', up: '↑', down: '↓', enter: '⏎', esc: 'Esc' }
  : { mod: 'Ctrl', alt: 'Alt', shift: 'Shift', up: '↑', down: '↓', enter: 'Enter', esc: 'Esc' }

export function shortcut(...parts) {
  const sep = isMac ? '' : '+'
  return parts.map(p => PARTS[p] ?? p).join(sep)
}
