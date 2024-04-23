<template>
    <Tests />
</template>

<script setup>
    import { onMounted } from "vue";
    import Tests from "./Components/Tests.vue";
</script>

<style>
    html, body {
        display: flex;
        flex-direction: column;
        height: 100%;
        width: 100%;
        max-width: 100%;
        overflow: hidden;
        padding: 0;
        margin: 0;
    }

    #app {
        display: flex;
        max-width: 100%;
        max-height: 100%;
        overflow: hidden;
        flex: 1 1 1px;
    }

    .container {
        margin: 0;
        padding: 1em 1.75em 1em 1em;
        flex: 1 1 1px;
        display: flex;
        flex-direction: column;
        max-width: 100%;
    }
</style>