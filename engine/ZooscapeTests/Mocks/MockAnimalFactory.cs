using System;
using Zooscape.Domain.Interfaces;
using Zooscape.Domain.Models;
using Zooscape.Domain.ValueObjects;

namespace ZooscapeTests.Mocks;

class MockAnimalFactory : IAnimalFactory
{
    public Animal CreateAnimal(Guid id, string nickname, GridCoords spawnPoint, int queueSize)
    {
        return new Animal(id, nickname, spawnPoint, 1, 0.0, 1.0, 0);
    }
}
