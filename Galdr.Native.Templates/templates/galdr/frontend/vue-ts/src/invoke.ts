// Wrapper around the global galdrInvoke injected by Galdr.
export async function invoke<T = unknown>(command: string, args?: unknown): Promise<T> {
  try {
    return args === undefined
      ? await galdrInvoke<T>(command)
      : await galdrInvoke<T>(command, args)
  } catch (err) {
    console.error(`galdrInvoke('${command}') failed:`, err)
    throw err
  }
}
