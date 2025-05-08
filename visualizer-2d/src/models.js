// Enums in JavaScript are typically represented as plain objects.
export const CellContent = {
    Empty: 0,
    Wall: 1,
    Pellet: 2,
    ZookeeperSpawn: 3,
    AnimalSpawn: 4,
};

// Interfaces are a TypeScript feature and do not exist in JavaScript.
// The JavaScript code will rely on the objects having the correct properties (duck typing).
// JSDoc comments below describe the expected shape of these objects for documentation.

/**
 * @typedef {object} Cell
 * @property {number} X
 * @property {number} Y
 * @property {number} Content - A value from CellContent (e.g., CellContent.Wall)
 */

/**
 * @typedef {object} Zookeeper
 * @property {string} Id
 * @property {string} NickName
 * @property {number} X
 * @property {number} Y
 * @property {number} SpawnX
 * @property {number} SpawnY
 */

/**
 * @typedef {object} Animal
 * @property {string} Id
 * @property {string} NickName
 * @property {number} X
 * @property {number} Y
 * @property {number} SpawnX
 * @property {number} SpawnY
 * @property {number} Score
 * @property {number} CapturedCounter
 * @property {number} DistanceCovered
 * @property {boolean} IsViable
 */

/**
 * @typedef {object} GameState
 * @property {string} TimeStamp
 * @property {number} Tick
 * @property {Cell[]} Cells
 * @property {Animal[]} Animals
 * @property {Zookeeper[]} Zookeepers
 */

/**
 * @typedef {object} TickState
 * @property {GameState[]} WorldStates
 */

/**
 * InitializeGamePayload is expected to have the same structure as TickState.
 * @typedef {TickState} InitializeGamePayload
 */
