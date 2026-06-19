import { useState, type FormEvent } from 'react'
import { greet } from './greetingService'

export default function App() {
  const [name, setName] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  async function sayHello(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      const response = await greet(name)
      setMessage(response.message)
    } catch (err) {
      setMessage(`Error: ${err instanceof Error ? err.message : String(err)}`)
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="app">
      <h1>Galdr Greeter</h1>

      <form className="row" onSubmit={sayHello}>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          type="text"
          placeholder="Type your name..."
          autoFocus
        />
        <button type="submit" disabled={busy}>Greet</button>
      </form>

      {message && <p className="result">{message}</p>}
    </main>
  )
}
