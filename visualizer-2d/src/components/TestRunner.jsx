import React, { useState, useEffect, useCallback } from 'react';
import './TestRunner.css';

const TestRunner = ({ onGameStateSelected, apiBaseUrl = 'http://localhost:5009/api', currentGameState = null, currentGameStateName = null, shouldShowCreateModal = false, onCreateModalChange = null }) => {
  const [tests, setTests] = useState([]);
  const [testResults, setTestResults] = useState({});
  const [loading, setLoading] = useState(false);
  const [selectedTest, setSelectedTest] = useState(null);
  const [runningTests, setRunningTests] = useState(new Set());
  const [error, setError] = useState(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [creating, setCreating] = useState(false);
  const [availableBots, setAvailableBots] = useState([]);

  // Load available tests and bots on component mount
  useEffect(() => {
    loadTests();
    loadAvailableBots();
  }, []);

  // Handle external request to show create modal
  useEffect(() => {
    if (shouldShowCreateModal && !showCreateModal) {
      setShowCreateModal(true);
      // Reset the external signal
      if (onCreateModalChange) {
        onCreateModalChange(false);
      }
    }
  }, [shouldShowCreateModal, showCreateModal, onCreateModalChange]);

  const loadTests = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/test/definitions`);
      if (!response.ok) {
        throw new Error(`Failed to load tests: ${response.status} ${response.statusText}`);
      }
      const data = await response.json();
      setTests(data);
    } catch (error) {
      console.error('Error loading tests:', error);
      setError(`Failed to load tests: ${error.message}`);
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl]);

  const loadAvailableBots = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/test/bots`);
      if (!response.ok) {
        throw new Error(`Failed to load bots: ${response.status} ${response.statusText}`);
      }
      const data = await response.json();
      setAvailableBots(data);
    } catch (error) {
      console.error('Error loading available bots:', error);
      // Don't set error state for bots loading failure, just log it
    }
  }, [apiBaseUrl]);

  const runTest = useCallback(async (testName) => {
    setRunningTests(prev => new Set([...prev, testName]));
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/test/run/${encodeURIComponent(testName)}`, {
        method: 'POST'
      });
      
      if (!response.ok) {
        throw new Error(`Failed to run test: ${response.status} ${response.statusText}`);
      }
      
      const result = await response.json();
      setTestResults(prev => ({
        ...prev,
        [testName]: result
      }));
    } catch (error) {
      console.error('Error running test:', error);
      setTestResults(prev => ({
        ...prev,
        [testName]: {
          testName,
          success: false,
          errorMessage: error.message,
          executionTimeMs: 0,
          botResults: []
        }
      }));
    } finally {
      setRunningTests(prev => {
        const newSet = new Set(prev);
        newSet.delete(testName);
        return newSet;
      });
    }
  }, [apiBaseUrl]);

  const runAllTests = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/test/run/all`, {
        method: 'POST'
      });
      
      if (!response.ok) {
        throw new Error(`Failed to run all tests: ${response.status} ${response.statusText}`);
      }
      
      const results = await response.json();
      const resultsMap = {};
      results.forEach(result => {
        resultsMap[result.testName] = result;
      });
      setTestResults(resultsMap);
    } catch (error) {
      console.error('Error running all tests:', error);
      setError(`Failed to run all tests: ${error.message}`);
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl]);

  const viewGameState = useCallback(async (gameStateFile) => {
    try {
      const response = await fetch(`${apiBaseUrl}/test/gamestate/${encodeURIComponent(gameStateFile)}`);
      if (!response.ok) {
        throw new Error(`Failed to load game state: ${response.status} ${response.statusText}`);
      }
      const gameState = await response.json();
      
      // Call the callback to display the game state in the grid
      if (onGameStateSelected) {
        onGameStateSelected(gameState, `Test Game State: ${gameStateFile}`);
      }
    } catch (error) {
      console.error('Error loading game state:', error);
      setError(`Failed to load game state: ${error.message}`);
    }
  }, [apiBaseUrl, onGameStateSelected]);

  const createTest = useCallback(async (testData) => {
    setCreating(true);
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/test/create`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(testData)
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to create test: ${response.status} ${errorText}`);
      }
      
      const newTest = await response.json();
      
      // Refresh test list
      await loadTests();
      
      setShowCreateModal(false);
      return newTest;
    } catch (error) {
      console.error('Error creating test:', error);
      setError(`Failed to create test: ${error.message}`);
      throw error;
    } finally {
      setCreating(false);
    }
  }, [apiBaseUrl, loadTests]);

  const getTestStatus = (testName) => {
    if (runningTests.has(testName)) return 'running';
    const result = testResults[testName];
    if (!result) return 'not-run';
    return result.success ? 'passed' : 'failed';
  };

  const getStatusIcon = (status) => {
    switch (status) {
      case 'running': return '⏳';
      case 'passed': return '✅';
      case 'failed': return '❌';
      default: return '⚪';
    }
  };

  const getStatusColor = (status) => {
    switch (status) {
      case 'running': return '#ffa500';
      case 'passed': return '#28a745';
      case 'failed': return '#dc3545';
      default: return '#6c757d';
    }
  };

  const formatTestType = (testType) => {
    switch (testType) {
      case 'GameStateLoad': return 'Load Test';
      case 'SingleBot': return 'Single Bot';
      case 'MultiBotArray': return 'Multi-Bot';
      case 'TickOverride': return 'Tick Override';
      default: return testType;
    }
  };

  if (loading && tests.length === 0) {
    return (
      <div className="test-runner">
        <div className="loading">Loading tests...</div>
      </div>
    );
  }

  return (
    <div className="test-runner">
      <div className="test-runner-header">
        <h3>Functional Test Runner</h3>
        <div className="test-runner-actions">
          <button 
            onClick={loadTests} 
            disabled={loading}
            className="btn btn-secondary"
          >
            🔄 Refresh Tests
          </button>
          <button 
            onClick={() => setShowCreateModal(true)} 
            disabled={!currentGameState}
            className="btn btn-info"
            title={!currentGameState ? "View a game state first to create a test" : "Create a test with current game state"}
          >
            ➕ Create Test
          </button>
          <button 
            onClick={runAllTests} 
            disabled={loading || tests.length === 0}
            className="btn btn-primary"
          >
            ▶️ Run All Tests
          </button>
        </div>
      </div>

      {error && (
        <div className="error-message">
          <strong>Error:</strong> {error}
        </div>
      )}

      <div className="test-stats">
        <div className="stat">
          <span className="stat-label">Total Tests:</span>
          <span className="stat-value">{tests.length}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Passed:</span>
          <span className="stat-value passed">
            {Object.values(testResults).filter(r => r.success).length}
          </span>
        </div>
        <div className="stat">
          <span className="stat-label">Failed:</span>
          <span className="stat-value failed">
            {Object.values(testResults).filter(r => !r.success).length}
          </span>
        </div>
        <div className="stat">
          <span className="stat-label">Running:</span>
          <span className="stat-value running">
            {runningTests.size}
          </span>
        </div>
      </div>

      <div className="test-list">
        {tests.map((test) => {
          const status = getTestStatus(test.testName);
          const result = testResults[test.testName];
          
          return (
            <div 
              key={test.testName} 
              className={`test-item ${status} ${selectedTest === test.testName ? 'selected' : ''}`}
              onClick={() => setSelectedTest(selectedTest === test.testName ? null : test.testName)}
            >
              <div className="test-item-header">
                <div className="test-info">
                  <span className="test-status" style={{ color: getStatusColor(status) }}>
                    {getStatusIcon(status)}
                  </span>
                  <span className="test-name">{test.testName}</span>
                  <span className="test-type">{formatTestType(test.testType)}</span>
                </div>
                <div className="test-actions">
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      viewGameState(test.gameStateFile);
                    }}
                    className="btn btn-sm btn-info"
                    title="View game state in grid"
                  >
                    👁️ View
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      runTest(test.testName);
                    }}
                    disabled={runningTests.has(test.testName)}
                    className="btn btn-sm btn-primary"
                  >
                    {runningTests.has(test.testName) ? '⏳' : '▶️'} Run
                  </button>
                </div>
              </div>

              {selectedTest === test.testName && (
                <div className="test-details">
                  <div className="test-description">
                    <strong>Description:</strong> {test.description || 'No description'}
                  </div>
                  
                  <div className="test-config">
                    <div><strong>Game State:</strong> {test.gameStateFile}</div>
                    {test.botNickname && <div><strong>Bot Nickname:</strong> {test.botNickname}</div>}
                    {test.expectedAction && <div><strong>Expected Action:</strong> {test.expectedAction}</div>}
                    {test.acceptableActions?.length > 0 && (
                      <div><strong>Acceptable Actions:</strong> {test.acceptableActions.join(', ')}</div>
                    )}
                    {test.bots?.length > 0 && (
                      <div><strong>Bots:</strong> {test.bots.join(', ')}</div>
                    )}
                  </div>

                  {result && (
                    <div className="test-result">
                      <div className={`result-summary ${result.success ? 'success' : 'error'}`}>
                        <strong>Result:</strong> {result.success ? 'PASSED' : 'FAILED'}
                        <span className="execution-time">({result.executionTimeMs}ms)</span>
                      </div>
                      
                      {result.errorMessage && (
                        <div className="error-details">
                          <strong>Error:</strong> {result.errorMessage}
                        </div>
                      )}

                      {result.botResults?.length > 0 && (
                        <div className="bot-results">
                          <strong>Bot Results & Actions:</strong>
                          {result.botResults.map((botResult, index) => (
                            <div key={index} className={`bot-result ${botResult.success ? 'success' : 'error'}`}>
                              <div className="bot-info">
                                <span className="bot-type">{botResult.botType}</span>
                                {botResult.botId && (
                                  <span className="bot-id">ID: {botResult.botId.substring(0, 8)}...</span>
                                )}
                                {(botResult.initialScore !== undefined || botResult.finalScore !== undefined) && (
                                  <div className="bot-scores">
                                    <span className="score-info">
                                      Score: {botResult.initialScore || 0} → {botResult.finalScore || 0}
                                      {botResult.scoreDelta !== undefined && (
                                        <span className={`score-delta ${botResult.scoreDelta >= 0 ? 'positive' : 'negative'}`}>
                                          ({botResult.scoreDelta >= 0 ? '+' : ''}{botResult.scoreDelta})
                                        </span>
                                      )}
                                    </span>
                                  </div>
                                )}
                              </div>
                              <div className="bot-action-display">
                                <span className={`bot-action ${botResult.success ? 'success' : 'error'}`}>
                                  {botResult.action || 'N/A'}
                                </span>
                                {botResult.scoreDelta !== undefined && (
                                  <span className={`action-score ${botResult.scoreDelta >= 0 ? 'positive' : 'negative'}`}>
                                    {botResult.scoreDelta >= 0 ? '+' : ''}{botResult.scoreDelta}
                                  </span>
                                )}
                              </div>
                              <div className="bot-status-indicator">
                                {botResult.success ? '✅' : '❌'}
                              </div>
                              {botResult.errorMessage && (
                                <div className="bot-error">
                                  Error: {botResult.errorMessage}
                                </div>
                              )}
                              {botResult.performanceMetrics?.allActionScores && Object.keys(botResult.performanceMetrics.allActionScores).length > 0 && (
                                <div className="action-scores-breakdown">
                                  <div className="action-scores-title">Bot's Action Scores:</div>
                                  <div className="action-scores-grid">
                                    {Object.entries(botResult.performanceMetrics.allActionScores).map(([action, score]) => (
                                      <div 
                                        key={action} 
                                        className={`action-score-item ${action === botResult.action ? 'chosen' : ''}`}
                                      >
                                        <span className="action-name">{action}</span>
                                        <span className="action-score-value">
                                          {typeof score === 'number' ? score.toFixed(2) : score}
                                        </span>
                                      </div>
                                    ))}
                                  </div>
                                  {botResult.performanceMetrics.chosenActionScore !== undefined && (
                                    <div className="chosen-action-summary">
                                      <strong>Chosen:</strong> {botResult.action} (Score: {typeof botResult.performanceMetrics.chosenActionScore === 'number' ? botResult.performanceMetrics.chosenActionScore.toFixed(2) : botResult.performanceMetrics.chosenActionScore})
                                    </div>
                                  )}
                                </div>
                              )}
                            </div>
                          ))}
                          {result.botResults.length > 1 && (
                            <div className="action-summary">
                              <div className="action-summary-title">Action Summary:</div>
                              <div className="action-counts">
                                <div className="action-count">
                                  <span className="action-count-label">Total Bots:</span>
                                  <span className="action-count-value">{result.botResults.length}</span>
                                </div>
                                <div className="action-count">
                                  <span className="action-count-label">Successful:</span>
                                  <span className="action-count-value" style={{color: 'var(--accent-green)'}}>
                                    {result.botResults.filter(br => br.success).length}
                                  </span>
                                </div>
                                <div className="action-count">
                                  <span className="action-count-label">Failed:</span>
                                  <span className="action-count-value" style={{color: 'var(--accent-red)'}}>
                                    {result.botResults.filter(br => !br.success).length}
                                  </span>
                                </div>
                                {(() => {
                                  const actionCounts = result.botResults.reduce((acc, br) => {
                                    const action = br.action || 'N/A';
                                    acc[action] = (acc[action] || 0) + 1;
                                    return acc;
                                  }, {});
                                  return Object.entries(actionCounts).map(([action, count]) => (
                                    <div key={action} className="action-count">
                                      <span className="action-count-label">{action}:</span>
                                      <span className="action-count-value">{count}</span>
                                    </div>
                                  ));
                                })()}
                              </div>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {tests.length === 0 && !loading && (
        <div className="no-tests">
          <p>No tests found. Make sure the test API is running and accessible.</p>
          <button onClick={loadTests} className="btn btn-primary">
            Retry Loading Tests
          </button>
        </div>
      )}

      {/* Create Test Modal */}
      {showCreateModal && (
        <CreateTestModal
          isOpen={showCreateModal}
          onClose={() => setShowCreateModal(false)}
          onCreateTest={createTest}
          currentGameState={currentGameState}
          currentGameStateName={currentGameStateName}
          isCreating={creating}
          availableBots={availableBots}
        />
      )}
    </div>
  );
};

// Create Test Modal Component
const CreateTestModal = ({ isOpen, onClose, onCreateTest, currentGameState, currentGameStateName, isCreating, availableBots = [] }) => {
  const [testName, setTestName] = useState('');
  const [description, setDescription] = useState('');
  const [testType, setTestType] = useState('SingleBot');
  const [acceptableActions, setAcceptableActions] = useState({
    Up: false,
    Down: false,
    Left: false,
    Right: false
  });
  const [selectedBots, setSelectedBots] = useState({});
  const [botNickname, setBotNickname] = useState('');

  // Initialize selectedBots when availableBots changes
  useEffect(() => {
    if (availableBots.length > 0) {
      const initialBotSelection = {};
      availableBots.forEach(bot => {
        initialBotSelection[bot] = false;
      });
      setSelectedBots(initialBotSelection);
    }
  }, [availableBots]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!testName.trim()) {
      alert('Please enter a test name');
      return;
    }

    // Get selected acceptable actions - convert to enum values (1-4)
    const actionMapping = { Up: 1, Down: 2, Left: 3, Right: 4 };
    const selectedActions = Object.entries(acceptableActions)
      .filter(([action, selected]) => selected)
      .map(([action]) => actionMapping[action]);

    // Get selected bots
    const selectedBotsList = Object.entries(selectedBots)
      .filter(([bot, selected]) => selected)
      .map(([bot]) => bot);

    // Determine bots based on test type and selection
    let botsToUse = [];
    if (testType === 'MultiBotArray') {
      botsToUse = selectedBotsList.length > 0 ? selectedBotsList : ['ClingyHeuroBot2'];
    } else if (testType === 'SingleBot' && selectedBotsList.length > 0) {
      botsToUse = [selectedBotsList[0]]; // Use first selected bot for SingleBot tests
    }

    const testData = {
      testName: testName.trim(),
      gameStateFile: currentGameStateName || 'current-state.json',
      description: description.trim() || `Test created from ${currentGameStateName || 'current game state'}`,
      testType,
      acceptableActions: selectedActions,
      botNickname: botNickname.trim() || null,
      tickOverride: false,
      bots: botsToUse,
      currentGameState: currentGameState  // Send the actual game state JSON data
    };

    try {
      await onCreateTest(testData);
      // Reset form
      setTestName('');
      setDescription('');
      setTestType('SingleBot');
      setAcceptableActions({ Up: false, Down: false, Left: false, Right: false });
      const resetBotSelection = {};
      availableBots.forEach(bot => {
        resetBotSelection[bot] = false;
      });
      setSelectedBots(resetBotSelection);
      setBotNickname('');
      // Close modal after successful creation
      onClose();
    } catch (error) {
      // Error handled in parent
    }
  };

  const handleActionChange = (action, checked) => {
    setAcceptableActions(prev => ({
      ...prev,
      [action]: checked
    }));
  };

  const handleBotChange = (bot, checked) => {
    setSelectedBots(prev => ({
      ...prev,
      [bot]: checked
    }));
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Create New Test</h3>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        
        <form onSubmit={handleSubmit} className="modal-form">
          <div className="form-group">
            <label htmlFor="testName">Test Name *</label>
            <input
              id="testName"
              type="text"
              value={testName}
              onChange={(e) => setTestName(e.target.value)}
              placeholder="Enter test name..."
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="description">Description</label>
            <textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional test description..."
              rows="3"
            />
          </div>

          <div className="form-group">
            <label htmlFor="testType">Test Type</label>
            <select
              id="testType"
              value={testType}
              onChange={(e) => setTestType(e.target.value)}
            >
              <option value="SingleBot">Single Bot</option>
              <option value="MultiBotArray">Multi-Bot Array</option>
              <option value="GameStateLoad">Game State Load</option>
              <option value="TickOverride">Tick Override</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="botNickname">Bot Nickname (Optional)</label>
            <input
              id="botNickname"
              type="text"
              value={botNickname}
              onChange={(e) => setBotNickname(e.target.value)}
              placeholder="Leave empty to use first animal..."
            />
          </div>

          <div className="form-group">
            <label>Acceptable Actions</label>
            <div className="checkbox-group">
              {Object.entries(acceptableActions).map(([action, checked]) => (
                <label key={action} className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(e) => handleActionChange(action, e.target.checked)}
                  />
                  <span>{action}</span>
                </label>
              ))}
            </div>
            <small>Leave all unchecked to accept any action</small>
          </div>

          {availableBots.length > 0 && (
            <div className="form-group">
              <label>Select Bots {testType === 'MultiBotArray' ? '(Multiple bots will be tested)' : '(Only first selected bot will be used)'}</label>
              <div className="checkbox-group">
                {availableBots.map((bot) => (
                  <label key={bot} className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={selectedBots[bot] || false}
                      onChange={(e) => handleBotChange(bot, e.target.checked)}
                    />
                    <span>{bot}</span>
                  </label>
                ))}
              </div>
              <small>
                {testType === 'MultiBotArray' 
                  ? 'Select multiple bots to test (defaults to ClingyHeuroBot2 if none selected)' 
                  : 'Select which bot to use for testing (defaults to ClingyHeuroBot2 if none selected)'}
              </small>
            </div>
          )}

          <div className="form-group">
            <label>Game State File</label>
            <input
              type="text"
              value={currentGameStateName || 'current-state.json'}
              disabled
              className="disabled-input"
            />
            <small>Using current game state from visualizer</small>
          </div>

          <div className="modal-actions">
            <button type="button" onClick={onClose} className="btn btn-secondary" disabled={isCreating}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={isCreating}>
              {isCreating ? 'Creating...' : 'Create Test'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default TestRunner; 