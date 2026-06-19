import { invoke } from './invoke'

export interface GreetResponse {
  message: string
}

// Command args are keyed by the C# handler's parameter names (here: request).
export const greet = (name: string) => invoke<GreetResponse>('greet', { request: { name } })
