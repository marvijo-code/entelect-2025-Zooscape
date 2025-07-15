# Entelect Challenge 2025 - Zooscape 🦏 - Release 2025.0.0

<!-- IMPORTANT: If you add new H2 or H3 sections, please update the Table of Contents manually -->

- Entelect Challenge 2025 - Zooscape 🦏 - Release 2025.0.0
  - [Table of Contents](#table-of-contents)
  - [Changelog](#change-log)
  - [The Game](#the-game)
  - [General](#general)
  - [Rules](#rules)
    - [Animals](#animals)
    - [Zookeeper](#zookeeper)
    - [The World](#the-world)
    - [Power-ups](#power-ups)
    - [Score Streaks](#score-streaks)
    - [Catch and release](#catch-and-release)
    - [Scoring](#scoring)
  - [Game Ticks](#game-ticks)
  - [Animal Spawning](#animal-spawning)
  - [Commands](#commands)
    - [Command processing order](#command-processing-order)
  - [Win Conditions](#win-conditions)

---

> ## Change log
>
> ### 2025.2.0 - Power ups & new mechanics!
>
> #### :broken_heart: Breaking changes
>
> - **Bot Action:** Added new action `UseItem` with value `5`.
> - **Cell Contents:** Added new cell content types for power-ups:
>   - `PowerPellet` = 5
>   - `ChameleonCloak` = 6
>   - `Scavenger` = 7
>   - `BigMooseJuice` = 8
>
> #### :star2: New features
>
> - **Power-ups:** Introduced several new power-ups. See the [Power-ups](#power-ups) section under Rules for details.
>   - Power pellet
>   - Chameleon Cloak
>   - Scavenger
>   - Big Moose Juice
> - **Score Streaks:** Implemented a score streak mechanic. See the [Score Streaks](#score-streaks) section under Rules for details.
> - **Multiple Zookeepers:** The game now supports multiple zookeepers. See the [Zookeeper](#zookeeper) section under Rules for details.
> - **Pellet Respawning:** Pellets will now respawn during the game. See [The World](#the-world) section under Rules for details.
>
> ### Initial Release
>
> (Details of the initial release, if any, would go here. For now, it marks the first version.)

---

## The Game

---

## General

📒 Note: All configuration is subject to change while we balance the game. For the latest configurations please navigate to ./Zooscape for appsettings.json, appsettings.Development.json and appsettings.Production.json

---

## Rules

### Animals

- Only 1 action is allowed per tick
- Actions are processed in the order they are received
- Actions can be queued, however the queue size is limited
  - The queue size can be viewed in appsettings.json
- Actions **cannot** be cancelled once sent
- When an animal hits a wall or an untraversable space they will remain stationary until a command to change direction is received
- Animals will not collide with each other and can occupy the same space. There is no penalty for being close to or clashing with opponents. The only penalty comes from clashing with a zookeeper.

### Zookeeper

- At the beginning of the game up to 4 zookeepers will spawn throughout the game, presenting an additional challenge!
- The Zookeepers will target the nearest animal and is recalculated every 20 game ticks.
- If two animals are equal distance from the zookeeper, the original viable animal will remain the target.
- When the game starts, the zookeeper will only start moving once there is exactly one viable animal to target. If two or more animals are the same distance from the zookeeper and the closest, none of them are viable targets.
- The zookeeper’s actions will be processed after all animal actions during each tick.

### The World

- Every traversable tile will contain a food pellet except:
  - Animal spawns
  - Zookeeper spawn
- **Pellets will respawn throughout the course of the game, allowing more chances for a comeback.**
- The World will be symmetrical along the x and y axis.
- The Zookeeper spawn will always be the centre tile of the map.

### Power-ups

New power-ups have been added to the game, introducing new strategic elements.
When an animal picks up a power-up item from a cell, it is automatically equipped. Only one power-up can be held at a time; picking up a new one replaces the current one (if the old one was active and had a duration, its effect ends). To activate most collected power-ups, the `UseItem` command must be issued.

- **Power Pellet (`CellContent` ID: 5)**
  - A large pellet that is worth 10 times more than a normal pellet.
  - This is a passive power-up and is consumed immediately upon collection, granting its score benefit instantly. It does not need to be activated with `UseItem` and does not occupy the power-up slot.
- **Chameleon Cloak (`CellContent` ID: 6)**
  - **Effect:** Makes your animal invisible to the zookeeper(s) for 20 ticks upon activation.
  - **Activation:** Use the `UseItem` command.
  - **Note:** If your animal physically bumps into a zookeeper while the cloak is active, you will still be caught.
- **Scavenger (`CellContent` ID: 7)**
  - **Effect:** For 5 ticks after activation, your animal automatically collects all pellets in an 11x11 square area centered on its current position.
  - **Activation:** Use the `UseItem` command.
- **Big Moose Juice (`CellContent` ID: 8)**
  - **Effect:** For 5 ticks after activation, multiplies the value of all pellets your animal picks up by 3.
  - **Activation:** Use the `UseItem` command.

### Score Streaks

- For every consecutive pellet an animal picks up, they gain a streak multiplier. This multiplier increases the point value of every subsequent pellet collected while the streak is active.
- The streak multiplier starts at x1 (normal value) and can increase up to a maximum of x4.
- If an animal does not pick up any pellets for 3 consecutive ticks, its score streak resets to x1.
- The streak bonus applies to the base value of pellets. It also stacks with effects like Big Moose Juice.

### Catch and release

- When the zookeeper occupies the same position in the world as an animal at the end of the tick, the animal has been caught and will be sent back to their cage.
- The animal’s action queue will be cleared when caught.
- There is no limit on the number of times an animal can be caught.
- The animal will not be a viable target for the zookeeper while in the cage but will become one when leaving the cage.
- The animal will start in the cage in an idle state and will only start moving when an action to do so is queued.
- Animals cannot re-enter their cages once they have left, except upon being caught.

### Scoring

- If more than one animal land on the same pellet in the same tick, the animal that sent their command first will get the pellet.

---

## Game Ticks

Zooscape is a real time game that utilises `Ticks`, as a unit of time to keep track of the game.

---

## Animal Spawning

- Every animal will be assigned a spawn point (aka “Cage”) upon joining the game.
- Every animal’s spawn point will be the same distance from the zookeeper’s spawn point.
- When an animal spawns, it starts in an idle state and will be not be targeted by the Zookeeper(s) until it has left its spawn point.
- Once an animal has left its spawn point, it cannot get back in.

---

## Commands

When an animal is spawned, they spawn in an IDLE state on their designated spawn point, and will begin moving as soon as the first command of movement is sent. Animals may traverse the map via basic movement **or use collected power-ups**.

Note: Animals have continuous movement and will move in their last direction until a new command is issued or it is at the edge of the map where the animal will stop.

The following commands are available:

- `UP` - 1
- `DOWN` - 2
- `LEFT` - 3
- `RIGHT` - 4
- `UseItem` - 5 (Activates the currently held power-up)

### Command Processing Order

The Game Engine maintains a command queue for each animal. An animal can issue a command at any point which will then be added to that animal's command queue. With each game tick the Game Engine pops the first command off of each animal's queue and orders them by received timestamp. The commands are then processed in that order.

---

## Win Conditions

- When the timer runs out or all the food pellets have been collected, the animal with the highest score wins. If there are any ties the engine will calculate the winner using the following order of tie breakers:
  1. Highest Score
  2. Fewest times captured
  3. Least time spent on spawn
  4. Farthest distance travelled
  5. First command issued
- When an animal is captured they will lose a percentage of their score, the percentage is set in appsettings.json.
