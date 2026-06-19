<script setup>
import { useThemeStore } from './stores/themeStore'

useThemeStore().init()
</script>

<template>
  <router-view />
</template>
