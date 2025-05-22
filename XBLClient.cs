using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XboxLiveStatusClient;
public class XBLClient
{
    // Status values for each state, feel free to change them to your liking, I just used this for the DevExpress gauges
    public enum ServiceStatus
    {
        Unknown = 0, // Grey/Undefined
        Inoperational = 1, // Red
        Mostly = 2, // Amber
        Fully = 3 // Green
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XboxLiveMessageType
    {
        [EnumMember(Value = "stats")] Stats,

        [EnumMember(Value = "xbl_status")] XblStatus,

        [EnumMember(Value = "xboxlive_status")]
        XboxliveStatus,

        Unknown
    }

    public async Task<XboxLiveStatusResult> GetLiveAuthStatusAsync(int timeoutMs = 5000)
    {
        var result = new XboxLiveStatusResult();
        var url = new Uri("wss://kvchecker.com/ws/LIVEAuthentication");

        result.LastUpdated = DateTime.UtcNow;

        using (var ws = new ClientWebSocket())
        {
            ws.Options.SetRequestHeader("Origin", "https://xblstatus.com");
            var cancellationTokenSource = new CancellationTokenSource(timeoutMs);
            var buffer = new byte[8192]; // Buffer size

            try
            {
                await ws.ConnectAsync(url, cancellationTokenSource.Token);
            }
            catch (WebSocketException ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to connect to WebSocket: {ex.Message}";
                return result; // Early return in case of connection failure
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Unexpected error during WebSocket connection: {ex.Message}";
                return result; // Early return in case of unexpected failure
            }

            var dataReceived = new TaskCompletionSource<bool>();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        var receiveResult = await ws.ReceiveAsync(segment, cancellationTokenSource.Token);

                        if (receiveResult.MessageType == WebSocketMessageType.Text)
                        {
                            var jsonContent = Encoding.UTF8.GetString(segment.Array, 0, receiveResult.Count);
                            var serializedContent = JsonSerializer.Deserialize<XboxLiveStatusResponse>(jsonContent);

                            if (serializedContent == null)
                            {
                                result.ErrorMessage = "Invalid data received from WebSocket.";
                                dataReceived.TrySetResult(false); // Signals that the data wasn't procesed
                                return;
                            }

                            if (serializedContent.Type != XboxLiveMessageType.Unknown &&
                                serializedContent.Services != null)
                            {
                                foreach (var service in serializedContent.Services)
                                {
                                    var status = DetermineServiceStatus(service);

                                    result.Services.Add(new XboxLiveService
                                    {
                                        Name = service.Name,
                                        Description = service.Description,
                                        IsOperational = service.IsOperational,
                                        Status = status,
                                        StatusText = GetStatusText(status)
                                    });
                                }

                                result.Success = true;
                                dataReceived.TrySetResult(true);
                            }
                            else
                            {
                                result.Success = false;
                                result.ErrorMessage = "Invalid response format received.";
                                dataReceived.TrySetResult(false); // Signals a failure due to bad format
                            }
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"WebSocket error while receiving data: {ex.Message}";
                    dataReceived.TrySetResult(false); // Signals a failure due toa  WebSocket error
                }
                catch (TaskCanceledException)
                {
                    result.Success = false;
                    result.ErrorMessage = "Operation timed out while receiving data.";
                    dataReceived.TrySetResult(false); // Signals a failure due to a timeout
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Unexpected error while reading data: {ex.Message}";
                    dataReceived.TrySetResult(false); // Signals a failure due to an unexpected error
                }
            });

            try
            {
                // Awaits TaskCompletion to signal cancellation or completion
                await dataReceived.Task;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error while waiting for WebSocket data: {ex.Message}";
            }

            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error closing WebSocket: {ex.Message}";
            }
        }

        return result;
    }

    // Determines service status based on description & status, this can be improved further but for now it works
    private ServiceStatus DetermineServiceStatus(XboxLiveServiceStatus service)
    {
        if (!service.IsOperational)
        {
            return ServiceStatus.Inoperational;
        }

        if (!string.IsNullOrEmpty(service.Description) && service.Description.Contains("Mostly"))
        {
            return ServiceStatus.Mostly;
        }

        return ServiceStatus.Fully;
    }

    // Gets the status text based on the service status
    private string GetStatusText(ServiceStatus status)
    {
        switch (status)
        {
            case ServiceStatus.Fully:
                return "Fully Operational";
            case ServiceStatus.Mostly:
                return "Mostly Operational";
            case ServiceStatus.Inoperational:
                return "Inoperational";
            default:
                return "Unknown";
        }
    }

    public class XboxLiveServiceStatus
    {
        [JsonPropertyName("name")]
        public string Name
        {
            get;
            set;
        }

        [JsonPropertyName("description")]
        public string Description
        {
            get;
            set;
        }

        [JsonPropertyName("color")]
        public string Color
        {
            get;
            set;
        }

        public bool IsOperational => Color == "#0c0";
    }

    public sealed class XboxLiveStatusResponse
    {
        [JsonPropertyName("message_type")]
        public string MessageTypeString
        {
            get;
            set;
        } = string.Empty;

        [JsonIgnore]
        public XboxLiveMessageType Type
        {
            get
            {
                if (Enum.TryParse<XboxLiveMessageType>(MessageTypeString, true, out var result))
                {
                    return result;
                }

                if (MessageTypeString.Contains("status"))
                {
                    return XboxLiveMessageType.XboxliveStatus;
                }

                return XboxLiveMessageType.Unknown;
            }
        }

        [JsonPropertyName("services")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public XboxLiveServiceStatus[] Services
        {
            get;
            set;
        }
    }

    public class XboxLiveService
    {
        public string Name
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public bool IsOperational
        {
            get;
            set;
        }

        public ServiceStatus Status
        {
            get;
            set;
        } = ServiceStatus.Unknown;

        public string StatusText
        {
            get;
            set;
        } = "Unknown";
    }

    public class XboxLiveStatusResult
    {
        public List<XboxLiveService> Services
        {
            get;
            set;
        } = new List<XboxLiveService>();

        public bool Success
        {
            get;
            set;
        }

        public string ErrorMessage
        {
            get;
            set;
        }

        public DateTime LastUpdated
        {
            get;
            set;
        }
    }
}
