# Castor Engine

Castor Engine is the real-time media execution engine for Castor Studio.

It provides a stable boundary between the Castor Studio desktop application and the native media stack used for capture, scene composition, audio mixing, preview rendering, encoding, recording, and live streaming.

The engine is designed to use [libobs](https://obsproject.com/) internally while exposing a small, versioned API owned by Castor Studio.

> [!IMPORTANT]
> This repository is currently in its bootstrap phase. The initial milestone is to validate the complete native-to-.NET integration before implementing media features.

## Repository responsibilities

This repository owns:

- the native Castor Engine implementation;
- the C ABI exposed by the native DLL;
- the managed .NET wrapper;
- the integration with libobs and selected OBS modules;
- native and managed integration tests;
- assembly of the Windows x64 runtime;
- creation of the Castor Engine NuGet packages.

This repository does not contain:

- the Avalonia user interface;
- Castor Studio ViewModels or navigation;
- business and product workflows;
- AI models or AI decision services;
- user authentication and account management.
