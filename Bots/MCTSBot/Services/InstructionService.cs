using System;
using System.Linq;
using MCTSBot.Enums;
using MCTSBot.Models;

namespace MCTSBot.Services;

public class InstructionService
{
    private Guid _botId; // Set upon registration

    public void SetBotId(Guid botId)
    {
        _botId = botId;
    }

    /// <summary>
    /// Translates the game state received from the engine into the MCTS-specific game state.
    /// This is a crucial step and will require detailed mapping based on actual game data.
    /// </summary>
    public MCTSGameState TranslateToMCTSState(EngineGameState engineState)
    {
        if (engineState == null)
        {
            // Or throw an ArgumentNullException, or return a default error state
            Console.WriteLine(
                "TranslateToMCTSState received null engineState. Returning a minimal valid state."
            );
            return new MCTSGameState(
                new int[1, 1]
                {
                    { MCTSGameState.CellTypeEmpty },
                },
                0,
                0,
                0,
                1,
                -1,
                -1
            ); // Minimal valid state
        }

        // Check for invalid map dimensions from the engine
        if (engineState.MapHeight <= 0 || engineState.MapWidth <= 0)
        {
            Console.WriteLine(
                $"Warning: Engine state has invalid map dimensions ({engineState.MapHeight}x{engineState.MapWidth}). Tick: {engineState.Tick}. Returning a minimal terminal state."
            );
            // Create a state that's effectively terminal: 1x1 map, player at (0,0), score from engine, MaxTicksForSimulation = 0.
            // This ensures IsTerminal() will likely be true from the start for the MCTS simulation.
            return new MCTSGameState(
                new int[1, 1]
                {
                    { MCTSGameState.CellTypeEmpty },
                },
                0,
                0,
                0,
                0,
                -1,
                -1
            );
        }

        int mapHeight = engineState.MapHeight;
        int mapWidth = engineState.MapWidth;
        var mctsMapData = new int[mapHeight, mapWidth];

        for (int y = 0; y < mapHeight; y++)
        {
            if (y >= engineState.Map.Count || engineState.Map[y] == null)
            {
                Console.WriteLine($"Warning: Map data missing for row {y}.");
                for (int x = 0; x < mapWidth; x++)
                {
                    mctsMapData[y, x] = MCTSGameState.CellTypeEmpty; // Default to empty
                }
                continue;
            }
            string row = engineState.Map[y];
            for (int x = 0; x < mapWidth; x++)
            {
                if (x >= row.Length)
                {
                    Console.WriteLine($"Warning: Map data missing for cell ({x},{y}).");
                    mctsMapData[y, x] = MCTSGameState.CellTypeEmpty; // Default to empty
                    continue;
                }
                char cellChar = row[x];
                switch (cellChar)
                {
                    case 'W': // Wall
                        mctsMapData[y, x] = MCTSGameState.CellTypeWall;
                        break;
                    case 'P': // Pellet
                        mctsMapData[y, x] = MCTSGameState.CellTypePellet;
                        break;
                    case 'U': // PowerUp
                        mctsMapData[y, x] = MCTSGameState.CellTypePowerUp;
                        break;
                    case 'X': // EscapeZone
                        mctsMapData[y, x] = MCTSGameState.CellTypeEscapeZone;
                        break;
                    // Zookeepers are handled by coordinates, not map character initially for MCTSGameState model
                    case '.': // Empty
                    case ' ': // Also Empty
                    default:
                        mctsMapData[y, x] = MCTSGameState.CellTypeEmpty;
                        break;
                }
            }
        }

        int playerX = engineState.MyBot?.X ?? 0;
        int playerY = engineState.MyBot?.Y ?? 0;
        int playerScore = engineState.MyBot?.Score ?? 0;

        int zookeeperX = -1;
        int zookeeperY = -1;
        var firstZookeeper = engineState.Zookeepers?.FirstOrDefault();
        if (firstZookeeper != null)
        {
            zookeeperX = firstZookeeper.X;
            zookeeperY = firstZookeeper.Y;
        }

        // MaxTicksForSimulation for MCTS is the remaining ticks in the game.
        int maxTicksForSimulation = engineState.MaxGameTicks - engineState.Tick;
        if (maxTicksForSimulation <= 0)
        {
            // Game is over or no time left, ensure at least 1 tick for MCTS internal logic if needed
            // or handle as terminal. MCTSGameState handles terminal checks based on its own rules.
            Console.WriteLine(
                "Warning: MaxGameTicks <= engineState.Tick. Simulation time might be zero or negative."
            );
            maxTicksForSimulation = 1; // Give it at least one conceptual tick if game isn't over by other means
        }

        return new MCTSGameState(
            mctsMapData,
            playerX,
            playerY,
            playerScore,
            maxTicksForSimulation, // This is the 'horizon' for the MCTS simulation runs
            zookeeperX,
            zookeeperY
        );
    }

    /// <summary>
    /// Translates the MCTS-chosen GameAction into an EngineBotCommand.
    /// </summary>
    public EngineBotCommand CreateBotCommand(GameAction mctsAction)
    {
        EngineBotAction engineAction;
        switch (mctsAction)
        {
            case GameAction.MoveUp:
                engineAction = EngineBotAction.Up;
                break;
            case GameAction.MoveDown:
                engineAction = EngineBotAction.Down;
                break;
            case GameAction.MoveLeft:
                engineAction = EngineBotAction.Left;
                break;
            case GameAction.MoveRight:
                engineAction = EngineBotAction.Right;
                break;
            case GameAction.DoNothing:
            default:
                engineAction = EngineBotAction.DoNothing;
                break;
        }
        // SequenceNumber or other fields might be needed.
        // For now, assuming BotId is set during registration and action is primary.
        Console.WriteLine(
            $"Creating EngineBotCommand for MCTSAction: {mctsAction} -> EngineAction: {engineAction}"
        );
        return new EngineBotCommand(_botId, engineAction);
    }
}
