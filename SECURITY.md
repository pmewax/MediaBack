# Проверки и безопасность / Checks and security — 0.4.3 beta

## Русский

- Windows x64, WPF / .NET 10.0.13; runtime включён в MSI.
- **26 новых проверок обложек и подсказок прошли.** Общий прогон: **190 успешных проверок и 5 сбоев тестов получения фокуса Windows**. Те же пять сбоев повторились на неизменённой 0.4.2 в этой среде. Код взаимодействия с играми не менялся; полностью успешная проверка фокуса здесь не заявляется.
- Проверены быстрые смены трека, задержанные изображения, отмена устаревших чтений, обновление картинки без смены названия, отсутствие изображения и одинаковые обложки альбома. Использовались управляемые последовательности событий; реальное воспроизведение пользователя не переключалось.
- Проверены **221 ключ RU/EN**, 210 используемых ссылок и совпадение параметров.
- Сборка и проверка таблиц MSI выполнены. **Обновление 0.4.2 → 0.4.3 прошло успешно**, установленный EXE совпал с проверенной сборкой, файл настроек не изменился. Полное удаление/переустановка 0.4.3 не выполнялись.
- Microsoft Defender не обнаружил угроз в финальном MSI, код выхода 0. **Независимый аудит не проводился. MSI не подписан сертификатом издателя.** Контрольная сумма опубликована в релизе.
- Системные файлы и игры не изменяются; внедрения в процессы игр нет. Приложение работает с правами обычного пользователя. Адреса поддержки не менялись.

Если плеер не передаёт актуальную обложку Windows, приложение не может восстановить её по названию трека. Не проверены все плееры, HID-устройства, мониторы/DPI и полноэкранные игры; прямой проверки в Roblox не было.

## English

- Windows x64, WPF / .NET 10.0.13; runtime included in the MSI.
- **All 26 new artwork and tooltip checks passed.** Full run: **190 passes and 5 Windows foreground-focus test failures**. The same failures were reproduced on unchanged 0.4.2 in this environment. Game interaction code was unchanged; a fully passing focus test is not claimed.
- Controlled sequences cover rapid skips, delayed images, obsolete read cancellation, artwork-only changes, missing images and shared album art. The user's actual playback was not switched during testing.
- **221 RU/EN localization keys**, 210 references and matching placeholders checked.
- Application build and MSI tables checked. **The 0.4.2 → 0.4.3 upgrade succeeded**; the installed EXE matched the tested build and settings were unchanged. A full uninstall/reinstall was not performed.
- Microsoft Defender found no threats in the final MSI, exit code 0. **No independent security audit has been conducted. The MSI is unsigned.** A separate SHA-256 file is attached to the release.
- No Windows system file replacement or game process injection. Runs as a regular user. Donation addresses are unchanged.

If a player does not supply current artwork to Windows, the app cannot reconstruct it from a track title. Not every player, HID device, monitor/DPI setup or fullscreen game has been tested; Roblox was not tested directly.

## Исходники и сообщения об ошибках / Source and reporting

[Код / Source](source) · [Сборка / Build](README.txt) · [Подробная проверка / Validation](VALIDATION.txt) · [SHA-256](SHA256-SOURCE.txt) · [Условия / License](LICENSE.txt)

Личные настройки и журналы с данными реальных плееров исключены из публикации. Сообщайте об ошибках через Issues без паролей, токенов и приватных журналов. Для возможной уязвимости начните с краткого описания без секретов.

Private settings and logs containing real player information are excluded. Report bugs through Issues without passwords, tokens or private logs. For a potential vulnerability, start with a brief description containing no secrets.
