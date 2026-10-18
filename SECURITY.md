# Проверки и безопасность / Checks and security — 0.4.2 beta

## Русский

- Windows x64, WPF / .NET 10.0.13, runtime включён в установщик.
- **169 автоматических проверок прошли, 0 ошибок.** Проверены настройки, медиасессии, горячие клавиши и их конфликты, скрытие и восстановление панели, поддержка, локализация, курсор и сглаживание громкости.
- Проверены полнота **221 ключа RU/EN**, совпадение параметров и переключение языка в интерфейсе.
- Курсор проверен с отдельным тестовым окном, которое скрывает указатель. Проверены явная активация панели, захват мыши, Esc, возврат фокуса и переход в другое окно. Прямая проверка в Roblox не проводилась; совместимость зависит от игры.
- Проверены таблицы MSI, а также SHA-256 файлов внутри CAB. **Обновление с 0.4.1 до 0.4.2 выполнено успешно**, установленный EXE совпал с проверенной сборкой, файл пользовательских настроек не изменился. Полное удаление и повторная установка 0.4.2 не выполнялись.
- Microsoft Defender не обнаружил угроз в финальном MSI; код выхода 0. Это не гарантирует отсутствие всех угроз и ошибок. Независимый аудит не проводился.
- Адреса поддержки не изменены. Ранее все шесть вариантов монеты/сети проверены независимым декодером QR. Принадлежность кошельков и переводы не проверялись.
- Системные файлы, игры и драйверы не изменяются; внедрения в процессы игр нет. Программа работает с правами обычного пользователя.
- **MSI не подписан сертификатом издателя.** SHA-256 опубликован в релизе отдельным файлом.

Не проверены все HID-устройства, конфигурации мониторов и DPI, версии плееров и полноэкранные игры. Сообщайте об ошибках через Issues. Не публикуйте пароли, токены, персональные данные или приватные журналы. При сообщении о возможной уязвимости сначала дайте краткое описание без секретов.

## English

- Windows x64, WPF / .NET 10.0.13; runtime included.
- **169 automated checks passed, 0 failures.** Coverage includes settings, media sessions, shortcuts and conflicts, flyout lifetime, support dialogs, localization, cursor behavior and volume smoothing.
- **221 RU/EN localization keys** checked for completeness and matching placeholders, alongside live UI language switching.
- Cursor interaction tested using a dedicated window that hides its pointer. Activation, mouse capture, Esc, focus restoration and switching to another window were checked. Roblox was not tested directly; compatibility varies by game.
- MSI tables and the SHA-256 of its packaged files were checked. **The 0.4.1 → 0.4.2 upgrade succeeded**; the installed EXE matched the tested build and the settings file remained unchanged. A full uninstall/reinstall of 0.4.2 was not performed.
- Microsoft Defender found no threats in the final MSI, exit code 0. This does not guarantee the absence of all threats or bugs. No independent security audit has been conducted.
- Donation addresses are unchanged. All six coin/network combinations were previously decoded independently from their QR images. Wallet ownership and transactions were not verified.
- No Windows system file replacement, driver modification or game process injection. Runs as a regular user.
- **The MSI is not signed by a publisher certificate.** A separate SHA-256 file is attached to the release.

Not all HID devices, monitor/DPI configurations, players or fullscreen games have been tested. Report bugs in Issues without passwords, tokens, personal data or private logs. For a potential vulnerability, start with a brief description that contains no secrets.

## Исходный код / Source review

Код 0.4.2 доступен в [source](source), сборка описана в [README.txt](README.txt). Подготовленная копия прошла все 169 встроенных проверок; локальные журналы с информацией о плеерах не опубликованы. Контрольные суммы файлов: [SHA256-SOURCE.txt](SHA256-SOURCE.txt).

The 0.4.2 source is available in [source](source), with build instructions in [README.txt](README.txt). The prepared source passed all 169 built-in checks. Logs containing real player information are excluded. File hashes: [SHA256-SOURCE.txt](SHA256-SOURCE.txt). Source review terms are in [LICENSE.txt](LICENSE.txt).
