package Enums;

public enum CellContent {
    EMPTY(0),
    WALL(1),
    PELLET(2),
    ZOOKEEPER_SPAWN(3),
    ANIMAL_SPAWN(4),
    POWER_PELLET(5),
    CHAMELEON_CLOAK(6),
    SCAVENGER(7),
    BIG_MOOSE_JUICE(8);

    private final int value;

    CellContent(int value) {
        this.value = value;
    }

    public int getValue() {
        return value;
    }
}
