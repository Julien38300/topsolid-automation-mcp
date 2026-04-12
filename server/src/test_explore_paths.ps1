$serverPath = "C:\Users\jup\OneDrive\Cortana\TopSolidMcpServer\src\bin\Debug\net48\TopSolidMcpServer.exe"

function Send-McpRequest($method, $params) {
    $request = @{
        jsonrpc = "2.0"
        id = 1
        method = $method
        params = $params
    } | ConvertTo-Json -Compress
    
    $request | & $serverPath | Out-String
}

Write-Host "--- Test Explore Paths (IPdm -> String) ---"
Send-McpRequest "tools/call" @{ 
    name = "topsolid_explore_paths"; 
    arguments = @{ 
        sourceType = "IPdm"; 
        targetType = "String"; 
        maxDepth = 3 
    } 
}

Write-Host "`n--- Test No Path (String -> IPdm) ---"
Send-McpRequest "tools/call" @{ 
    name = "topsolid_explore_paths"; 
    arguments = @{ 
        sourceType = "String"; 
        targetType = "IPdm"; 
        maxDepth = 2 
    } 
}
