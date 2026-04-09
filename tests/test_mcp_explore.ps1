$request = '{"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "topsolid_explore_paths", "arguments": {"sourceType": "IElements", "targetType": "System.Collections.Generic.List", "maxDepth": 1}}}'
$request | & 'c:\Users\jup\OneDrive\Cortana\TopSolidMcpServer\src\bin\Debug\net48\TopSolidMcpServer.exe'
