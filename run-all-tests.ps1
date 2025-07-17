# Get the list of test definitions from the API
$tests = Invoke-RestMethod -Uri 'http://localhost:5008/api/Test/definitions' -Method GET

if ($null -eq $tests) {
    Write-Host "Failed to retrieve tests or no tests found."
    exit 1
}

Write-Host "Found $($tests.Count) tests to run."

# Loop through each test and execute it
foreach ($test in $tests) {
    $testName = $test.testName
    Write-Host "--------------------------------------------------"
    Write-Host "Running test: $testName"
    
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5008/api/Test/run/$testName" -Method POST
        
        # Check the success property from the result
        if ($result.success) {
            Write-Host "Result: PASSED"
        } else {
            Write-Host "Result: FAILED"
            Write-Host "Error: $($result.errorMessage)"
        }
    } catch {
        Write-Host "Result: FAILED (Exception)"
        Write-Host "An error occurred while running test '$testName': $_"
    }
    
    Write-Host "--------------------------------------------------"
    Write-Host ""
}

Write-Host "All tests completed."
