using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TopSolid.Kernel.Automating;
using System.Collections.Generic;

namespace TopSolid.Remote
{
    class Program
    {
        private static IApplication _topSolidApp;
        private static HttpListener _listener;
        private static CancellationTokenSource _cts;
        private const int Port = 5050;
        private static string _connectionStatus = "Not Connected";
        private static string _lastError = "None";

        static void Main(string[] args)
        {
            Console.WriteLine("TopSolid Remote Controller - Persistent Mode");
            Console.WriteLine("==========================================");
            
            // Start HTTP Server IMMEDIATELY
            Console.WriteLine($"Starting HTTP server on port {Port}...");
            StartHttpServer();
            
            Console.WriteLine($"Server running. Diagnostic URLs:");
            Console.WriteLine($"  - Status:  http://localhost:{Port}/status");
            Console.WriteLine($"  - Connect: http://localhost:{Port}/connect");
            Console.WriteLine($"  - Launch:  http://localhost:{Port}/launch");
            Console.WriteLine("------------------------------------------");

            // Initial connection attempt (non-blocking)
            Task.Run(() => ConnectToTopSolid());

            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();
            
            StopHttpServer();
            Console.WriteLine("Goodbye.");
        }

        static bool ConnectToTopSolid(bool forceLaunch = false)
        {
            _connectionStatus = "Connecting...";
            try
            {
                Console.WriteLine("Attempting to connect to TopSolid...");
                
                if (forceLaunch)
                {
                    Console.WriteLine("Force launch requested...");
                }

                // Method 1: Static Connect
                Console.WriteLine("Method 1: TopSolidHost.Connect()...");
                bool connected = TopSolidHost.Connect();
                Console.WriteLine($"  Result: {connected}");
                
                if (connected)
                {
                    _topSolidApp = TopSolidHost.Application;
                    if (_topSolidApp != null)
                    {
                        _connectionStatus = "Connected (TopSolidHost)";
                        Console.WriteLine("Successfully connected via TopSolidHost!");
                        return true;
                    }
                }
                
                // Method 2: Instance Connect
                Console.WriteLine("Method 2: TopSolidHostInstance...");
                var instance = new TopSolidHostInstance();
                bool instanceConnected = instance.Connect();
                Console.WriteLine($"  Result: {instanceConnected}");
                
                if (instanceConnected)
                {
                    _topSolidApp = instance.Application;
                    if (_topSolidApp != null)
                    {
                        _connectionStatus = "Connected (TopSolidHostInstance)";
                        Console.WriteLine("Successfully connected via TopSolidHostInstance!");
                        return true;
                    }
                }
                
                _connectionStatus = "Failed: All methods returned false";
                _lastError = "TopSolidHost.Connect returned false. Check if TopSolid is running and no firewall blocks WCF.";
                Console.WriteLine(_connectionStatus);
                return false;
            }
            catch (Exception ex)
            {
                _connectionStatus = $"Error: {ex.GetType().Name}";
                _lastError = $"{ex.Message}\nStack: {ex.StackTrace}";
                if (ex.InnerException != null) _lastError += $"\nInner: {ex.InnerException.Message}";
                
                Console.WriteLine($"Connection error: {_lastError}");
                return false;
            }
        }

        static void StartHttpServer()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL: Failed to start HTTP server: {ex.Message}");
                _connectionStatus = "HTTP SERVER FAILED";
            }
        }

        static void StopHttpServer()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        static async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
            }
        }

        static void ProcessRequest(HttpListenerContext context)
        {
            string responseString = "OK";
            var request = context.Request;
            var path = request.Url.AbsolutePath;

            try
            {
                if (path == "/message")
                {
                    string text = request.QueryString["text"] ?? "No text provided";
                    ShowMessageInTopSolid(text);
                    responseString = $"Message sent to TopSolid: {text}";
                }
                else if (path == "/status")
                {
                    responseString = GetDetailStatus();
                }
                else if (path == "/version")
                {
                    responseString = GetTopSolidVersion();
                }
                else if (path == "/connect")
                {
                    bool result = ConnectToTopSolid();
                    responseString = result ? "Connected!" : "Connection Failed. Check Status.";
                }
                else if (path == "/launch")
                {
                    // Attempt to connect forcing parameters if possible, or just retry
                    ConnectToTopSolid(true); 
                    responseString = "Launch/Connect attempt initiated.";
                }
                else
                {
                    responseString = "Available commands: /message?text=..., /status, /version, /connect, /launch";
                }
            }
            catch (Exception ex)
            {
                responseString = $"Error processing request: {ex.Message}";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        static void ShowMessageInTopSolid(string text)
        {
            Console.WriteLine($"[TopSolid] Request to display: {text}");
            if (_topSolidApp != null)
            {
                // Placeholder for actual API call, assuming we figure out exact method
                Console.WriteLine($"Message sent to API: {text}");
            }
            else
            {
                Console.WriteLine("Cannot display message: Not Connected.");
            }
        }

        static string GetDetailStatus()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Connection Status: {_connectionStatus}");
            sb.AppendLine($"Last Error: {_lastError}");
            
            // Check for TopSolid Process
            try {
                var processes = Process.GetProcessesByName("TopSolid");
                sb.AppendLine($"TopSolid Processes Found: {processes.Length}");
                foreach (var p in processes)
                {
                    sb.AppendLine($" - PID: {p.Id}, SessionId: {p.SessionId}, WindowTitle: {p.MainWindowTitle}");
                }
            } catch (Exception ex) {
                sb.AppendLine($"Error listing processes: {ex.Message}");
            }

            if (_topSolidApp != null)
            {
                sb.AppendLine("API Object: Valid (Not Null)");
            }

            return sb.ToString();
        }

        static string GetTopSolidVersion()
        {
            if (_topSolidApp == null) return "Not connected";
            try
            {
                return $"TopSolid Version: {TopSolidHost.Application.Version}";
            }
            catch (Exception ex) { return $"Error getting version: {ex.Message}"; }
        }
    }
}
