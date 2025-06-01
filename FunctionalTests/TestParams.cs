using System;
using System.Collections.Generic;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace FunctionalTests;

/// <summary>
/// Test parameters for functional bot testing with improved architecture
/// </summary>
public record TestParams
{
    /// <summary>
    /// Descriptive name for the test case
    /// </summary>
    public string TestName { get; init; }

    /// <summary>
    /// JSON game state file name (relative to GameStates directory)
    /// </summary>
    public required string TestGameStateJsonPath { get; init; }

    /// <summary>
    /// Specific BotId if testing one bot, null for general state validation
    /// </summary>
    public Guid? BotIdToTest { get; init; }

    /// <summary>
    /// Bot nickname to test against (alternative to BotIdToTest)
    /// </summary>
    public string? BotNicknameToTest { get; init; }

    /// <summary>
    /// List of bot specifications to test (for multi-bot testing)
    /// </summary>
    public List<Guid>? BotsToTest { get; init; }

    /// <summary>
    /// Array of bot instances to test
    /// </summary>
    public object[]? BotsArray { get; init; }

    /// <summary>
    /// List of acceptable actions for the test scenario
    /// </summary>
    public required List<BotAction> AcceptableActions { get; init; }

    /// <summary>
    /// Optional: preferred action if there is one most expected result
    /// </summary>
    public BotAction? ExpectedAction { get; init; }

    /// <summary>
    /// Optional: override tick number for the game state
    /// </summary>
    public bool TickOverride { get; init; } = false;

    /// <summary>
    /// Optional: test description
    /// </summary>
    public string? TestDescription { get; init; }
}

/// <summary>
/// Specification for a bot to test
/// </summary>
public class BotTestSpec
{
    /// <summary>
    /// Bot type identifier (e.g., "ClingyHeuroBot2", "ReferenceBot")
    /// </summary>
    public required string BotType { get; set; }

    /// <summary>
    /// Bot nickname for identification
    /// </summary>
    public required string BotNickname { get; set; }

    /// <summary>
    /// Expected action from this specific bot
    /// </summary>
    public BotAction? ExpectedAction { get; set; }

    /// <summary>
    /// Bot ID to use (if specific ID is required)
    /// </summary>
    public Guid? BotId { get; set; }
}
