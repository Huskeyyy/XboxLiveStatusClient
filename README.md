
# 🎮 XboxLiveStatusClient

XboxLiveStatusClient is a C# .NET 8.0 class library for retrieving the **XBL service status** in real time via WebSocket from [kvchecker.com](https://kvchecker.com). (All credits go to them, this is just a wrapper) This is useful for integrating the Xbox Live status into your applications.

---

## 📦 Features

- 🔌 Connects via WebSocket to receive the service status in real-time
- ✅ Parses and classifies service states: Fully, Mostly or Inoperational

---

## 🛠️ Usage

### 1. Install / Reference

Add the library project to your solution, or reference the compiled `XboxLiveStatusClient.dll`.

### 2. Sample Code

```csharp
using XboxLiveStatusClient;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new XBLClient();
        var result = await client.GetLiveAuthStatusAsync();

        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return;
        }

        foreach (var service in result.Services)
        {
            Console.WriteLine($"{service.Name} - {service.StatusText} ({service.Description})");
        }
    }
}
```

## ⚠️ Notes
Intended for informational use – this is **NOT** an official Microsoft service

Status descriptions are parsed from raw WebSocket JSON payloads

## 📄 License
MIT License – use freely with attribution. Not affiliated with Microsoft or Xbox Live.

## 🙋‍♂️ Author
Created by Huskeyyy – feedback, contributions, and suggestions welcome!
