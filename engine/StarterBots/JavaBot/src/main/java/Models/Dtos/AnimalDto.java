package Models.Dtos;

import Enums.PowerUpType;

import java.util.UUID;

public class AnimalDto {
    public UUID Id;
    public int x;
    public int y;
    public int spawnX;
    public int spawnY;
    public int score;
    public int capturedCounter;
    public int distanceCovered;
    public boolean isViable;
    public PowerUpType heldPowerUp;

    public ActivePowerUpDto activePowerUp;

    public UUID getId() {
        return Id;
    }

    public void setId(UUID id) {
        Id = id;
    }

    public int getX() {
        return x;
    }

    public void setX(int x) {
        this.x = x;
    }

    public int getY() {
        return y;
    }

    public void setY(int y) {
        this.y = y;
    }

    public int getSpawnX() {
        return spawnX;
    }

    public void setSpawnX(int spawnX) {
        this.spawnX = spawnX;
    }

    public int getSpawnY() {
        return spawnY;
    }

    public void setSpawnY(int spawnY) {
        this.spawnY = spawnY;
    }

    public int getScore() {
        return score;
    }

    public void setScore(int score) {
        this.score = score;
    }

    public int getCapturedCounter() {
        return capturedCounter;
    }

    public void setCapturedCounter(int capturedCounter) {
        this.capturedCounter = capturedCounter;
    }

    public int getDistanceCovered() {
        return distanceCovered;
    }

    public void setDistanceCovered(int distanceCovered) {
        this.distanceCovered = distanceCovered;
    }

    public boolean isViable() {
        return isViable;
    }

    public void setViable(boolean viable) {
        isViable = viable;
    }

    public ActivePowerUpDto getActivePowerUp() {
        return activePowerUp;
    }

    public void setActivePowerUp(ActivePowerUpDto activePowerUp) {
        this.activePowerUp = activePowerUp;
    }

    public PowerUpType getHeldPowerUp() {
        return heldPowerUp;
    }

    public void setHeldPowerUp(PowerUpType heldPowerUp) {
        this.heldPowerUp = heldPowerUp;
    }
}
