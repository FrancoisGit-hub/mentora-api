# Dev test scripts — Mentora

Manual test utilities. Not part of the runtime app.

## test-signalr.html

Standalone HTML page to test the SignalR `/hubs/chat` Hub from a browser without needing the mobile app.

Usage:
1. Run the API locally (`dotnet run --project Mentora.API`) or point at the VPS.
2. Open `test-signalr.html` directly in Chrome/Firefox (`file://...` or via any static server).
3. Paste a valid JWT (obtain it via the OTP login flow).
4. Paste the conversation UUID you want to test against.
5. Click Connect → Join → Send / MarkAsRead.

This page is also useful as a reference implementation for PJ's Flutter SignalR integration.
