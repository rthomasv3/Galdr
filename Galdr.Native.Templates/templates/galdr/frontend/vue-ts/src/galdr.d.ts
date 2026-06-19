export {}

// The global bridge function injected by Galdr into the page.
declare global {
  function galdrInvoke<T = unknown>(command: string, args?: unknown): Promise<T>
}
