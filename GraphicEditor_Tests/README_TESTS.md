# Инструкция по запуску Unit-тестов

## Что было исправлено

### 1. Добавлен файл конфигурации MSTestSettings.runsettings
   - Настроена параллельная обработка тестов
   - Указаны параметры для корректной работы тестового адаптера

### 2. Исправлена работа с размерами элементов в тестах
   - Добавлено принудительное измерение элементов (Measure/Arrange)
   - Это необходимо для корректной работы ActualWidth/ActualHeight в WPF

### 3. Обновлен файл проекта
   - Добавлен файл MSTestSettings.runsettings в проект

## Как запустить тесты

### Способ 1: Через Visual Studio (рекомендуется)

1. **Откройте решение:**
   - Откройте файл `Graphic Editor.sln` в Visual Studio

2. **Соберите решение:**
   - Нажмите `Ctrl+Shift+B` или меню `Build` → `Build Solution`
   - Убедитесь, что оба проекта собрались без ошибок

3. **Откройте Test Explorer:**
   - Меню `Test` → `Test Explorer` (или `Ctrl+E, T`)
   - Дождитесь обнаружения тестов (может занять несколько секунд)

4. **Запустите тесты:**
   - Нажмите кнопку "Run All" в Test Explorer
   - Или правой кнопкой на тестовом проекте → `Run Tests`

5. **Если тесты не обнаружены:**
   - Убедитесь, что проект `GraphicEditor_Tests` выбран как тестовый проект
   - Пересоберите решение (`Build` → `Rebuild Solution`)
   - В Test Explorer нажмите кнопку "Refresh" (обновить)

### Способ 2: Через командную строку (MSBuild + VSTest)

1. **Откройте Developer Command Prompt for VS:**
   - Пуск → Visual Studio → Developer Command Prompt for VS

2. **Перейдите в директорию решения:**
   ```cmd
   cd C:\Users\User\Documents\GitHub\Kapibara
   ```

3. **Соберите тестовый проект:**
   ```cmd
   msbuild "Graphic Editor.sln" /t:Build /p:Configuration=Debug
   ```

4. **Запустите тесты через VSTest:**
   ```cmd
   vstest.console.exe "..\GraphicEditor_Tests\bin\Debug\GraphicEditor_Tests.dll" /Settings:"..\GraphicEditor_Tests\MSTestSettings.runsettings"
   ```

### Способ 3: Через Test Explorer в Visual Studio Code (если установлено расширение)

1. Установите расширение `.NET Core Test Explorer` или `Test Explorer UI`
2. Откройте папку проекта
3. Тесты должны автоматически обнаружиться

## Структура тестов

### Класс: `ShapeDragTests`

Тесты проверяют функциональность перемещения фигур на Canvas:

1. **Test_Move_Ellipse_Success** - Проверяет успешное перемещение эллипса
2. **Test_Move_Ellipse_Outside_Left** - Проверяет ограничение перемещения левой границей
3. **Test_Move_Element_Not_On_Canvas** - Проверяет, что элементы не на Canvas не перемещаются
4. **Test_Move_Multiple_Shapes** - Проверяет перемещение нескольких фигур

## Возможные проблемы и решения

### Проблема: Тесты не обнаруживаются

**Решение:**
- Убедитесь, что установлены пакеты NuGet:
  - `MSTest.TestFramework` версии 4.0.2
  - `MSTest.TestAdapter` версии 4.0.2
  - `Microsoft.NET.Test.Sdk` версии 18.0.1
- Пересоберите решение
- Перезапустите Visual Studio

### Проблема: Ошибка компиляции

**Решение:**
- Убедитесь, что проект `Graphic Editor` собран успешно
- Проверьте, что ссылка на проект `Graphic Editor` в тестовом проекте корректна
- Убедитесь, что все пакеты NuGet восстановлены (правой кнопкой на решение → `Restore NuGet Packages`)

### Проблема: Тесты падают с ошибкой

**Решение:**
- Проверьте вывод тестов в Test Explorer для деталей ошибки
- Убедитесь, что все зависимости проекта доступны
- Проверьте, что `InternalsVisibleTo` атрибут в `AssemblyInfo.cs` основного проекта указывает на `GraphicEditor_Tests`

## Дополнительная информация

- **Фреймворк тестирования:** MSTest v2 (4.0.2)
- **Целевой фреймворк:** .NET Framework 4.7.2
- **Файл настроек:** `MSTestSettings.runsettings`

## Проверка работоспособности

После исправлений тесты должны:
1. ✅ Успешно компилироваться
2. ✅ Обнаруживаться в Test Explorer
3. ✅ Запускаться и выполняться
4. ✅ Показывать результаты выполнения

Если проблемы остаются, проверьте:
- Версию Visual Studio (рекомендуется 2019 или новее)
- Установленные компоненты .NET Framework
- Логи сборки на наличие ошибок

