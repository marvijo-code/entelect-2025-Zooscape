import React, { useState, useEffect, useCallback } from 'react';
import './TestRunner.css';

const TestRunner = ({ onGameStateSelected, apiBaseUrl = import.meta.env.VITE_API_BASE_URL, currentGameState = null, currentGameStateName = null, shouldShowCreateModal = false, onCreateModalChange = null }) => {
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
    loadAvailableBotsFromGameState();
  }, []);

  // Update available bots when currentGameState changes
  useEffect(() => {
    loadAvailableBotsFromGameState();
  }, [currentGameState]);

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
      const response = await fetch(`${apiBaseUrl}/Test/definitions`);
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

  const loadAvailableBotsFromGameState = useCallback(() => {
    if (!currentGameState) {
      setAvailableBots([]);
      return;
    }

    try {
      // Extract bot nicknames from the current game state
      const animals = currentGameState.animals || currentGameState.Animals || [];
      const botNicknames = animals
        .map(animal => animal.nickname || animal.Nickname)
        .filter(nickname => nickname && nickname.trim() !== '')
        .filter((nickname, index, array) => array.indexOf(nickname) === index) // Remove duplicates
        .sort();

      console.log('Available bots from game state:', botNicknames);
      setAvailableBots(botNicknames);
    } catch (error) {
      console.error('Error extracting bots from game state:', error);
      setAvailableBots([]);
    }
  }, [currentGameState]);

  const runTest = useCallback(async (testName) => {
    setRunningTests(prev => new Set([...prev, testName]));
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/Test/run/${encodeURIComponent(testName)}`, {
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

  const runTestDirect = useCallback(async (testData) => {
    const testName = testData.testName;
    setRunningTests(prev => new Set([...prev, testName]));
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/Test/direct`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(testData)
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to run direct test: ${response.status} ${errorText}`);
      }
      
      const result = await response.json();
      setTestResults(prev => ({
        ...prev,
        [testName]: result
      }));
      
      return result;
    } catch (error) {
      console.error('Error running direct test:', error);
      const errorResult = {
        testName,
        success: false,
        errorMessage: error.message,
        executionTimeMs: 0,
        botResults: []
      };
      setTestResults(prev => ({
        ...prev,
        [testName]: errorResult
      }));
      return errorResult;
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
      const response = await fetch(`${apiBaseUrl}/Test/run/all`, {
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
      const response = await fetch(`${apiBaseUrl}/Test/gamestate/${encodeURIComponent(gameStateFile)}`);
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
      const response = await fetch(`${apiBaseUrl}/Test/create`, {
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
          onRunTest={runTest}
          onRunTestDirect={runTestDirect}
          currentGameState={currentGameState}
          currentGameStateName={currentGameStateName}
          isCreating={creating}
          availableBots={availableBots}
          testResults={testResults}
          runningTests={runningTests}
          getTestStatus={getTestStatus}
          getStatusIcon={getStatusIcon}
          getStatusColor={getStatusColor}
        />
      )}
    </div>
  );
};

// Create Test Modal Component
const CreateTestModal = ({ isOpen, onClose, onCreateTest, onRunTest, onRunTestDirect, currentGameState, currentGameStateName, isCreating, availableBots = [], testResults = {}, runningTests = new Set(), getTestStatus, getStatusIcon, getStatusColor }) => {
  const [testName, setTestName] = useState(() => {
    // Generate a simple GUID-like string
    return 'test-' + Math.random().toString(36).substr(2, 9) + '-' + Date.now().toString(36);
  });
  const [description, setDescription] = useState('');
  const [testType, setTestType] = useState('MultiBotArray');
  const [acceptableActions, setAcceptableActions] = useState({
    Up: false,
    Down: false,
    Left: false,
    Right: false
  });
  const [selectedBots, setSelectedBots] = useState({});
  const [botNickname, setBotNickname] = useState('');
  const [showOptionalFields, setShowOptionalFields] = useState(false);
  const [createdTestName, setCreatedTestName] = useState('');
  const [testResult, setTestResult] = useState(null);
  const [runningTest, setRunningTest] = useState(false);

  // Initialize selectedBots when availableBots changes
  useEffect(() => {
    if (availableBots.length > 0) {
      const initialBotSelection = {};
      availableBots.forEach(bot => {
        // Auto-select ClingyHeuroBot2 if available
        initialBotSelection[bot] = bot === 'ClingyHeuroBot2';
      });
      setSelectedBots(initialBotSelection);
    }
  }, [availableBots]);



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

  const handleRunTest = async () => {
    const testNameToRun = testName.trim();
    
    if (!testNameToRun) {
      alert('Please enter a test name');
      return;
    }

    setRunningTest(true);
    setTestResult(null);

    try {
      // Create the test data for direct execution
      const actionMapping = { Up: 1, Down: 2, Left: 3, Right: 4 };
      const selectedActions = Object.entries(acceptableActions)
        .filter(([action, selected]) => selected)
        .map(([action]) => actionMapping[action]);

      const selectedBotsList = Object.entries(selectedBots)
        .filter(([bot, selected]) => selected)
        .map(([bot]) => bot);

      let botsToUse = [];
      if (testType === 'MultiBotArray') {
        botsToUse = selectedBotsList.length > 0 ? selectedBotsList : ['ClingyHeuroBot2'];
      } else if (testType === 'SingleBot' && selectedBotsList.length > 0) {
        botsToUse = [selectedBotsList[0]];
      }

      const testData = {
        testName: testNameToRun,
        gameStateFile: currentGameStateName || 'current-state.json',
        description: description.trim() || `Direct test execution from ${currentGameStateName || 'current game state'}`,
        testType,
        acceptableActions: selectedActions,
        botNickname: botNickname.trim() || null,
        tickOverride: false,
        bots: botsToUse,
        currentGameState: currentGameState
      };

      // Run the test directly without saving
      if (onRunTestDirect) {
        const result = await onRunTestDirect(testData);
        setTestResult(result);
        console.log('Direct test executed successfully:', result);
      }
    } catch (error) {
      console.error('Error running direct test:', error);
      setTestResult({
        testName: testNameToRun,
        success: false,
        errorMessage: error.message,
        executionTimeMs: 0,
        botResults: []
      });
    } finally {
      setRunningTest(false);
    }
  };

  const handleSaveTest = async () => {
    const testNameToSave = testName.trim();
    
    if (!testNameToSave) {
      alert('Please enter a test name');
      return;
    }

    try {
      // Create the test data for saving
      const actionMapping = { Up: 1, Down: 2, Left: 3, Right: 4 };
      const selectedActions = Object.entries(acceptableActions)
        .filter(([action, selected]) => selected)
        .map(([action]) => actionMapping[action]);

      const selectedBotsList = Object.entries(selectedBots)
        .filter(([bot, selected]) => selected)
        .map(([bot]) => bot);

      let botsToUse = [];
      if (testType === 'MultiBotArray') {
        botsToUse = selectedBotsList.length > 0 ? selectedBotsList : ['ClingyHeuroBot2'];
      } else if (testType === 'SingleBot' && selectedBotsList.length > 0) {
        botsToUse = [selectedBotsList[0]];
      }

      const testData = {
        testName: testNameToSave,
        gameStateFile: currentGameStateName || 'current-state.json',
        description: description.trim() || `Test created from ${currentGameStateName || 'current game state'}`,
        testType,
        acceptableActions: selectedActions,
        botNickname: botNickname.trim() || null,
        tickOverride: false,
        bots: botsToUse,
        currentGameState: currentGameState
      };

      // Save the test
      if (onCreateTest) {
        await onCreateTest(testData);
        console.log('Test saved successfully:', testNameToSave);
      }
    } catch (error) {
      console.error('Error saving test:', error);
      // Error handling is done in the onCreateTest function
    }
  };

  const handleCloseModal = () => {
    // Reset form when closing
    setTestName('test-' + Math.random().toString(36).substr(2, 9) + '-' + Date.now().toString(36));
    setDescription('');
    setTestType('MultiBotArray');
    setAcceptableActions({ Up: false, Down: false, Left: false, Right: false });
    const resetBotSelection = {};
    availableBots.forEach(bot => {
      resetBotSelection[bot] = bot === 'ClingyHeuroBot2';
    });
    setSelectedBots(resetBotSelection);
    setBotNickname('');
    setShowOptionalFields(false);
    setTestResult(null);
    setRunningTest(false);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={handleCloseModal}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '800px', width: '90vw' }}>
        <div className="modal-header">
          <h3>🧪 Run Test from Current State</h3>
          <button className="modal-close" onClick={handleCloseModal}>×</button>
        </div>
        
        <div className="modal-form" style={{ display: 'grid', gap: '15px' }}>
          {/* Row 1: Test Name and Type */}
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '10px' }}>
            <div className="form-group" style={{ margin: 0 }}>
              <label htmlFor="testName" style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                📝 Test Name *
              </label>
              <input
                id="testName"
                type="text"
                value={testName}
                onChange={(e) => setTestName(e.target.value)}
                placeholder="Enter test name..."
                required
              />
            </div>
            <div className="form-group" style={{ margin: 0 }}>
              <label htmlFor="testType" style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                ⚙️ Type
              </label>
              <select
                id="testType"
                value={testType}
                onChange={(e) => setTestType(e.target.value)}
              >
                <option value="SingleBot">🤖 Single Bot</option>
                <option value="MultiBotArray">🤖🤖 Multi-Bot</option>
                <option value="GameStateLoad">📂 Load Test</option>
                <option value="TickOverride">⏱️ Tick Override</option>
              </select>
            </div>
          </div>

          {/* Row 2: Actions and Optional Fields Toggle */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: '10px', alignItems: 'start' }}>
            <div className="form-group" style={{ margin: 0 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                🎮 Acceptable Actions
              </label>
              <div className="checkbox-group" style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
                {Object.entries(acceptableActions).map(([action, checked]) => (
                  <label key={action} className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: '3px' }}>
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={(e) => handleActionChange(action, e.target.checked)}
                    />
                    <span>{action === 'Up' ? '⬆️' : action === 'Down' ? '⬇️' : action === 'Left' ? '⬅️' : '➡️'} {action}</span>
                  </label>
                ))}
              </div>
              <small>Leave all unchecked to accept any action</small>
            </div>
            <button
              type="button"
              onClick={() => setShowOptionalFields(!showOptionalFields)}
              className="btn btn-secondary"
              style={{ padding: '5px 10px', fontSize: '12px' }}
            >
              {showOptionalFields ? '▼' : '▶'} Options
            </button>
          </div>

          {/* Optional Fields - Collapsible */}
          {showOptionalFields && (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', padding: '10px', backgroundColor: '#2a2a2a', borderRadius: '5px', border: '1px solid #444' }}>
              <div className="form-group" style={{ margin: 0 }}>
                <label htmlFor="description" style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                  📄 Description
                </label>
                <textarea
                  id="description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Optional description..."
                  rows="2"
                />
              </div>
              <div className="form-group" style={{ margin: 0 }}>
                <label htmlFor="botNickname" style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                  🏷️ Bot Nickname
                </label>
                <input
                  id="botNickname"
                  type="text"
                  value={botNickname}
                  onChange={(e) => setBotNickname(e.target.value)}
                  placeholder="Leave empty to use first animal..."
                />
              </div>
            </div>
          )}

          {/* Row 3: Bot Selection */}
          {availableBots.length > 0 && (
            <div className="form-group" style={{ margin: 0 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
                🤖 Select Bots {testType === 'MultiBotArray' ? '(Multiple)' : '(Single)'}
              </label>
              <div className="checkbox-group" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '5px' }}>
                {availableBots.map((bot) => (
                  <label key={bot} className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                    <input
                      type="checkbox"
                      checked={selectedBots[bot] || false}
                      onChange={(e) => handleBotChange(bot, e.target.checked)}
                    />
                    <span>{bot}</span>
                  </label>
                ))}
              </div>
            </div>
          )}

          {/* Row 4: Game State File */}
          <div className="form-group" style={{ margin: 0 }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '5px', marginBottom: '5px' }}>
              📁 Game State File
            </label>
            <input
              type="text"
              value={currentGameStateName || 'current-state.json'}
              disabled
              className="disabled-input"
              style={{ fontSize: '12px' }}
            />
          </div>

          {/* Test Results Section */}
          {testResult && (
            <div className="test-results-section" style={{ 
              marginTop: '20px', 
              padding: '15px', 
              border: '1px solid #444', 
              borderRadius: '5px', 
              backgroundColor: '#2a2a2a',
              color: '#e0e0e0'
            }}>
              <h4 style={{ color: '#ffffff', marginBottom: '10px' }}>🧪 Test Results: {testResult.testName}</h4>
              
              {/* Test Results Display */}
              <div className="test-result-details">
                <div style={{ 
                  fontFamily: 'monospace', 
                  fontSize: '12px', 
                  backgroundColor: '#1a1a1a', 
                  color: '#e0e0e0',
                  padding: '10px', 
                  border: '1px solid #555', 
                  borderRadius: '3px', 
                  maxHeight: '200px', 
                  overflow: 'auto' 
                }}>
                  <div><strong style={{ color: '#ffffff' }}>Success:</strong> {testResult.success ? '✅ Yes' : '❌ No'}</div>
                  <div><strong style={{ color: '#ffffff' }}>Execution Time:</strong> {testResult.executionTimeMs}ms</div>
                  {testResult.errorMessage && (
                    <div><strong style={{ color: '#ff6b6b' }}>Error:</strong> <span style={{ color: '#ff8a8a' }}>{testResult.errorMessage}</span></div>
                  )}
                  {testResult.botResults && testResult.botResults.length > 0 && (
                    <div>
                      <strong style={{ color: '#ffffff' }}>Bot Results ({testResult.botResults.length}):</strong>
                      <div style={{ marginTop: '8px' }}>
                        {testResult.botResults.map((botResult, index) => (
                          <div key={index} style={{ 
                            marginBottom: '10px', 
                            padding: '8px', 
                            backgroundColor: '#0f0f0f', 
                            border: '1px solid #333', 
                            borderRadius: '4px',
                            borderLeft: `4px solid ${botResult.success ? '#4ade80' : '#ef4444'}`
                          }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                              <strong style={{ color: '#ffffff' }}>{botResult.botType}</strong>
                              <span style={{ 
                                color: botResult.success ? '#4ade80' : '#ef4444',
                                fontSize: '12px',
                                fontWeight: 'bold'
                              }}>
                                {botResult.success ? '✅ Success' : '❌ Failed'}
                              </span>
                            </div>
                            
                            {botResult.action && (
                              <div style={{ marginBottom: '4px' }}>
                                <span style={{ color: '#a0a0a0' }}>Action: </span>
                                <span style={{ color: '#ffffff', fontWeight: 'bold' }}>{botResult.action}</span>
                              </div>
                            )}
                            
                            {botResult.botId && (
                              <div style={{ marginBottom: '4px' }}>
                                <span style={{ color: '#a0a0a0' }}>Bot ID: </span>
                                <span style={{ color: '#c0c0c0', fontFamily: 'monospace', fontSize: '11px' }}>{botResult.botId}</span>
                              </div>
                            )}
                            
                            {(botResult.initialScore !== undefined || botResult.finalScore !== undefined || botResult.scoreDelta !== undefined) && (
                              <div style={{ marginBottom: '4px' }}>
                                <span style={{ color: '#a0a0a0' }}>Score: </span>
                                {botResult.initialScore !== undefined && (
                                  <span style={{ color: '#c0c0c0' }}>Initial: {botResult.initialScore} </span>
                                )}
                                {botResult.finalScore !== undefined && (
                                  <span style={{ color: '#c0c0c0' }}>Final: {botResult.finalScore} </span>
                                )}
                                {botResult.scoreDelta !== undefined && (
                                  <span style={{ 
                                    color: botResult.scoreDelta >= 0 ? '#4ade80' : '#ef4444',
                                    fontWeight: 'bold'
                                  }}>
                                    Delta: {botResult.scoreDelta >= 0 ? '+' : ''}{botResult.scoreDelta}
                                  </span>
                                )}
                              </div>
                            )}
                            
                            {botResult.errorMessage && (
                              <div style={{ marginBottom: '4px' }}>
                                <span style={{ color: '#ff6b6b' }}>Error: </span>
                                <span style={{ color: '#ff8a8a' }}>{botResult.errorMessage}</span>
                              </div>
                            )}
                            
                            {botResult.performanceMetrics && Object.keys(botResult.performanceMetrics).length > 0 && (
                              <div style={{ marginTop: '6px' }}>
                                <div style={{ color: '#a0a0a0', fontSize: '11px', marginBottom: '4px' }}>Performance Metrics:</div>
                                <div style={{ 
                                  backgroundColor: '#1a1a1a', 
                                  padding: '6px', 
                                  borderRadius: '3px',
                                  fontSize: '11px'
                                }}>
                                  {Object.entries(botResult.performanceMetrics).map(([key, value]) => {
                                    if (key === 'detailedHeuristicScores' && Array.isArray(value) && value.length > 0) {
                                      return (
                                        <div key={key} style={{ marginBottom: '8px' }}>
                                          <div style={{ color: '#4ade80', fontWeight: 'bold', marginBottom: '6px' }}>
                                            🧠 Heuristic Scores ({value.length} actions evaluated):
                                          </div>
                                          {value.map((scoreLog, scoreIndex) => (
                                            <div key={scoreIndex} style={{ 
                                              marginBottom: '8px', 
                                              padding: '6px', 
                                              backgroundColor: '#0a0a0a', 
                                              border: '1px solid #333',
                                              borderRadius: '3px',
                                              borderLeft: scoreLog.Action === botResult.action ? '3px solid #4ade80' : '3px solid #555'
                                            }}>
                                              <div style={{ 
                                                display: 'flex', 
                                                justifyContent: 'space-between', 
                                                alignItems: 'center',
                                                marginBottom: '4px'
                                              }}>
                                                <span style={{ 
                                                  color: scoreLog.Action === botResult.action ? '#4ade80' : '#ffffff',
                                                  fontWeight: 'bold'
                                                }}>
                                                  {scoreLog.Action === botResult.action ? '✅ ' : ''}{scoreLog.Action}
                                                </span>
                                                <span style={{ 
                                                  color: scoreLog.TotalScore >= 0 ? '#4ade80' : '#ef4444',
                                                  fontWeight: 'bold',
                                                  fontSize: '12px'
                                                }}>
                                                  {scoreLog.TotalScore >= 0 ? '+' : ''}{Number(scoreLog.TotalScore).toFixed(2)}
                                                </span>
                                              </div>
                                              {scoreLog.DetailedLogLines && scoreLog.DetailedLogLines.length > 0 && (
                                                <div style={{ 
                                                  fontSize: '9px', 
                                                  color: '#888',
                                                  fontFamily: 'monospace',
                                                  whiteSpace: 'pre-wrap',
                                                  maxHeight: '150px',
                                                  overflowY: 'auto',
                                                  backgroundColor: '#050505',
                                                  padding: '4px',
                                                  borderRadius: '2px'
                                                }}>
                                                  {scoreLog.DetailedLogLines.join('\n')}
                                                </div>
                                              )}
                                            </div>
                                          ))}
                                        </div>
                                      );
                                    } else if (key === 'allActionScores' && typeof value === 'object') {
                                      return (
                                        <div key={key} style={{ marginBottom: '4px' }}>
                                          <div style={{ color: '#a0a0a0', marginBottom: '2px' }}>All Action Scores:</div>
                                          <div style={{ 
                                            backgroundColor: '#0a0a0a', 
                                            padding: '4px', 
                                            borderRadius: '2px',
                                            fontSize: '10px'
                                          }}>
                                            {Object.entries(value).map(([action, score]) => (
                                              <div key={action} style={{ 
                                                display: 'flex', 
                                                justifyContent: 'space-between',
                                                color: action === botResult.action ? '#4ade80' : '#c0c0c0'
                                              }}>
                                                <span>{action === botResult.action ? '✅ ' : ''}{action}:</span>
                                                <span>{Number(score).toFixed(2)}</span>
                                              </div>
                                            ))}
                                          </div>
                                        </div>
                                      );
                                    } else if (key !== 'detailedHeuristicScores' && key !== 'allActionScores') {
                                      return (
                                        <div key={key} style={{ marginBottom: '2px' }}>
                                          <span style={{ color: '#a0a0a0' }}>{key}: </span>
                                          <span style={{ color: '#c0c0c0' }}>
                                            {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                                          </span>
                                        </div>
                                      );
                                    }
                                    return null;
                                  })}
                                </div>
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          <div className="modal-actions" style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
            <button type="button" onClick={handleCloseModal} className="btn btn-secondary" disabled={runningTest || isCreating}>
              ❌ Cancel
            </button>
            <button 
              type="button"
              onClick={handleSaveTest} 
              disabled={!testName.trim() || isCreating}
              className="btn btn-info"
            >
              {isCreating ? '⏳ Saving...' : '💾 Save Test'}
            </button>
            <button 
              type="button"
              onClick={handleRunTest} 
              disabled={!testName.trim() || runningTest}
              className="btn btn-success"
            >
              {runningTest ? '⏳ Running...' : '▶️ Run Test'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default TestRunner; 