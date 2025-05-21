using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.FunctionalTests;
using Serilog;
using Xunit;

namespace FunctionalTests;

public class AllBotsTests
{
    private readonly BotTestRunner _testRunner;
    private readonly ILogger _logger;

    public AllBotsTests()
    {
        // Setup basic Serilog logger for tests if not already configured globally
        // You might want a more sophisticated setup or use a test-specific logger config
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            //.WriteTo.Console()
            //.WriteTo.File("FunctionalTests-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // If Xunit has a built-in way to capture output, prefer that.
        // For example, using ITestOutputHelper if preferred.
        // _logger = Substitute.For<ILogger>(); // Or use NSubstitute for more controlled logging tests

        _testRunner = new BotTestRunner(_logger);
        _logger.Information("AllBotsTests initialized.");
    }

    [Theory]
    [ClassData(typeof(TestDataSource))]
    public void RunBotTest(IBot<GameState> bot, FunctionalTestParams testParams)
    {
        if (bot == null)
        {
            _logger.Warning(
                $"Test skipped: Bot instance is null for test case based on {testParams.TestName} and file {testParams.TestFileName}."
            );
            // This might happen if BotFactory couldn't instantiate a bot for some reason
            // and TestDataSource yielded null for the bot.
            // Assert.Fail will make the test runner show an error.
            // Alternatively, you can use Assert.Skip or just return if this is an expected scenario (e.g. bot not meant to be tested).
            Assert.Fail(
                "Test case received a null bot instance. Check BotFactory and TestDataSource."
            );
            return;
        }

        _logger.Information(
            $"Executing test: {testParams.TestName} for bot: {bot.GetType().Name} with file: {testParams.TestFileName}"
        );
        try
        {
            _testRunner.RunTest(bot, testParams);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                $"Exception during test execution for {testParams.TestName} with bot {bot.GetType().Name}."
            );
            // Re-throw or Assert.Fail to ensure the test runner catches this as a failure.
            // Assert.Fail will include the exception message, which is good.
            Assert.Fail(
                $"Test {testParams.TestName} for bot {bot.GetType().Name} failed with exception: {ex.Message} - StackTrace: {ex.StackTrace}"
            );
        }
    }
}
