using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FunctionalTests.Models;
using Serilog;

namespace FunctionalTests.Services;

public class TestDefinitionDataSource : IEnumerable<object[]>
{
    private readonly List<object[]> _data;

    public TestDefinitionDataSource()
    {
        var logger = Log.Logger ?? new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var loader = new TestDefinitionLoader(logger);
        var definitions = loader.LoadAllTestDefinitions();
        _data = definitions.Select(def => new object[] { def }).ToList();
    }

    public IEnumerator<object[]> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
