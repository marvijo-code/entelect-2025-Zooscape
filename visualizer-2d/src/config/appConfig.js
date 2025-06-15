// Application configuration management
// Handles environment variables and user preferences

const DEFAULT_CONFIG = {
    defaultReplayMode: true,
    defaultActiveTab: 2,
    hubUrl: "http://localhost:5000/bothub",
    apiBaseUrl: "http://localhost:5008/api",
    debugLogs: false
};

// Get configuration from environment variables with fallbacks
export const getEnvConfig = () => {
    return {
        defaultReplayMode: import.meta.env.VITE_DEFAULT_REPLAY_MODE === 'true',
        defaultActiveTab: parseInt(import.meta.env.VITE_DEFAULT_ACTIVE_TAB || '2', 10),
        hubUrl: import.meta.env.VITE_HUB_URL || DEFAULT_CONFIG.hubUrl,
        apiBaseUrl: import.meta.env.VITE_API_BASE_URL || DEFAULT_CONFIG.apiBaseUrl,
        debugLogs: import.meta.env.VITE_DEBUG_LOGS === 'true'
    };
};

// Local storage keys for user preferences
const STORAGE_KEYS = {
    USER_PREFERENCES: 'zooscape_user_preferences',
    DEFAULT_MODE: 'zooscape_default_mode',
    DEFAULT_TAB: 'zooscape_default_tab'
};

// Get user preferences from localStorage
export const getUserPreferences = () => {
    try {
        const stored = localStorage.getItem(STORAGE_KEYS.USER_PREFERENCES);
        return stored ? JSON.parse(stored) : {};
    } catch (error) {
        console.warn('Failed to load user preferences:', error);
        return {};
    }
};

// Save user preferences to localStorage
export const saveUserPreferences = (preferences) => {
    try {
        const current = getUserPreferences();
        const updated = { ...current, ...preferences };
        localStorage.setItem(STORAGE_KEYS.USER_PREFERENCES, JSON.stringify(updated));
        return true;
    } catch (error) {
        console.error('Failed to save user preferences:', error);
        return false;
    }
};

// Get the final configuration (env + user preferences)
export const getAppConfig = () => {
    const envConfig = getEnvConfig();
    const userPrefs = getUserPreferences();

    return {
        ...envConfig,
        ...userPrefs
    };
};

// Reset user preferences to environment defaults
export const resetToDefaults = () => {
    try {
        localStorage.removeItem(STORAGE_KEYS.USER_PREFERENCES);
        return true;
    } catch (error) {
        console.error('Failed to reset preferences:', error);
        return false;
    }
};

// Validate tab index based on mode
export const getValidTabIndex = (tabIndex, isReplayMode) => {
    // Settings tab (4) is always available
    if (tabIndex === 4) return 4;

    if (isReplayMode) {
        // Replay mode: 0=Leaderboard, 1=JSON Paste, 2=Game Selector, 3=Test Runner, 4=Settings
        return Math.max(0, Math.min(3, tabIndex));
    } else {
        // Live mode: 0=Leaderboard, 1=JSON Paste, 2=Connection, 4=Settings (no Test Runner)
        if (tabIndex === 3) return 2; // Test Runner not available in live mode, switch to Connection
        return Math.max(0, Math.min(2, tabIndex));
    }
};

export default {
    getEnvConfig,
    getUserPreferences,
    saveUserPreferences,
    getAppConfig,
    resetToDefaults,
    getValidTabIndex,
    STORAGE_KEYS
};