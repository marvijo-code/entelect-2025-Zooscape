import React, { useState, useEffect, useCallback } from 'react';
import './TestRunner.css';

const TestRunner = ({ onGameStateSelected, apiBaseUrl = 'http://localhost:5009/api' }) => {
  const [tests, setTests] = useState([]);
  const [testResults, setTestResults] = useState({});
  const [loading, setLoading] = useState(false);
  const [selectedTest, setSelectedTest] = useState(null);
  const [runningTests, setRunningTests] = useState(new Set());
  const [error, setError] = useState(null);

  // Load available tests on component mount
  useEffect(() => {
    loadTests();
  }, []);

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
                          <strong>Bot Results:</strong>
                          {result.botResults.map((botResult, index) => (
                            <div key={index} className={`bot-result ${botResult.success ? 'success' : 'error'}`}>
                              <span className="bot-type">{botResult.botType}:</span>
                              <span className="bot-action">{botResult.action || 'N/A'}</span>
                              {botResult.errorMessage && (
                                <span className="bot-error">({botResult.errorMessage})</span>
                              )}
                            </div>
                          ))}
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
    </div>
  );
};

export default TestRunner; 