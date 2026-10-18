MediaBack 0.4.3 — исправления / fixes

Обложка перечитывается при обновлении медиаданных, даже когда название не изменилось. Запоздавшие загрузки отменяются; старая картинка не переносится на новый трек. Для задержанных картинок есть три ограниченных повтора. Всплывающие подсказки панели и настроек удалены.

Artwork is refreshed on media-property changes, including artwork-only updates. Obsolete reads are cancelled; a previous track's image is cleared. Three bounded follow-ups handle delayed images. Flyout and settings hover tooltips are removed.

MediaBack 0.4.3

RU - Сборка и проверка

Требования: Windows x64, .NET SDK 10.0.401 или совместимая более новая
feature-версия .NET 10. Версия SDK задана в source/global.json;
runtime приложения закреплён на 10.0.13.

Каталоги:
  source       C# / WPF, платформенные службы, интерфейс и ресурсы
  installer    сборка установщика Windows Installer
  tools        проверка переводов и генерация QR-изображений

Из корня репозитория в PowerShell:

  .\build.ps1

Результат: app\MediaBack.exe и MediaBack-Setup.msi.
Сборка использует dotnet, makecab.exe и Windows Installer COM.
Зависимости .NET восстанавливаются из nuget.org. MSI не устанавливается
автоматически. README.txt и лицензионные TXT используются упаковщиком.

Только приложение:

  dotnet publish .\source\MediaBack.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=None -o .\app --configfile .\source\NuGet.Config

Локальный запуск с отдельными настройками:

  .\app\MediaBack.exe --profile .\test-profile --settings

Проверка переводов:

  python .\tools\verify_localization.py

Встроенные проверки:

  .\app\MediaBack.exe --self-test .\test-output\report.json

Проверки используют отдельный профиль, временно показывают окна и
регистрируют тестовые сочетания. Они читают текущее состояние аудио и
медиасессий. Отчёт может содержать названия запущенных плееров;
не публикуйте test-profile и test-output.

QR-изображения включены в source/Assets/Support. Python не требуется для
сборки приложения. Для необязательной повторной генерации нужны qrcode,
zxing-cpp и Pillow; затем: python .\tools\generate_wallet_qr.py.

Код доступен для изучения и локальной проверки на условиях LICENSE.txt.
Это source available, не open source. Уведомления о сторонних компонентах:
THIRD-PARTY-NOTICES.txt. Контрольные суммы файлов: SHA256-SOURCE.txt.

Локальная сборка не заявляется побайтно идентичной опубликованному EXE:
на результат влияют SDK, путь и упаковка. Установщик из этих исходников
содержит README.txt и условия просмотра кода вместо старых документов
релиза. Не устанавливайте тестовый MSI поверх официального пакета.

EN - Build and verify

Requirements: Windows x64 and .NET SDK 10.0.401 or a compatible later .NET 10
feature band. See source/global.json. Application runtime is pinned to 10.0.13.

Run .\build.ps1 from the repository root in PowerShell. Outputs:
app\MediaBack.exe and MediaBack-Setup.msi. Packaging uses dotnet, makecab.exe
and Windows Installer COM. .NET dependencies restore from nuget.org.
The MSI is not installed automatically. Keep README.txt and the license TXT
files in the root, as the packager includes them.

The commands above also build just the app, run it with a separate profile,
validate localization, and run the built-in checks.

Checks create an isolated profile, show windows, temporarily register test
shortcuts, and read current audio/media state. Reports may include player
names. Do not publish test-profile or test-output.

QR images are included. Python is not required to build MediaBack. Optional
QR regeneration needs qrcode, zxing-cpp and Pillow.

Source is available for review and local verification under LICENSE.txt;
it is not offered under an open-source license. Third-party terms remain
in THIRD-PARTY-NOTICES.txt. SHA256-SOURCE.txt lists individual file hashes.

Local builds are not claimed to reproduce the released binary byte for byte.
Toolchain, paths and packaging affect output. The MSI built here includes
README.txt and source review terms instead of the older release documents.
Do not install a test MSI over the official package.
