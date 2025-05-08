using Zooscape.Application.Services;

namespace Zooscape.Infrastructure.SignalRHub.Models;

public class GameState
{
    public DateTime TimeStamp { get; set; }
    public int Tick { get; set; }
    public List<Cell> Cells { get; set; }
    public List<Animal> Animals { get; set; }
    public List<Zookeeper> Zookeepers { get; set; }

    public GameState(IGameStateService gameState)
    {
        TimeStamp = DateTime.UtcNow;
        Tick = gameState.TickCounter;
        Cells = gameState
            .World.Cells.OfType<Domain.Enums.CellContents>()
            .SelectMany(
                (_, index) =>
                {
                    int x = index / gameState.World.Cells.GetLength(1);
                    int y = index % gameState.World.Cells.GetLength(1);
                    return new[]
                    {
                        new Cell()
                        {
                            X = x,
                            Y = y,
                            Content = (CellContent)gameState.World.Cells[x, y],
                        },
                    };
                }
            )
            .ToList();

        Animals = gameState
            .Animals.Values.Select(a => new Animal()
            {
                Id = a.Id,
                Nickname = a.Nickname,
                X = a.Location.X,
                Y = a.Location.Y,
                SpawnX = a.SpawnPoint.X,
                SpawnY = a.SpawnPoint.Y,
                Score = a.Score,
                CapturedCounter = a.CapturedCounter,
                DistanceCovered = a.DistanceCovered,
                IsViable = a.IsViable,
            })
            .ToList();

        Zookeepers = gameState
            .Zookeepers.Values.Select(z => new Zookeeper()
            {
                Id = z.Id,
                Nickname = z.Nickname,
                X = z.Location.X,
                Y = z.Location.Y,
                SpawnX = z.SpawnPoint.X,
                SpawnY = z.SpawnPoint.Y,
            })
            .ToList();
    }
}
