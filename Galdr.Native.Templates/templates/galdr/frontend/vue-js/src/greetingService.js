import { invoke } from './invoke'

// Command args are keyed by the C# handler's parameter names (here: request).
export const greet = (name) => invoke('greet', { request: { name } })
