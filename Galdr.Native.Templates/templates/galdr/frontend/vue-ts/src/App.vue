<script setup lang="ts">
import { ref } from 'vue'
import { greet } from './greetingService'

const name = ref('')
const message = ref('')
const busy = ref(false)

async function sayHello() {
  busy.value = true
  try {
    const response = await greet(name.value)
    message.value = response.message
  } catch (err) {
    message.value = `Error: ${err instanceof Error ? err.message : String(err)}`
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="app">
    <h1>Galdr Greeter</h1>

    <form class="row" @submit.prevent="sayHello">
      <input v-model="name" type="text" placeholder="Type your name..." autofocus />
      <button type="submit" :disabled="busy">Greet</button>
    </form>

    <p class="result" v-if="message">{{ message }}</p>
  </main>
</template>

<style>
:root {
  color-scheme: light dark;
}

body {
  margin: 0;
  font-family: system-ui, -apple-system, sans-serif;
}

.app {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1.5rem;
  padding: 2rem;
  box-sizing: border-box;
}

h1 {
  margin: 0;
  font-size: 1.6rem;
  font-weight: 600;
}

.row {
  display: flex;
  gap: 0.5rem;
}

input {
  padding: 0.55rem 0.75rem;
  font-size: 1rem;
  border: 1px solid #8884;
  border-radius: 0.5rem;
  min-width: 14rem;
}

button {
  padding: 0.55rem 1.1rem;
  font-size: 1rem;
  font-weight: 600;
  border: none;
  border-radius: 0.5rem;
  background: #4f46e5;
  color: #fff;
  cursor: pointer;
}

button:disabled {
  opacity: 0.6;
  cursor: default;
}

.result {
  margin: 0;
  font-size: 1.4rem;
  font-weight: 500;
}
</style>
