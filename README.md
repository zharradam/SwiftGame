# Isabelle's Taylor Swift Music Quiz 🎵

A real-time multiplayer Taylor Swift song guessing game built with Angular and ASP.NET Core.

## 🎮 Play Now

👉 **[Play the game](https://zharradam.github.io/SwiftGame/)**

## 🎯 How to Play

1. Click **Play Now** and turn up your volume
2. Listen to the audio clip of a Taylor Swift song
3. Select the correct song from the 4 choices as fast as you can
4. The faster you answer, the more points you earn
5. Complete all 10 questions and see where you rank on the leaderboard!

## ✨ Features

- 🎵 1,200+ Taylor Swift songs from her entire discography
- ⚡ Speed-based scoring with exponential decay
- 🏆 Real-time leaderboard powered by SignalR
- 🎉 Fireworks celebration for top 10 finishes
- 📊 Game stats — correct answers, accuracy, response time
- 🖼️ Concert artwork with each question
- 📱 Mobile friendly

## 🛠️ Tech Stack

**Frontend**
- Angular 21
- TypeScript
- SCSS
- SignalR client
- Hosted on GitHub Pages

**Backend**
- ASP.NET Core (.NET 10)
- C# with Abstract Factory pattern
- Entity Framework Core
- SignalR
- Hosted on Azure App Service

**Database & Storage**
- PostgreSQL (Neon) — production
- SQL Server — development
- Azure Blob Storage — game images

**Music**
- iTunes Search API — song catalogue and previews
- Spotify Web API — metadata (with iTunes fallback)

## 🏗️ Architecture

The app uses the **Abstract Factory** design pattern in two places:

- **Music providers** — easily switch between Spotify and iTunes
- **Database providers** — SQL Server for dev, PostgreSQL for production

## 🚀 Running Locally

### Prerequisites
- .NET 10 SDK
- Node.js 24+
- SQL Server (LocalDB or Express)
- Angular CLI 21

### Backend
```bash
cd SwiftGame.API
dotnet restore
dotnet run
```

### Frontend
```bash
cd SwiftGame.Client
npm install
ng serve
```

The API runs on `https://localhost:7015` and the Angular app on `http://localhost:4200`.

### Database
Update `appsettings.Development.json` with your SQL Server connection string then run:
```bash
dotnet ef database update --project SwiftGame.Data.SqlServer --startup-project SwiftGame.API
```

Then seed the song catalogue via Scalar at `https://localhost:7015/scalar/v1` → `POST /api/seed/songs`.

## 📁 Project Structure

```
SwiftGame/
├── SwiftGame.API/              ← ASP.NET Core Web API
├── SwiftGame.Client/           ← Angular frontend
├── SwiftGame.Data/             ← EF Core entities & repositories
├── SwiftGame.Data.SqlServer/   ← SQL Server migrations
├── SwiftGame.Data.PostgreSql/  ← PostgreSQL migrations
└── SwiftGame.Music/            ← Music provider abstractions
    ├── Abstractions/           ← Interfaces & models
    ├── Spotify/                ← Spotify implementation
    ├── iTunes/                 ← iTunes implementation
    └── Fallback/               ← Fallback factory
```

## 🎨 Credits

- Built with ❤️ for Isabelle
- Taylor Swift artwork generated with [Ideogram](https://ideogram.ai)
- Songs and previews from the iTunes Search API

## 📄 License

Copyright (c) 2026 Michael Cherry. All rights reserved. This project is proprietary and not open for use, modification, or distribution without explicit written permission.
