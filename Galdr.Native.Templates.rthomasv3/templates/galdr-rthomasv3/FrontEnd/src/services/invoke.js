export async function invoke(command, args) {
  try {
    return args === undefined
      ? await galdrInvoke(command)
      : await galdrInvoke(command, args)
  } catch (err) {
    console.error(`galdrInvoke '${command}' failed:`, err)
    throw err
  }
}
