// Enums in JavaScript are typically represented as plain objects.
export const CellContent = {
    Empty: 0,
    Wall: 1,
    Pellet: 2,
    AnimalSpawn: 3, // Note: This is a legacy value, ZookeeperSpawn is 4 in the backend
    ZookeeperSpawn: 4,
    PowerPellet: 5,
    ChameleonCloak: 6,
    Scavenger: 7,
    BigMooseJuice: 8,
};

// Interfaces are a TypeScript feature and do not exist in JavaScript.
// The JavaScript code will rely on the objects having the correct properties (duck typing).
// JSDoc comments below describe the expected shape of these objects for documentation.

/**
 * @typedef {object} Cell
 * @property {number} X - or lowercase x
 * @property {number} Y - or lowercase y
 * @property {number} Content - A value from CellContent (e.g., CellContent.Wall) or lowercase content
 */

/**
 * @typedef {object} Zookeeper
 * @property {string} Id - or lowercase id
 * @property {string} Nickname - or lowercase nickname
 * @property {number} X - or lowercase x
 * @property {number} Y - or lowercase y
 * @property {number} SpawnX - or lowercase spawnX
 * @property {number} SpawnY - or lowercase spawnY
 */

/**
 * @typedef {object} Animal
 * @property {string} Id - or lowercase id
 * @property {string} Nickname - or lowercase nickname
 * @property {number} X - or lowercase x
 * @property {number} Y - or lowercase y
 * @property {number} SpawnX - or lowercase spawnX
 * @property {number} SpawnY - or lowercase spawnY
 * @property {number} Score - or lowercase score
 * @property {number} CapturedCounter - or lowercase capturedCounter
 * @property {number} DistanceCovered - or lowercase distanceCovered
 * @property {boolean} IsViable - or lowercase isViable
 * @property {number} [HeldPowerUp] - or lowercase heldPowerUp (a CellContent value)
 * @property {number} [ActivePowerUp] - or lowercase activePowerUp (a CellContent value)
 * @property {number} [PowerUpDuration] - or lowercase powerUpDuration
 * @property {number} [ScoreStreak] - or lowercase scoreStreak
 */

/**
 * @typedef {object} GameState
 * @property {string} TimeStamp - or lowercase timeStamp
 * @property {number} Tick - or lowercase tick
 * @property {Cell[]} Cells - or lowercase cells
 * @property {Animal[]} Animals - or lowercase animals
 * @property {Zookeeper[]} Zookeepers - or lowercase zookeepers
 */

/**
 * @typedef {object} TickState
 * @property {GameState[]} WorldStates - or lowercase worldStates
 */

/**
 * InitializeGamePayload is expected to have the same structure as TickState.
 * @typedef {TickState} InitializeGamePayload
 */
