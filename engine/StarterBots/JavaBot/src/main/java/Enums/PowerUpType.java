package Enums;

public enum PowerUpType {
    POWER_PELLET(0),
    CHAMELEON_CLOAK(1),
    SCAVENGER(2),
    BIG_MOOSE_JUICE(3);

    private final int value;

    PowerUpType(int value) {
        this.value = value;
    }

    public int getValue() {
        return value;
    }
}
