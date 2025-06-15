import React, { useState, useEffect } from 'react';
import { getAppConfig, saveUserPreferences, resetToDefaults, getEnvConfig } from '../config/appConfig.js';

const Settings = ({ onSettingsChange, currentMode, currentTab }) => {
    const [config, setConfig] = useState(getAppConfig());
    const [envConfig] = useState(getEnvConfig());
    const [hasChanges, setHasChanges] = useState(false);
    const [saveStatus, setSaveStatus] = useState('');

    useEffect(() => {
        const currentConfig = getAppConfig();
        setConfig(currentConfig);
    }, []);

    const handleConfigChange = (key, value) => {
        const newConfig = { ...config, [key]: value };
        setConfig(newConfig);
        setHasChanges(true);
        setSaveStatus('');
    };

    const handleSave = () => {
        const success = saveUserPreferences({
            defaultReplayMode: config.defaultReplayMode,
            defaultActiveTab: config.defaultActiveTab
        });

        if (success) {
            setSaveStatus('Settings saved successfully!');
            setHasChanges(false);
            // Notify parent component of changes
            if (onSettingsChange) {
                onSettingsChange(config);
            }
        } else {
            setSaveStatus('Failed to save settings.');
        }

        setTimeout(() => setSaveStatus(''), 3000);
    };

    const handleReset = () => {
        const success = resetToDefaults();
        if (success) {
            const resetConfig = getAppConfig();
            setConfig(resetConfig);
            setSaveStatus('Settings reset to defaults!');
            setHasChanges(false);
            // Notify parent component of changes
            if (onSettingsChange) {
                onSettingsChange(resetConfig);
            }
        } else {
            setSaveStatus('Failed to reset settings.');
        }

        setTimeout(() => setSaveStatus(''), 3000);
    };

    const getTabName = (index, isReplayMode) => {
        if (isReplayMode) {
            switch (index) {
                case 0: return 'Leaderboard';
                case 1: return 'JSON Paste Loader';
                case 2: return 'Game Selector';
                case 3: return 'Test Runner';
                default: return `Tab ${index}`;
            }
        } else {
            switch (index) {
                case 0: return 'Leaderboard';
                case 1: return 'JSON Paste Loader';
                case 2: return 'Connection';
                default: return `Tab ${index}`;
            }
        }
    };

    return (
        <div className="settings-container">
            <h3>Application Settings</h3>

            <div className="settings-section">
                <h4>Default Startup Configuration</h4>

                <div className="setting-item">
                    <label>
                        <input
                            type="checkbox"
                            checked={config.defaultReplayMode}
                            onChange={(e) => handleConfigChange('defaultReplayMode', e.target.checked)}
                        />
                        Start in Replay Mode
                    </label>
                    <small className="setting-description">
                        When enabled, the application will start in Replay Mode instead of Live Mode.
                        {envConfig.defaultReplayMode !== config.defaultReplayMode && (
                            <span className="env-override"> (Overriding .env setting: {envConfig.defaultReplayMode ? 'true' : 'false'})</span>
                        )}
                    </small>
                </div>

                <div className="setting-item">
                    <label>Default Active Tab:</label>
                    <select
                        value={config.defaultActiveTab}
                        onChange={(e) => handleConfigChange('defaultActiveTab', parseInt(e.target.value, 10))}
                    >
                        <option value={0}>Leaderboard</option>
                        <option value={1}>JSON Paste Loader</option>
                        <option value={2}>
                            {config.defaultReplayMode ? 'Game Selector' : 'Connection'}
                        </option>
                        {config.defaultReplayMode && (
                            <option value={3}>Test Runner</option>
                        )}
                    </select>
                    <small className="setting-description">
                        The tab that will be active when the application starts.
                        Current: {getTabName(config.defaultActiveTab, config.defaultReplayMode)}
                        {envConfig.defaultActiveTab !== config.defaultActiveTab && (
                            <span className="env-override"> (Overriding .env setting: {getTabName(envConfig.defaultActiveTab, envConfig.defaultReplayMode)})</span>
                        )}
                    </small>
                </div>
            </div>

            <div className="settings-section">
                <h4>Environment Configuration</h4>
                <div className="env-info">
                    <div className="env-item">
                        <strong>Hub URL:</strong> {envConfig.hubUrl}
                    </div>
                    <div className="env-item">
                        <strong>API Base URL:</strong> {envConfig.apiBaseUrl}
                    </div>
                    <div className="env-item">
                        <strong>Debug Logs:</strong> {envConfig.debugLogs ? 'Enabled' : 'Disabled'}
                    </div>
                    <small className="env-note">
                        These settings are configured via environment variables (.env file) and cannot be changed here.
                    </small>
                </div>
            </div>

            <div className="settings-section">
                <h4>Current Session</h4>
                <div className="current-info">
                    <div className="current-item">
                        <strong>Current Mode:</strong> {currentMode ? 'Replay Mode' : 'Live Mode'}
                    </div>
                    <div className="current-item">
                        <strong>Current Tab:</strong> {getTabName(currentTab, currentMode)}
                    </div>
                </div>
            </div>

            <div className="settings-actions">
                <button
                    onClick={handleSave}
                    disabled={!hasChanges}
                    className="save-button"
                >
                    Save Settings
                </button>
                <button
                    onClick={handleReset}
                    className="reset-button"
                >
                    Reset to Defaults
                </button>
            </div>

            {saveStatus && (
                <div className={`save-status ${saveStatus.includes('Failed') ? 'error' : 'success'}`}>
                    {saveStatus}
                </div>
            )}

            <div className="settings-help">
                <h4>Help</h4>
                <ul>
                    <li><strong>Replay Mode:</strong> Browse and replay saved game logs</li>
                    <li><strong>Live Mode:</strong> Connect to live game sessions</li>
                    <li><strong>Settings are saved locally</strong> in your browser and will persist between sessions</li>
                    <li><strong>Environment variables</strong> can be configured in the .env file for system-wide defaults</li>
                </ul>
            </div>
        </div>
    );
};

export default Settings;