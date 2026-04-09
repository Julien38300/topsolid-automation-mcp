using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EPFL.RhinoInsideTopSolid.AddIn
{
    public class HttpServer
    {
        private HttpListener _listener;
        private readonly int _port;
        private CancellationTokenSource _cts;

        public HttpServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            try
            {
                _listener.Start();
                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));
                System.Diagnostics.Debug.WriteLine($"HttpServer starting on port {_port}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Starts failed: {ex.Message}");
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped or failed
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {ex}");
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                var response = context.Response;
                string responseString = "OK";

                var request = context.Request;
                if (request.Url.AbsolutePath == "/message")
                {
                    string msg = request.QueryString["text"];
                    if (!string.IsNullOrEmpty(msg))
                    {
                        // Run on UI thread ideally, or simple MessageBox
                        // TopSolid might require UI thread for dialogs.
                        // We will use TopSolid API to show message.
                        ShowMessage(msg);
                        responseString = $"Message displayed: {msg}";
                    }
                    else
                    {
                        responseString = "Missing 'text' parameter.";
                    }
                }
                else
                {
                    responseString = "Unknown command.";
                }

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Request failed: {ex}");
            }
        }

        private void ShowMessage(string text)
        {
            // Use TopSolid API to show message if possible, or standard MessageBox
            // TopSolid.Kernel.SX.Application.ShowMessage(text) ?
            // I'll assume generic MessageBox for now or look for TS API call in next step.
            System.Windows.Forms.MessageBox.Show(text, "TopSolid AI Plugin");
        }
    }
}
