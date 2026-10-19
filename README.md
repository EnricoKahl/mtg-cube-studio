# MTG Cube Studio v0.2 (Quellcode)

Windows-Desktop-App mit C# / .NET 8 WinForms.

## Was neu ist
- Überlappende **Kartenbild-Stapel** in neun Farbgruppen und nach Kartentyp gruppiert.
- **Kartenraster** sowie klassische Tabellenansicht, umschaltbar im Cube-Editor.
- **Klick auf eine Karte** öffnet ein kleines Overlay mit `− Anzahl +`.
- **Hover** zeigt eine große Vorschau rechts.
- **Deutsch / Englisch**: Set-Generierung fordert passende Druckversionen bei Scryfall an. Fehlt eine deutsche Ausgabe, bleibt die englische Version erhalten.
- Alte v0.1-Projektdateien funktionieren weiter. Für bereits vorhandene Cubes auf Tab 1 **Druckversionen aktualisieren** klicken und speichern.
- Beim Hinzufügen einer Einzelkarte wird nach Möglichkeit eine deutsche Ausgabe derselben Edition verwendet.

## Weiterhin enthalten
- Set-Cubes mit Seltenheitsmultiplikatoren; Mixed-Cubes mit Kartensuche.
- JSON-Projekte speichern/öffnen und TXT-Export.
- Erste Booster-Obergrenze (kein vollständiger Booster-Generator).

## Grenzen dieser Zwischenversion
- In der Stapelansicht werden Scryfall-Bilder beim Scrollen nachgeladen. Beim ersten Öffnen kann es dauern, bis alle Kartenbilder sichtbar sind.
- Kartenausgaben sind an Edition/Sammlernummer gebunden; nicht jede Karte besitzt eine deutsche Ausgabe.
- Kein Proxy-PDF, kein Draft, kein Sealed, kein Import aus Moxfield/Cube Cobra.
- Code ist **noch nicht unter Windows kompiliert oder durch einen laufenden GUI-Test verifiziert**. Den Build erledigt GitHub Actions.

## Build via GitHub Actions
1. Dateien **aus dem Ordner** `MTG-Cube-Studio/` in das Hauptverzeichnis deines GitHub-Repos übernehmen, inkl. `.github/workflows/windows.yml`.
2. Auf GitHub `Actions` → `Windows Build` → neuesten Lauf öffnen.
3. Unter `Artifacts` `MTG-Cube-Studio-win-x64` herunterladen und entpacken.
4. `MTG-Cube-Studio.exe` starten.

Bestehende `.cube.json`-Dateien vorher zusätzlich sichern.

## Projektaufbau
- `CubeStudio/Models`: persistierte Cube- und Kartenmodelle
- `CubeStudio/Services/ScryfallService.cs`: HTTP, Kartendaten, Druckversionen
- `CubeStudio/Services/CubeLogic.cs`: Zählen, Sortieren, Kategorien
- `CubeStudio/Services/ProjectService.cs`: lokale JSON-Projekte, TXT-Export
- `CubeStudio/UI/MainForm.cs`: Hauptfenster und Tabs
- `CubeStudio/UI/CubeCanvas.cs`: selbst gezeichneter Karteneditor mit Hover- und Mengen-Overlay
