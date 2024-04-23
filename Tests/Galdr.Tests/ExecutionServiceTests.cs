using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Galdr.Tests;

[TestClass]
public class ExecutionServiceTests
{
    [TestMethod]
    public void ExtractArguments_ParametersNull_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods().FirstOrDefault();
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());

        object[] args = executionService.ExtractArguments(methodInfo, null);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 0);
    }

    [TestMethod]
    public void ExtractArguments_ParametersEmpty_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods().FirstOrDefault();
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());

        object[] args = executionService.ExtractArguments(methodInfo, Enumerable.Empty<object>());

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 0);
    }

    [TestMethod]
    public void ExtractArguments_IntParameter_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods()[1];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>("[ { x: 4 } ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 1);
        Assert.IsTrue((int)args[0] == 4);
    }

    [TestMethod]
    public void ExtractArguments_IntParameters_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods()[2];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>("[ { x: 4, y: 6 } ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 2);
        Assert.IsTrue((int)args[0] == 4);
        Assert.IsTrue((int)args[1] == 6);
    }

    [TestMethod]
    public void ExtractArguments_StringParameter_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods()[3];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>("[ { name: \"test\" } ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 1);
        Assert.IsTrue((string)args[0] == "test");
    }

    [TestMethod]
    public void ExtractArguments_StringParameters_EmptyCollectionReturned()
    {
        MethodInfo methodInfo = typeof(Testing).GetMethods()[4];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>("[ { name: \"test\", address: \"test2\" } ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 2);
        Assert.IsTrue((string)args[0] == "test");
        Assert.IsTrue((string)args[1] == "test2");
    }

    [TestMethod]
    public void ExtractArguments_GuidParameter_EmptyCollectionReturned()
    {
        Guid id = Guid.NewGuid();
        MethodInfo methodInfo = typeof(Testing).GetMethods()[5];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>($"[ {{ \"id\": \"{ id }\" }} ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 1);
        Assert.IsTrue((Guid)args[0] == id);
    }

    [TestMethod]
    public void ExtractArguments_GuidParameters_EmptyCollectionReturned()
    {
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        MethodInfo methodInfo = typeof(Testing).GetMethods()[6];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>($"[ {{ \"id1\": \"{id1}\", \"id2\": \"{id2}\" }} ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 2);
        Assert.IsTrue((Guid)args[0] == id1);
        Assert.IsTrue((Guid)args[1] == id2);
    }

    [TestMethod]
    public void ExtractArguments_DateTimeParameter_EmptyCollectionReturned()
    {
        DateTime time1 = DateTime.Now;
        MethodInfo methodInfo = typeof(Testing).GetMethods()[7];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>($"[ {{ \"dateTime1\": \"{time1}\" }} ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 1);
        Assert.IsTrue(((DateTime)args[0]).Minute == time1.Minute);
    }

    [TestMethod]
    public void ExtractArguments_DateTimeParameters_EmptyCollectionReturned()
    {
        DateTime time1 = DateTime.Now;
        DateTime time2 = DateTime.Now + TimeSpan.FromMinutes(1);
        MethodInfo methodInfo = typeof(Testing).GetMethods()[8];
        ServiceCollection services = new();
        ExecutionService executionService = new(services.BuildServiceProvider());
        object[] parameters = JsonConvert.DeserializeObject<object[]>($"[ {{ \"dateTime1\": \"{time1}\", \"dateTime2\": \"{time2}\" }} ]");

        object[] args = executionService.ExtractArguments(methodInfo, parameters);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length == 2);
        Assert.IsTrue(((DateTime)args[0]).Minute == time1.Minute);
        Assert.IsTrue(((DateTime)args[1]).Minute == time2.Minute);
    }
}

public abstract class Testing
{
    public abstract void TestingMethod();
    public abstract void TestingMethodInt(int x);
    public abstract void TestingMethodInts(int x, int y);
    public abstract void TestingMethodString(string name);
    public abstract void TestingMethodStrings(string name, string address);
    public abstract void TestingMethodGuid(Guid id);
    public abstract void TestingMethodGuids(Guid id1, Guid id2);
    public abstract void TestingMethodDateTime(DateTime dateTime1);
    public abstract void TestingMethodDateTimes(DateTime dateTime1, DateTime dateTime2);
}
