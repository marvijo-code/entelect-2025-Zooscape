using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace Marvijo.Zooscape.Bots.Common
{
    public class HeuristicContext : IHeuristicContext
    {
        public GameState CurrentGameState { get; }
        public Animal CurrentAnimal { get; }
        public BotAction CurrentMove { get; }
        public ILogger? Logger { get; }
        public (int X, int Y) MyNewPosition { get; }
        public BotAction? PreviousAction { get; } // Can be expanded later

        public HeuristicContext(
            GameState currentGameState,
            Animal currentAnimal,
            BotAction currentMove,
            ILogger? logger,
            BotAction? previousAction = null
        )
        {
            CurrentGameState = currentGameState;
            CurrentAnimal = currentAnimal;
            CurrentMove = currentMove;
            Logger = logger;
            PreviousAction = previousAction;

            MyNewPosition = BotUtils.ApplyMove(CurrentAnimal.X, CurrentAnimal.Y, CurrentMove);
        }

        // Stubbed implementations for other interface members
        public int GetVisitCount((int X, int Y) position)
        {
            // TODO: Implement actual visit count tracking if needed
            return 0;
        }

        public bool IsQuadrantVisited(int quadrant)
        {
            // TODO: Implement actual quadrant visit tracking if needed
            return false;
        }
    }
}
