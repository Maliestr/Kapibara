# Решение проблем со сборкой тестов

## Исправленные проблемы

✅ Удален неиспользуемый `using Graphic_Editor;`
✅ Убрана неиспользуемая переменная `parent` в методах `GetElementWidth` и `GetElementHeight`

## Частые ошибки сборки и их решения

### 1. Ошибка: "Не удается найти тип или имя пространства имен"

**Решение:**
- Убедитесь, что проект `Graphic Editor` собран успешно
- Проверьте ссылку на проект в тестовом проекте:
  - Правой кнопкой на `GraphicEditor_Tests` → `Properties` → `References`
  - Убедитесь, что есть ссылка на `Graphic Editor`

### 2. Ошибка: "Не удается найти файл пакетов NuGet"

**Решение:**
- В Visual Studio: правой кнопкой на решение → `Restore NuGet Packages`
- Или вручную проверьте, что все пакеты в папке `..\Kapibara\packages\` существуют

### 3. Ошибка: "CS0246: Не удается найти тип или имя пространства имен 'Microsoft.VisualStudio.TestTools.UnitTesting'"

**Решение:**
- Убедитесь, что установлены пакеты:
  - `MSTest.TestFramework` версии 4.0.2
  - `MSTest.TestAdapter` версии 4.0.2
- Восстановите пакеты NuGet

### 4. Ошибка: "CS0234: Имя типа или пространства имен 'System.Windows' не существует"

**Решение:**
- Убедитесь, что в проекте есть ссылки на:
  - `PresentationCore`
  - `PresentationFramework`
  - `WindowsBase`
- Проверьте файл `.csproj` - эти ссылки должны быть в секции `<Reference>`

### 5. Ошибка компиляции из-за несовместимости версий .NET Framework

**Текущие версии:**
- Основной проект: .NET Framework 4.8
- Тестовый проект: .NET Framework 4.7.2

**Решение:**
- Это нормально, 4.7.2 совместим с 4.8
- Если есть проблемы, можно обновить тестовый проект до 4.8

## Как проверить ошибки сборки

1. **В Visual Studio:**
   - Откройте `View` → `Error List` (или `Ctrl+W, E`)
   - Все ошибки будут показаны там

2. **В Output окне:**
   - `View` → `Output` (или `Ctrl+Alt+O`)
   - Выберите "Build" в выпадающем списке
   - Там будут детальные сообщения об ошибках

3. **При сборке:**
   - `Build` → `Build Solution` (или `Ctrl+Shift+B`)
   - Ошибки появятся в Error List

## Что делать, если ошибки остаются

1. **Очистите и пересоберите:**
   - `Build` → `Clean Solution`
   - `Build` → `Rebuild Solution`

2. **Удалите папки bin и obj:**
   - Закройте Visual Studio
   - Удалите папки `bin` и `obj` в обоих проектах
   - Откройте Visual Studio и пересоберите

3. **Проверьте пути к пакетам:**
   - Убедитесь, что путь `..\Kapibara\packages\` корректен
   - Если пакеты в другом месте, обновите пути в `.csproj`

4. **Покажите конкретные ошибки:**
   - Скопируйте текст ошибок из Error List
   - Это поможет точно определить проблему

## Проверка конфигурации проекта

Убедитесь, что в файле `GraphicEditor_Tests.csproj` есть:

```xml
<Reference Include="PresentationCore" />
<Reference Include="PresentationFramework" />
<Reference Include="WindowsBase" />
<Reference Include="MSTest.TestFramework" />
```

И что проект ссылается на основной проект:

```xml
<ProjectReference Include="..\Kapibara\Graphic Editor.csproj">
  <Project>{0e2f854c-7347-4d4d-88c1-79ee8bfcfbef}</Project>
  <Name>Graphic Editor</Name>
</ProjectReference>
```

