package Enums;

public enum BotAction {
    UP(1),
    DOWN(2),
    LEFT(3),
    RIGHT(4),
    USE_ITEM(5);

    private final int value;

    BotAction(int value) {
        this.value = value;
    }

    public int getValue() {
        return value;
    }
}
