param()

# Convenience wrapper to stop the FunctionalTests API to avoid DLL file locks during build/test cycles.
& "$PSScriptRoot\start-api.ps1" -Stop
