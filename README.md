# AZURE — Minecraft лаунчер (офлайн)

Minecraft 1.16.5 – 1.20.1 · Vanilla / Fabric / Forge · C# + WPF (.NET 8)

## Как получить AZURE-Launcher.exe (ничего ставить на ПК не нужно)
1. Создай репозиторий на GitHub, залей туда все файлы из этой папки (включая `.github`).
2. Вкладка **Actions** → **build** → **Run workflow** (или просто сделай push в `main`).
3. Когда сборка позеленеет — открой её и скачай артефакт **AZURE-Launcher** → внутри `AZURE-Launcher.exe`.

Либо локально: `dotnet publish Luba.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out`

## Что умеет
- Офлайн-аккаунты (ник + корректный offline-UUID, как у обычного оффлайн-режима)
- Сборки: любое количество, у каждой своя папка (`%AppData%\.azure\instances\...`) с mods/saves
- Автоустановка: Minecraft, библиотеки, ресурсы, Java 8/17, Fabric, Forge
- Лёгкая оптимизация: JVM-флаги G1 + (по галке) Sodium/Lithium/FerriteCore/ModernFix (Fabric) или Embeddium/FerriteCore/ModernFix (Forge) с Modrinth
- Система крашей: при вылете окно с разбором причины, crash-report и лог игры
- Настройки: ОЗУ, JVM-аргументы, свой Java, разрешение, цвет интерфейса
- Логи лаунчера: `%AppData%\.azure\logs`
