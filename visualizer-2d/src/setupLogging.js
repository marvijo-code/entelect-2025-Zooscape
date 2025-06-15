// setupLogging.js
// Simple global log silencer to improve performance in production / live visualizations.
// To re-enable verbose logging set VITE_DEBUG_LOGS=true when running vite or define
//   window.DEBUG_ZOOSCAPE = true in the browser console.
//
// We keep console.error untouched so real errors are still visible.

const DEBUG = import.meta.env.VITE_DEBUG_LOGS === 'true' || (typeof window !== 'undefined' && window.DEBUG_ZOOSCAPE);

if (!DEBUG && typeof console !== 'undefined') {
  ['log', 'info', 'debug', 'warn'].forEach(level => {
    const original = console[level];
    console[level] = (...args) => {
      // Keep a minimal noop in production; could buffer or send to server if desired
      // Errors are still allowed via console.error
      if (import.meta.env.DEV) {
        // In dev mode we still allow logs if ?debuglogs flag is present
        if (window.location.search.includes('debuglogs')) {
          original(...args);
        }
      }
    };
  });
}

export default DEBUG;
