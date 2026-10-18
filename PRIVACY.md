# Приватность MediaBack

MediaBack 0.4.3 работает локально. В коде приложения нет телеметрии, рекламных SDK, учётных записей, сетевого сервиса или автоматической отправки отчётов.

Для показа музыки приложение читает метаданные и обложку системных Windows Media Session и отправляет выбранному плееру команды управления. Громкость и устройства доступны через Windows Core Audio. Сам плеер может обращаться в интернет по своим правилам.

Для подписи сайта приложение может сопоставлять название трека с заголовком видимого окна браузера. История, содержимое страниц и список вкладок не читаются. Этот способ не всегда определяет фоновую вкладку; показ сайта отключается в настройках.

Обработчики клавиш и HID нужны для медиаклавиш и назначенных команд. Обычный ввод не записывается. Во время явной записи нового сочетания клавиши используются для выбора этой команды.

Настройки, импортированные фоны и локальные диагностические файлы хранятся в `%LOCALAPPDATA%\MediaBack`. Удаление приложения сохраняет этот профиль. Его можно удалить вручную после завершения программы.

Кнопка копирования адреса поддержки записывает выбранный публичный адрес в буфер обмена. Приложение не подключается к кошельку и не совершает переводы. QR содержит адрес без суммы и сети; сеть выбирается отдельно.

Не прикладывайте к публичным Issues личные файлы, полные диагностические журналы или снимки с приватными данными. GitHub и используемые вами криптосервисы обрабатывают данные по собственным правилам.

## English — MediaBack privacy

MediaBack 0.4.3 works locally. The application has no telemetry, advertising SDKs, user accounts, network service or automatic report submission.

It reads track metadata and artwork from Windows Media Session and sends playback commands to your selected player. Volume and audio devices use Windows Core Audio. The player itself may access the internet under its own policies.

To identify a website, MediaBack can match the track title to the title of a visible browser window. It does not read browsing history, page contents or the tab list. Background tabs cannot always be identified. Website labels can be disabled in settings.

Keyboard and HID handlers support media keys and assigned commands. Ordinary typing is not recorded. While you explicitly record a new shortcut, key presses are used to choose that binding. Game interaction mode temporarily activates the flyout and captures pointer input for its controls; it does not inject code into a game.

Settings, imported backgrounds and local diagnostic files are stored in `%LOCALAPPDATA%\MediaBack`. Uninstalling keeps this profile; you can remove it manually after quitting the app.

Copying a donation address writes the selected public address to the clipboard. MediaBack does not connect to a wallet or execute transactions. QR codes contain an address without an amount or network; the network is selected separately.

Do not attach personal files, complete diagnostic logs or screenshots with private data to public Issues. GitHub and any cryptocurrency services you use process data under their own policies.
