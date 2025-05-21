import React, { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';

const ConnectionDebugger = ({ connection, hubUrl }) => {
  const [connectionState, setConnectionState] = useState("Not connected");
  const [debugInfo, setDebugInfo] = useState([]);
  const [isExpanded, setIsExpanded] = useState(false);

  useEffect(() => {
    if (!connection) {
      setConnectionState("No connection object");
      return;
    }

    const updateState = () => {
      const state = connection.state;
      if (state === signalR.HubConnectionState.Connected) {
        setConnectionState("Connected");
      } else if (state === signalR.HubConnectionState.Connecting) {
        setConnectionState("Connecting...");
      } else if (state === signalR.HubConnectionState.Disconnected) {
        setConnectionState("Disconnected");
      } else if (state === signalR.HubConnectionState.Reconnecting) {
        setConnectionState("Reconnecting...");
      } else {
        setConnectionState("Unknown state");
      }
    };

    updateState();

    // Get the event handlers registered on the connection
    const getRegisteredHandlers = () => {
      try {
        // This is accessing internal state - may not work in all versions
        if (connection._methods) {
          return Object.keys(connection._methods);
        }
        return [];
      } catch (e) {
        return [];
      }
    };

    const addDebugMessage = (message) => {
      setDebugInfo(prev => [...prev, {
        timestamp: new Date().toISOString(),
        message
      }]);
    };

    // Try to hook into connection events for debug messages
    try {
      addDebugMessage(`Initial connection state: ${connection.state}`);
      addDebugMessage(`Connection to hub URL: ${hubUrl}`);
      
      const handlers = getRegisteredHandlers();
      addDebugMessage(`Registered handlers: ${handlers.join(', ') || 'None'}`);
    } catch (e) {
      addDebugMessage(`Error getting connection info: ${e.message}`);
    }

    // Check every 3 seconds
    const timer = setInterval(() => {
      updateState();
    }, 3000);

    return () => {
      clearInterval(timer);
    };
  }, [connection, hubUrl]);

  const toggleExpand = () => {
    setIsExpanded(!isExpanded);
  };

  const handleReconnect = () => {
    if (!connection) return;
    
    if (connection.state === signalR.HubConnectionState.Disconnected) {
      connection.start()
        .then(() => {
          setDebugInfo(prev => [...prev, {
            timestamp: new Date().toISOString(),
            message: "Successfully reconnected"
          }]);
          connection.invoke("RegisterVisualiser").catch(err => {
            setDebugInfo(prev => [...prev, {
              timestamp: new Date().toISOString(),
              message: `Error registering visualizer: ${err.message}`
            }]);
          });
        })
        .catch(err => {
          setDebugInfo(prev => [...prev, {
            timestamp: new Date().toISOString(),
            message: `Error reconnecting: ${err.message}`
          }]);
        });
    }
  };

  return (
    <div className="connection-debugger">
      <div className="debugger-header" onClick={toggleExpand}>
        <span className={`connection-status ${connectionState === "Connected" ? "connected" : "disconnected"}`}>
          {connectionState}
        </span>
        <button className="toggle-button">{isExpanded ? "Hide details" : "Show details"}</button>
      </div>
      
      {isExpanded && (
        <div className="debugger-content">
          <div className="connection-actions">
            <button onClick={handleReconnect} disabled={connectionState === "Connected"}>
              Force Reconnect
            </button>
          </div>
          
          <div className="debug-log scroll-content">
            {debugInfo.map((item, index) => (
              <div key={index} className="log-entry">
                <span className="timestamp">{item.timestamp.split('T')[1].split('.')[0]}</span>
                <span className="message">{item.message}</span>
              </div>
            ))}
            {debugInfo.length === 0 && <p>No debug information available</p>}
          </div>
        </div>
      )}
    </div>
  );
};

export default ConnectionDebugger; 