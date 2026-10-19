# MTG Cube Studio – Prototyp v0.1

Windows-Anwendung für Magic: The Gathering Cubes, entwickelt mit C# und .NET 8 WinForms.

## Implementiert
- Neuen Set-Cube oder Mixed-Cube anlegen
- Setliste über Scryfall laden; Set-Karten über `set:CODE is:booster` importieren
- Multiplikatoren für Common, Uncommon, Rare und Mythic
- Freie Kartensuche, Kartenzahlen hinzufügen und verändern
- Tabellenansicht und experimentelle Karten-/Stapelübersicht mit Hover-Bildvorschau
- JSON-Projektdateien speichern und laden
- Einfache TXT-Liste exportieren
- Einfache Obergrenze der Boosterzahl (noch **keine** vollständige Kapazitätsberechnung)
- Automatisches Windows-GitHub-Actions-Publishing

## Noch nicht implementiert
PDF-Proxy-Export, Imports von Moxfield/Cube Cobra, echte überlappende Kartenbilder, automatische deutsche Kartenbildauflösung, regelkonforme Booster-Generierung, Draft- und Sealed-Simulator, Deckbuilder.

Die Sprachwahl wird gespeichert, wirkt sich derzeit aber noch nicht auf die geladenen Scryfall-Bilder aus. Für den Set-Import nutzt v0.1 den Scryfall-Filter `is:booster`; Sonderfälle einzelner Sets müssen vor einer Freigabe als originalgetreuer Draft-Pool überprüft werden.

## Bauen

Voraussetzung für Entwickler: Windows und .NET 8 SDK.

```powershell
dotnet restore CubeStudio/CubeStudio.csproj
dotnet run --project CubeStudio/CubeStudio.csproj
```

Portable Windows-x64-Dateien erstellen:

```powershell
dotnet publish CubeStudio/CubeStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Alternativ Repo bei GitHub hochladen: Unter **Actions → Windows Build → Run workflow** starten und das erzeugte Artifact herunterladen.

## Architektur
- `Models/`: Projekt, Karteneintrag und Booster-Regelmodelle
- `Services/ScryfallService.cs`: HTTP und JSON
- `Services/ProjectService.cs`: JSON-/TXT-Dateien
- `Services/CubeLogic.cs`: Kartenmengen, Sortierung, vereinfachte Berechnung
- `UI/MainForm.cs`: Benutzeroberfläche

## Status und Einschränkungen
Dies ist ein **ungetesteter Quellcode-Prototyp**: Im Erstellungscontainer war kein .NET SDK verfügbar. Ein erfolgreicher Windows-Build wurde noch nicht nachgewiesen. Die Bilder werden derzeit nur im Arbeitsspeicher zwischengespeichert, nicht auf der Festplatte. Nicht zum kommerziellen Verkauf nicht lizenzierter Kartenbilder gedacht. Bitte Scryfall-Nutzungsregeln und Wizards-of-the-Coast-Fan-Content-Richtlinien beachten.
