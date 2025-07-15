# Get all test definitions from the API
$tests = Invoke-RestMethod -Uri "http://localhost:5008/api/test/definitions" -Method GET

Write-Output "=================================================="
Write-Output "Starting full functional test suite..."
Write-Output "Found $($tests.Count) tests to run."
Write-Output "=================================================="

$passedCount = 0
$failedCount = 0
$failedTests = New-Object System.Collections.ArrayList

# Loop through each test and run it
foreach ($test in $tests) {
    $testName = $test.testName
    $uri = "http://localhost:5008/api/test/run/$testName"

    try {
        Write-Output "Running test: $testName..."
        $result = Invoke-RestMethod -Uri $uri -Method POST

        if ($result.success) {
            Write-Host -ForegroundColor Green "  [PASS] $testName"
            $passedCount++
        } else {
            Write-Host -ForegroundColor Red "  [FAIL] $testName"
            Write-Host -ForegroundColor Red "    Reason: $($result.errorMessage)"
            $failedCount++
            [void]$failedTests.Add($testName)
        }
    } catch {
        Write-Host -ForegroundColor Red "  [FAIL] $testName"
        Write-Host -ForegroundColor Red "    Reason: An error occurred while running the test. $_"
        $failedCount++
        [void]$failedTests.Add($testName)
    }
}

Write-Output "=================================================="
Write-Output "Test suite finished."
Write-Output "=================================================="
Write-Host -ForegroundColor Green "Passed: $passedCount"
Write-Host -ForegroundColor Red "Failed: $failedCount"

if ($failedCount -gt 0) {
    Write-Host -ForegroundColor Yellow "Failed tests:"
    foreach ($failedTest in $failedTests) {
        Write-Host -ForegroundColor Yellow "  - $failedTest"
    }
    # Exit with a non-zero code to indicate failure, useful for CI/CD
    exit 1
} else {
    Write-Host -ForegroundColor Green "All tests passed successfully!"
    exit 0
}
