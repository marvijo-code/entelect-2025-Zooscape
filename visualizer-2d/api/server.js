const express = require('express');
const cors = require('cors');
const path = require('path');
const fsPromises = require('fs').promises; // Use promises API for async operations
const fsSync = require('fs'); // For synchronous operations like existsSync

const app = express();
const port = process.env.PORT || 5008; // API will run on port 5008

// --- Configuration ---
// The absolute path to the root directory where all game logs are stored.
const LOGS_BASE_DIRECTORY = path.resolve(process.env.LOGS_DIR || 'C:/dev/2025-Zooscape/logs');
console.log(`[API DEBUG] Attempting to use LOGS_BASE_DIRECTORY: ${LOGS_BASE_DIRECTORY}`);
console.log(`[API] Serving logs from: ${LOGS_BASE_DIRECTORY}`);

// --- Middleware ---
app.use(cors({
    origin: ['http://localhost:5173', 'http://localhost:3000'] // Allow your React app's origin
}));
app.use(express.json()); // To parse JSON bodies

// --- Helper Functions ---
async function getLogFilePath(runId, logFilename) {
    // Basic security check to prevent path traversal
    if (runId.includes('..') || logFilename.includes('..')) {
        throw { status: 400, message: 'Invalid path components.' };
    }

    const filePath = path.join(LOGS_BASE_DIRECTORY, runId, logFilename);
    try {
        await fsPromises.access(filePath); // Check if file exists and is accessible
    } catch (error) {
        throw { status: 404, message: `Log file not found: ${runId}/${logFilename}` };
    }
    return filePath;
}

// --- API Endpoints ---

app.get('/api/health', (req, res) => {
    res.json({ status: 'API is running' });
});

app.get('/api/log_runs', async (req, res, next) => {
    try {
        await fsPromises.access(LOGS_BASE_DIRECTORY);
    } catch (error) {
        console.error(`[API Error] Logs base directory not found: ${LOGS_BASE_DIRECTORY}`);
        return next({ status: 500, message: `Logs base directory not found: ${LOGS_BASE_DIRECTORY}` });
    }

    try {
        const items = await fsPromises.readdir(LOGS_BASE_DIRECTORY, { withFileTypes: true });
        const runDirectories = items
            .filter(item => item.isDirectory())
            .map(item => item.name);
        res.json({ runs: runDirectories.sort() });
    } catch (error) {
        console.error(`[API Error] Error reading log runs: ${error.message}`);
        return next({ status: 500, message: 'Failed to list log runs.' });
    }
});

app.get('/api/logs/:run_id', async (req, res, next) => {
    const { run_id } = req.params;
    if (run_id.includes('..')) {
        return next({ status: 400, message: 'Invalid run_id.' });
    }

    const runPath = path.join(LOGS_BASE_DIRECTORY, run_id);
    try {
        await fsPromises.access(runPath);
    } catch (error) {
        return next({ status: 404, message: `Run directory not found: ${run_id}` });
    }

    try {
        const items = await fsPromises.readdir(runPath, { withFileTypes: true });
        const logFiles = items
            .filter(item => item.isFile() && path.extname(item.name).toLowerCase() === '.json')
            .map(item => item.name);
        res.json({ run_id: run_id, log_files: logFiles.sort() });
    } catch (error) {
        console.error(`[API Error] Error listing logs in run ${run_id}: ${error.message}`);
        return next({ status: 500, message: `Failed to list logs in run ${run_id}.` });
    }
});

app.get('/api/logs/:run_id/:log_filename', async (req, res, next) => {
    const { run_id, log_filename } = req.params;
    try {
        const filePath = await getLogFilePath(run_id, log_filename);
        const data = await fsPromises.readFile(filePath, 'utf8');
        res.json(JSON.parse(data)); // Assuming log files are JSON
    } catch (error) {
        if (error instanceof SyntaxError) {
            console.error(`[API Error] JSON SyntaxError in ${run_id}/${log_filename}: ${error.message}`);
            return next({ status: 500, message: `Error decoding JSON from log file: ${run_id}/${log_filename}` });
        }
        console.error(`[API Error] Reading ${run_id}/${log_filename}: ${error.message || error}`);
        return next(error); // Forward error to the error handler
    }
});

app.get('/api/tournament_files', async (req, res, next) => {
    try {
        await fsPromises.access(LOGS_BASE_DIRECTORY);
    } catch (error) {
        console.error(`[API Error] Logs base directory for tournament files not found: ${LOGS_BASE_DIRECTORY}`);
        return next({ status: 500, message: `Logs base directory not found: ${LOGS_BASE_DIRECTORY}` });
    }

    let tournamentLogPaths = [];
    console.log('[API DEBUG /api/tournament_files] Starting scan...');

    try {
        const runDirsDirents = await fsPromises.readdir(LOGS_BASE_DIRECTORY, { withFileTypes: true });
        for (const runDirDirent of runDirsDirents) {
            if (runDirDirent.isDirectory()) {
                const runDirName = runDirDirent.name;
                const runDirPath = path.join(LOGS_BASE_DIRECTORY, runDirName);
                let lastLogFile = null;
                let maxLogNum = -1;

                try {
                    const filesInRunDir = await fsPromises.readdir(runDirPath);
                    for (const fileName of filesInRunDir) {
                        if (fileName.toLowerCase().endsWith('.json')) {
                            const baseName = fileName.slice(0, -5); // Remove .json
                            if (/^\d+$/.test(baseName)) { // Check if basename is purely numeric
                                const num = parseInt(baseName, 10);
                                if (num > maxLogNum) {
                                    maxLogNum = num;
                                    lastLogFile = fileName;
                                }
                            }
                        } else {
                            console.log(`[API DEBUG /api/tournament_files] ... skipping non-JSON file: ${fileName}`);
                        }
                    }

                    if (lastLogFile) {
                        console.log(`[API DEBUG /api/tournament_files] Selected for ${runDirName}: ${lastLogFile}`);
                        tournamentLogPaths.push(`${runDirName}/${lastLogFile}`);
                    } else {
                        console.log(`[API DEBUG /api/tournament_files] No suitable numeric JSON log found for ${runDirName}`);
                    }
                } catch (err) {
                    console.warn(`[API Warn] Could not read or process files in directory ${runDirName}: ${err.message}`);
                    // Continue to the next run directory if one fails
                }
            }
        }
        console.log(`[API DEBUG /api/tournament_files] Final tournament_files list: [${tournamentLogPaths.join(', ')}]`);
        res.json({ tournament_files: tournamentLogPaths.sort() });
    } catch (error) {
        console.error(`[API Error] Error scanning for tournament files: ${error.message}`);
        return next({ status: 500, message: 'Failed to scan for tournament files.' });
    }
});

// --- Error Handling Middleware ---
app.use((err, req, res, next) => {
    console.error('[API Global Error]', err.message || err);
    const status = err.status || 500;
    const message = err.message || 'Something went wrong on the server.';
    res.status(status).json({ error: message });
});

// --- Start Server ---
app.listen(port, () => {
    console.log(`[API] Express server listening on port ${port}`);
    // Use fsSync for the synchronous check here
    if (!fsSync.existsSync(LOGS_BASE_DIRECTORY)) {
        console.error(`[API CRITICAL] LOGS_BASE_DIRECTORY (${LOGS_BASE_DIRECTORY}) does NOT exist or is not accessible by the Node.js process at startup.`);
    } else {
        console.log(`[API INFO] LOGS_BASE_DIRECTORY (${LOGS_BASE_DIRECTORY}) confirmed to exist and is accessible at startup.`);
        try {
            const testRead = fsSync.readdirSync(LOGS_BASE_DIRECTORY);
            console.log(`[API INFO] Successfully read contents of LOGS_BASE_DIRECTORY at startup. Found ${testRead.length} items.`);
        } catch (e) {
            console.error(`[API CRITICAL] LOGS_BASE_DIRECTORY (${LOGS_BASE_DIRECTORY}) exists, but reading its contents failed at startup:`, e.message);
        }
    }
});
