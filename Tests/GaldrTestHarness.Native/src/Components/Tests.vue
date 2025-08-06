<template>
    <div class="wrapper">
        <!--<form class="row" @submit.prevent="runTests">
            <h1 class="title">Test Runner</h1>
            <button class="test-button" type="submit" :disabled="testsRunning">Start Tests</button>
        </form>-->


        <form class="row" @submit.prevent="testCommands">
            <h1 class="title">Test Commands</h1>
            <button class="test-button" type="submit" :disabled="testsRunning">Test Commands</button>
        </form>

        <div ref="outputConsole" class="console">
            <pre v-for="item in testResults">{{ item }}</pre>
        </div>
    </div>
</template>

<script setup>
    import { ref, onMounted, watch } from "vue";

    const testResults = ref([]);
    const outputConsole = ref();
    const testsRunning = ref(false);

    onMounted(() => {
        window.addEventListener(
            "unitTestResults",
            (e) => {
                testResults.value.push(e.detail);
            },
            false,
        );
    });

    watch(testResults, () => {
        outputConsole.value.scrollTo(0, outputConsole.value.scrollHeight);
    }, { flush: 'post', deep: true });

    async function runTests() {
        testsRunning.value = true;
        testResults.value = ["Running Unit Tests...", " "];

        await galdrInvoke("runAllUnitTests");

        testResults.value.push(" ");
        testResults.value.push("Unit Tests Complete.");
        testResults.value.push(" ");
        testResults.value.push("Running Integration Tests...");
        testResults.value.push(" ");

        await runTest("testingMethod");
        await runTest("testingMethodInt", { x: 10 });
        await runTest("testingMethodInts", { x: 10, y: 20 });
        await runTest("testingMethodString", { name: "testing string" });
        await runTest("testingMethodStrings", { name: "some name", address: "some address" });
        await runTest("testingMethodGuid", { id: "D5E84F46-9D18-42FE-A3D3-BCFB28794812" });
        await runTest("testingMethodGuids", { id1: "{0DD17A8F-7B10-4018-A44B-76D7D4AB5740}", id2: "D5E84F46-9D18-42FE-A3D3-BCFB28794812" });
        await runTest("testingMethodDateTime", { dateTime1: new Date() });
        await runTest("testingMethodDateTimes", { dateTime1: new Date(), dateTime2: new Date() });
        await runTest("uTF8Test");
        await runTest("modelParameterTest", { model: { id: 1, name: "name of model" } });
        await runTest("modelParameterTest", { id: 1, name: "name of model" });
        await runTest("modelReturnTest");
        await runTest("dITest");
        await runTest("dynamicTest", { param: "dynamic string" });
        await runTest("testCommands.PrefixTest");

        testResults.value.push(" ");
        testResults.value.push("Integration Tests Complete.");
        testResults.value.push(" ");
        testResults.value.push("Done!");

        testsRunning.value = false;
    }

    async function runTest(testName, args) {
        const startTime = performance.now();
        var result = args ? await galdrInvoke(testName, args) : await galdrInvoke(testName);
        const endTime = performance.now();
        const time = endTime - startTime;

        if (testName === "modelReturnTest") {
            result = result.Id > 0 && result.Name.trim() !== ""
        }

        testResults.value.push((result ? "Passed " : "Failed ") + testName + " [" + time.toFixed(2) + " ms]");
    }

    async function testCommands() {
        let result = await galdrInvoke("commandTest1");
        console.log(result);

        let result2 = await galdrInvoke("commandTest2", { x: 10 });
        console.log(result2);

        let result3 = await galdrInvoke("commandTest3", { x: 69 });
        console.log(result3);
        testResults.value.push(result3);

        let result4 = await galdrInvoke("commandTest4", { count: 3, request: { id: "9DB5BDD9-1249-41ED-B298-68AE417DD3DB", name: "Print me!" } });
        console.log(result4);
        testResults.value.push(result4);
    }
</script>

<style scoped>
    .wrapper {
        flex: 1 1 1px;
        display: flex;
        flex-direction: column;
        width: 100%;
    }

    .title {
        flex-grow: 1;
        margin-right: 1em;
        margin-left: 1em;
        text-align: left;
    }

    .test-button {
        margin-right: 1em;
        margin-top: 0.5em;
    }

    .console {
        background-color: #0d0c0c;
        margin-top: 1em;
        padding: 1em;
        overflow-y: auto;
        overflow-x: auto;
        scroll-margin: 2em;
        flex: 1 1 1px;
    }

        .console > pre {
            margin: 0;
        }
</style>
