import React, { useState } from 'react';
import PropTypes from 'prop-types';
import './JsonPasteLoader.css';

const JsonPasteLoader = ({ onLoadJson, onError }) => {
  const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

  const [jsonInput, setJsonInput] = useState('');

  const handleInputChange = (event) => {
    setJsonInput(event.target.value);
  };

  const isLikelyFilePath = (text) => {
    // Simple check: starts with C:\, D:\, /, or contains .json or .log extension with slashes
    // This can be refined for more accuracy if needed.
    const windowsPathRegex = /^[a-zA-Z]:\\/i; // C:\, D:\ etc.
    const unixPathRegex = /^\//i; // /path/to/file
    const extensionRegex = /(\.json|\.log)$/i;
    const containsSlashes = /[\\/]/;

    return (windowsPathRegex.test(text) || unixPathRegex.test(text) || (containsSlashes.test(text) && extensionRegex.test(text)));
  };

  const handleLoadJsonFromPath = async (filePath) => {
    if (onError) onError(null); // Clear previous errors
    try {
      const response = await fetch(`${API_BASE_URL}/Replay/file/load-json?path=${encodeURIComponent(filePath)}`);
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`API Error (${response.status}): ${errorText || 'Failed to load file from path.'}`);
      }
      const parsedJson = await response.json();

      if (!parsedJson || typeof parsedJson !== 'object') {
        throw new Error('Invalid JSON structure from file: Root must be an object.');
      }
      if (!parsedJson.cells && !parsedJson.Cells) {
        throw new Error('Invalid JSON from file: "cells" or "Cells" property is missing.');
      }

      onLoadJson(parsedJson);
      setJsonInput(''); // Clear input after successful load
    } catch (error) {
      console.error('Error loading JSON from path:', error);
      if (onError) onError(`Error loading from path: ${error.message}`);
    }
  };

  const handleSubmit = () => {
    if (!jsonInput.trim()) {
      if (onError) onError('JSON input cannot be empty.');
      return;
    }
    if (isLikelyFilePath(jsonInput.trim())) {
      handleLoadJsonFromPath(jsonInput.trim());
    } else {
      try {
        const parsedJson = JSON.parse(jsonInput);
        
        if (!parsedJson || typeof parsedJson !== 'object') {
          throw new Error('Invalid JSON structure: Root must be an object.');
        }
        if (!parsedJson.cells && !parsedJson.Cells) {
          throw new Error('Invalid JSON: "cells" or "Cells" property is missing.');
        }
        
        onLoadJson(parsedJson);
        setJsonInput(''); // Clear input after successful load
      } catch (error) {
        console.error('Error parsing or validating JSON:', error);
        if (onError) onError(`Error processing JSON: ${error.message}`);
      }
    }
  };

  return (
    <div className="json-paste-loader-container className">
      <h3>Paste Game State JSON or File Path</h3>
      <p>Paste the content of a single game state JSON (one tick data) OR a full local file path (e.g., C:\path\to\your\file.json) to load and visualize it.</p>
      <textarea
        className="json-input-textarea className"
        value={jsonInput}
        onChange={handleInputChange}
        placeholder="Paste game state JSON or local file path here..."
        rows="15"
        cols="80"
      />
      <button className="load-json-button className" onClick={handleSubmit}>Load and View JSON</button>
    </div>
  );
};

JsonPasteLoader.propTypes = {
  onLoadJson: PropTypes.func.isRequired,
  onError: PropTypes.func,
};

export default JsonPasteLoader;
