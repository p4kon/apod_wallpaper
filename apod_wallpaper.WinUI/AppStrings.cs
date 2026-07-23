using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace apod_wallpaper.WinUI;

internal static class AppStrings
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Lazy<IReadOnlyDictionary<string, string>> CanonicalKeys = new(BuildCanonicalKeys);

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["About"] = "О программе",
        ["About APOD Wallpaper"] = "О программе APOD Wallpaper",
        ["Actions idle"] = "Действия ожидают",
        ["Actions ready"] = "Действия готовы",
        ["API key saved through protected backend storage."] = "API-ключ сохранен через защищенное хранилище backend.",
        ["APOD calendar browser"] = "Календарь APOD",
        ["APOD image"] = "Изображение APOD",
        ["APOD image added to favorites."] = "Изображение APOD добавлено в избранное.",
        ["APOD image removed from favorites."] = "Изображение APOD удалено из избранного.",
        ["{0} favorite images."] = "Избранных изображений: {0}.",
        ["Add local APOD images to favorites from the Calendar preview."] = "Добавляйте локальные изображения APOD в избранное из превью календаря.",
        ["Add to favorites"] = "Добавить в избранное",
        ["Adding favorite"] = "Добавление в избранное",
        ["Apply"] = "Применить",
        ["Apply latest"] = "Применить последнюю",
        ["Apply latest APOD"] = "Применить последний APOD",
        ["Applying latest APOD"] = "Установка последнего APOD",
        ["Applying wallpaper"] = "Установка обоев",
        ["Auto"] = "Авто",
        ["Auto Off"] = "Авто выкл.",
        ["Auto On"] = "Авто вкл.",
        ["Auto-check preference saved."] = "Настройка автопроверки сохранена.",
        ["Auto-check today's APOD"] = "Автопроверка сегодняшнего APOD",
        ["APOD Wallpaper"] = "APOD Wallpaper",
        ["Automatically launch APOD Wallpaper in the background."] = "Автоматически запускать APOD Wallpaper в фоновом режиме.",
        ["Available"] = "Доступно",
        ["Backend host is not available."] = "Backend-хост недоступен.",
        ["Backend host is unavailable."] = "Backend-хост недоступен.",
        ["Backend initialized"] = "Backend запущен",
        ["Browse"] = "Обзор",
        ["Build {0}.{1}"] = "Сборка {0}.{1}",
        ["Calendar"] = "Календарь",
        ["Calendar month failed"] = "Ошибка месяца календаря",
        ["Calendar unavailable"] = "Календарь недоступен",
        ["Cancel"] = "Отмена",
        ["Center"] = "По центру",
        ["Changes are saved through the backend as soon as the field is committed."] = "Изменения сохраняются через backend сразу после подтверждения.",
        ["Changes save through the backend as soon as each control is committed."] = "Изменения сохраняются через backend сразу после подтверждения.",
        ["Checking..."] = "Проверка...",
        ["Choose a date in the calendar to load APOD preview."] = "Выберите дату в календаре, чтобы загрузить превью APOD.",
        ["Choose how the selected image should fit your screen."] = "Как изображение должно помещаться на экране.",
        ["Choose or enter an images folder first."] = "Сначала выберите или введите папку изображений.",
        ["Choose what happens when you click the window close button."] = "Выберите, что происходит при нажатии кнопки закрытия окна.",
        ["Close behavior"] = "Поведение при закрытии",
        ["Configure"] = "Настроить",
        ["Configure a personal key to avoid rate limiting issues."] = "Настройте личный ключ, чтобы избежать лимитов запросов.",
        ["Contact Support"] = "Написать в поддержку",
        ["Copied"] = "Скопировано",
        ["Copy"] = "Копировать",
        ["CopyFailed"] = "Не удалось скопировать текст",
        ["Created by p4kon."] = "Разработчик: p4kon.",
        ["Custom folder: {0}"] = "Своя папка: {0}",
        ["Data provided by NASA API."] = "Данные предоставлены NASA API.",
        ["Default portable folder: {0}"] = "Папка portable по умолчанию: {0}",
        ["Default window action"] = "Действие окна по умолчанию",
        ["DEMO_KEY / no personal key"] = "DEMO_KEY / личный ключ не задан",
        ["Direct link to the original NASA APOD page"] = "Ссылка на оригинальную страницу NASA APOD",
        ["Disabled"] = "Выключено",
        ["Download"] = "Скачать",
        ["Download and add to favorites"] = "Скачать и добавить в избранное",
        ["Download folder"] = "Папка загрузки",
        ["Folder for downloaded APOD images"] = "Папка для скачанных изображений APOD",
        ["Download images manually or let APOD Wallpaper keep today's image fresh automatically."] = "Скачивайте изображения вручную или разрешите APOD Wallpaper автоматически обновлять картинку дня.",
        ["Downloaded locally"] = "Скачано локально",
        ["Downloading APOD image for {0}."] = "Скачивание изображения APOD за {0}.",
        ["Downloading and applying APOD for {0}."] = "Скачивание и установка APOD за {0}.",
        ["Downloading favorite image"] = "Скачивание изображения для избранного",
        ["Downloading image"] = "Скачивание изображения",
        ["Enabled"] = "Включено",
        ["Error"] = "Ошибка",
        ["Exit"] = "Выход",
        ["Exit application"] = "Закрывать приложение",
        ["Explanation"] = "Описание",
        ["Favorite"] = "Избранное",
        ["Favorite download failed"] = "Не удалось скачать избранное",
        ["Favorite images loaded"] = "Избранные изображения загружены",
        ["Favorite removed"] = "Удалено из избранного",
        ["Favorite saved"] = "Добавлено в избранное",
        ["Favorite unavailable"] = "Избранное недоступно",
        ["Favorite was not removed"] = "Не удалось удалить из избранного",
        ["Favorite was not saved"] = "Не удалось сохранить избранное",
        ["Favorites"] = "Избранное",
        ["Fill"] = "Заполнить",
        ["Fit"] = "По размеру",
        ["Fri"] = "Пт",
        ["French"] = "Французский",
        ["Future date"] = "Будущая дата",
        ["German"] = "Немецкий",
        ["Get API key"] = "Получить API-ключ",
        ["Get NASA API key"] = "Получить API-ключ NASA",
        ["GitHub Repository"] = "Репозиторий GitHub",
        ["Hide to tray"] = "Свернуть в трей",
        ["HtmlFallback"] = "HTML",
        ["Idle"] = "Ожидание",
        ["If the NASA page does not open in your region, VPN may be required."] = "Если страница NASA не открывается в вашем регионе, может потребоваться VPN.",
        ["Image preview and explanation"] = "Превью и описание изображения",
        ["Images directory saved."] = "Папка изображений сохранена.",
        ["Invalid key, DEMO_KEY fallback active"] = "Ключ неверный, используется DEMO_KEY",
        ["Invalid key, using DEMO_KEY"] = "Ключ неверный, используется DEMO_KEY",
        ["Italian"] = "Итальянский",
        ["Japanese"] = "Японский",
        ["Language"] = "Язык",
        ["Language preference saved."] = "Настройка языка сохранена.",
        ["Language used by APOD Wallpaper."] = "Язык приложения APOD Wallpaper.",
        ["License Agreement"] = "Лицензия",
        ["Loading backend state"] = "Загрузка состояния backend",
        ["Loading backend state..."] = "Загрузка состояния backend...",
        ["Loading cached month state"] = "Загрузка месяца из кэша",
        ["Loading favorite images"] = "Загрузка избранных изображений",
        ["Loading month..."] = "Загрузка месяца...",
        ["Loading package..."] = "Загрузка пакета...",
        ["Loading settings"] = "Загрузка настроек",
        ["Loading version..."] = "Загрузка версии...",
        ["Local"] = "Локально",
        ["Main page did not receive backend composition root arguments."] = "Главный экран не получил аргументы backend-композиции.",
        ["Manual download and apply actions"] = "Ручное скачивание и установка",
        ["Manage application behavior and wallpaper preferences."] = "Управляйте поведением приложения и настройками обоев.",
        ["Mon"] = "Пн",
        ["Month loaded"] = "Месяц загружен",
        ["Month refresh partially unavailable"] = "Месяц обновлен частично",
        ["Month refreshed"] = "Месяц обновлен",
        ["Month status idle"] = "Месяц ожидает",
        ["NASA"] = "NASA",
        ["NASA API Key"] = "API-ключ NASA",
        ["NASA has not published this date yet."] = "NASA еще не опубликовала эту дату.",
        ["NASA image available"] = "Изображение NASA доступно",
        ["No date selected yet"] = "Дата еще не выбрана",
        ["No favorite images yet"] = "Избранных изображений пока нет",
        ["No images folder configured"] = "Папка изображений не настроена",
        ["No text to copy"] = "Нет текста для копирования",
        ["No text to translate"] = "Нет текста для перевода",
        ["Not configured"] = "Не настроено",
        ["Not loaded"] = "Не загружено",
        ["Not ready yet"] = "Еще не готово",
        ["Open"] = "Открыть",
        ["Open favorite in Calendar"] = "Открыть избранное в календаре",
        ["Open in Google Translate"] = "Открыть в Google Переводчике",
        ["Open NASA page"] = "Открыть NASA",
        ["Could not open Google Translate"] = "Не удалось открыть Google Переводчик",
        ["Official Website"] = "Официальный сайт",
        ["Optional NASA API key support"] = "Поддержка личного API-ключа NASA",
        ["Only locally downloaded APOD images can be added to favorites."] = "В избранное можно добавлять только локально скачанные изображения APOD.",
        ["Paste NASA API key"] = "Вставьте API-ключ NASA",
        ["Periodically check NASA for new images and apply them automatically."] = "Периодически проверять NASA и автоматически устанавливать новые изображения.",
        ["Preview idle"] = "Превью ожидает",
        ["Preview not loaded"] = "Превью не загружено",
        ["Preview ready."] = "Превью готово.",
        ["Preview unavailable"] = "Превью недоступно",
        ["Privacy Policy"] = "Политика конфиденциальности",
        ["Product info"] = "Информация о продукте",
        ["Product info ready"] = "Информация готова",
        ["Portuguese"] = "Португальский",
        ["Ready"] = "Готово",
        ["Refreshing month in background"] = "Месяц обновляется в фоне",
        ["Reload preview"] = "Обновить превью",
        ["Remove"] = "Удалить",
        ["Remove from favorites"] = "Удалить из избранного",
        ["Removing favorite"] = "Удаление из избранного",
        ["Repository, support, licensing, and runtime service credits are available from this screen."] = "Репозиторий, поддержка, лицензия и сведения о сервисах доступны на этом экране.",
        ["Requesting the initial snapshot from apod_wallpaper.Core."] = "Запрос начального состояния из apod_wallpaper.Core.",
        ["Requesting the latest available APOD and applying it as wallpaper."] = "Запрос последнего доступного APOD и установка его как обоев.",
        ["Russian"] = "Русский",
        ["Running without package identity"] = "Запущено без идентификатора пакета",
        ["Sat"] = "Сб",
        ["Save"] = "Сохранить",
        ["Saving settings"] = "Сохранение настроек",
        ["Saving wallpaper style"] = "Сохранение режима обоев",
        ["Select"] = "Выбор",
        ["Select a translation language first"] = "Сначала выберите язык перевода",
        ["Select translation language"] = "Выберите язык перевода",
        ["Selected date: {0}"] = "Выбранная дата: {0}",
        ["Saved local APOD images appear here."] = "Здесь отображаются сохраненные локальные изображения APOD.",
        ["Settings"] = "Настройки",
        ["Settings page did not receive backend composition arguments."] = "Экран настроек не получил аргументы backend-композиции.",
        ["Settings ready"] = "Настройки готовы",
        ["Settings saved"] = "Настройки сохранены",
        ["Settings unavailable"] = "Настройки недоступны",
        ["Settings were not saved"] = "Настройки не сохранены",
        ["Show"] = "Показать",
        ["Smart"] = "Умный",
        ["Span"] = "На все экраны",
        ["Spanish"] = "Испанский",
        ["Start with Windows"] = "Запускать с Windows",
        ["Start-with-Windows preference saved."] = "Настройка запуска с Windows сохранена.",
        ["Starting backend"] = "Запуск backend",
        ["Stretch"] = "Растянуть",
        ["Sun"] = "Вс",
        ["Success"] = "Успешно",
        ["The backend did not return a valid APOD page URL."] = "Backend не вернул корректную ссылку на страницу APOD.",
        ["The date was checked and does not contain a downloadable image."] = "Дата проверена, скачиваемого изображения нет.",
        ["The day is not verified yet or background month warmup has not reached it."] = "День еще не проверен или фоновый прогрев месяца до него не дошел.",
        ["This date cannot be added to favorites."] = "Эту дату нельзя добавить в избранное.",
        ["The saved key looks invalid. The app will continue through DEMO_KEY and HTML fallback."] = "Сохраненный ключ выглядит неверным. Приложение продолжит работу через DEMO_KEY и HTML fallback.",
        ["The WinUI host created ApplicationController and loaded the initial snapshot in one backend call."] = "WinUI host создал ApplicationController и загрузил начальное состояние одним backend-вызовом.",
        ["This day resolves to image content, but the local file is not present."] = "За этот день есть изображение, но локального файла пока нет.",
        ["Thu"] = "Чт",
        ["Third-party Notices"] = "Сторонние компоненты",
        ["Tile"] = "Плитка",
        ["Text copied. Paste it into Google Translate."] = "Текст скопирован. Вставьте его в Google Переводчик.",
        ["Tray-friendly desktop behavior"] = "Работа через трей",
        ["Translate"] = "Перевести",
        ["Translation language"] = "Язык перевода",
        ["Translation language preference saved."] = "Настройка языка перевода сохранена.",
        ["Tue"] = "Вт",
        ["Unable to load favorite images."] = "Не удалось загрузить избранные изображения.",
        ["Unable to load settings."] = "Не удалось загрузить настройки.",
        ["Unable to add APOD image to favorites."] = "Не удалось добавить изображение APOD в избранное.",
        ["Unable to add favorite."] = "Не удалось добавить в избранное.",
        ["Unable to download favorite image."] = "Не удалось скачать изображение для избранного.",
        ["Unable to load favorite APOD dates."] = "Не удалось загрузить даты избранных APOD.",
        ["Unable to load favorite APOD images."] = "Не удалось загрузить избранные изображения APOD.",
        ["Unable to read favorite APOD state."] = "Не удалось прочитать состояние избранного APOD.",
        ["Unable to remove APOD image from favorites."] = "Не удалось удалить изображение APOD из избранного.",
        ["Unable to remove favorite."] = "Не удалось удалить из избранного.",
        ["Unable to open folder"] = "Не удалось открыть папку",
        ["Unable to open NASA page"] = "Не удалось открыть страницу NASA",
        ["Unable to save the wallpaper style."] = "Не удалось сохранить режим обоев.",
        ["Unknown"] = "Неизвестно",
        ["Unknown / not checked"] = "Неизвестно / не проверено",
        ["Unknown backend error while saving settings."] = "Неизвестная ошибка backend при сохранении настроек.",
        ["Valid personal key"] = "Личный ключ активен",
        ["Version {0}.{1}.{2} ({3})"] = "Версия {0}.{1}.{2} ({3})",
        ["Version {0}"] = "Версия {0}",
        ["Version info was resolved from the assembly because packaged identity was unavailable."] = "Версия получена из сборки, потому что идентификатор пакета недоступен.",
        ["Video"] = "Видео",
        ["Video or unsupported content"] = "Видео или неподдерживаемый контент",
        ["Waiting for request."] = "Ожидание запроса.",
        ["Wallpaper style"] = "Режим обоев",
        ["Wallpaper style applied"] = "Режим обоев применен",
        ["Wallpaper style was not saved"] = "Режим обоев не сохранен",
        ["We could not download or show a preview for this date."] = "Не удалось скачать или показать превью за эту дату.",
        ["We could not load the preview."] = "Не удалось загрузить превью.",
        ["We could not open the configured images folder."] = "Не удалось открыть настроенную папку изображений.",
        ["We will keep the current preview visible while the new date is resolved."] = "Текущее превью останется видимым, пока новая дата загружается.",
        ["Wed"] = "Ср",
        ["Windows did not open the images folder."] = "Windows не открыла папку изображений.",
        ["A personal NASA API key unlocks richer calendar warmup and avoids DEMO_KEY rate limits."] = "Личный API-ключ NASA включает более полный прогрев календаря и помогает избежать лимитов DEMO_KEY.",
        ["A usable local wallpaper file exists on disk."] = "На диске найден локальный файл обоев.",
        ["Applying wallpaper complete"] = "Установка обоев завершена",
        ["Auto-check updated"] = "Автообновление обновлено",
        ["Auto-check was not updated"] = "Автообновление не обновлено",
        ["Auto-check was turned off because you manually applied a specific date."] = "Автообновление отключено, потому что вы вручную установили конкретную дату.",
        ["Automatic daily check and apply is now disabled."] = "Ежедневная проверка и установка отключены.",
        ["Automatic daily check and apply is now enabled."] = "Ежедневная проверка и установка включены.",
        ["Automatic month warmup is limited with DEMO_KEY to avoid spending the shared hourly quota."] = "С DEMO_KEY прогрев месяца ограничен, чтобы не расходовать общий лимит запросов.",
        ["Backend initialization failed before the main page loaded."] = "Backend не успел инициализироваться до загрузки главного экрана.",
        ["Backend startup failed"] = "Ошибка запуска backend",
        ["Backend: {0} ms, image render: {1} ms."] = "Backend: {0} мс, рендер изображения: {1} мс.",
        ["Backend: {0} ms. The preview image failed to render in the host."] = "Backend: {0} мс. Host не смог отрисовать превью.",
        ["Background warmup is filling unknown dates and unsupported-media knowledge for this month."] = "Фоновый прогрев уточняет неизвестные даты и дни с неподдерживаемым контентом за этот месяц.",
        ["Clicking X now exits the app."] = "Крестик теперь закрывает приложение.",
        ["Clicking X now hides the app to tray."] = "Крестик теперь скрывает приложение в трей.",
        ["Clicking X hides to tray instead of exiting"] = "Крестик скрывает приложение в трей вместо закрытия",
        ["Current month is refreshed in the background because APOD can still grow tomorrow or later today."] = "Текущий месяц обновляется в фоне, потому что APOD еще может появиться сегодня или завтра.",
        ["DEMO_KEY mode detected. Automatic month warmup is limited to avoid burning the shared rate limit."] = "Обнаружен режим DEMO_KEY. Автопрогрев месяца ограничен, чтобы не расходовать общий лимит.",
        ["Disabling auto-check"] = "Отключаем автообновление",
        ["Enabling auto-check"] = "Включаем автообновление",
        ["Future apply actions will use {0}."] = "Следующие установки будут использовать режим {0}.",
        ["Green is always resolved from the local images folder, so deleted files do not lie to the user."] = "Зеленый статус всегда берется из локальной папки изображений, поэтому удаленные файлы не отображаются как сохраненные.",
        ["Hide count: {0}"] = "Скрытий: {0}",
        ["Last action: {0}"] = "Последнее действие: {0}",
        ["Last backend check (UTC): {0}"] = "Последняя проверка backend (UTC): {0}",
        ["Loading"] = "Загрузка",
        ["Loading..."] = "Загрузка...",
        ["Loading APOD preview for {0}."] = "Загружаем превью APOD за {0}.",
        ["Loading APOD preview..."] = "Загружаем превью APOD...",
        ["Loading calendar state"] = "Загрузка состояния календаря",
        ["Loading preview"] = "Загрузка превью",
        ["Local: {0}  Remote image: {1}  Unsupported: {2}  Unknown: {3}  (cache {4} ms)"] = "Локально: {0}  Фото: {1}  Видео/неподдерж.: {2}  Неизвестно: {3}  (кэш {4} мс)",
        ["Local: {0}  Remote image: {1}  Unsupported: {2}  Unknown: {3}  (cache {4} ms, warmup {5} ms)"] = "Локально: {0}  Фото: {1}  Видео/неподдерж.: {2}  Неизвестно: {3}  (кэш {4} мс, прогрев {5} мс)",
        ["No image preview"] = "Нет превью изображения",
        ["No preview image"] = "Нет изображения для превью",
        ["No preview location"] = "Нет пути к превью",
        ["Off"] = "Выкл.",
        ["On"] = "Вкл.",
        ["Package identity unavailable in this launch context."] = "Идентификатор пакета недоступен в этом режиме запуска.",
        ["Personal API key detected. Month warmup will refresh missing dates in the background."] = "Найден личный API-ключ. Прогрев месяца будет обновлять недостающие даты в фоне.",
        ["Personal NASA API key is active."] = "Личный API-ключ NASA активен.",
        ["Persisting automatic daily check through the backend facade."] = "Сохраняем ежедневную автопроверку через backend.",
        ["Persisting changes through the backend facade."] = "Сохраняем изменения через backend.",
        ["Persisting wallpaper style and reapplying the current wallpaper."] = "Сохраняем режим обоев и переустанавливаем текущие обои.",
        ["Persisting wallpaper style through the backend facade."] = "Сохраняем режим обоев через backend.",
        ["Preview failed"] = "Превью не загрузилось",
        ["Preview image unavailable"] = "Превью изображения недоступно",
        ["Preview loaded"] = "Превью загружено",
        ["Preview metadata loaded"] = "Метаданные превью загружены",
        ["Preview metadata loaded successfully."] = "Метаданные превью успешно загружены.",
        ["Preview preserved"] = "Превью сохранено",
        ["Preview rendered"] = "Превью отрисовано",
        ["Reading persisted settings and current API key state through the backend facade."] = "Читаем настройки и состояние API-ключа через backend.",
        ["Red and blue states come from backend month knowledge persisted in metadata cache."] = "Красные и синие статусы берутся из месячных метаданных backend.",
        ["Reapplying current image"] = "Переустанавливаем текущее изображение",
        ["Rendering cached knowledge first so the calendar appears immediately."] = "Сначала показываем кэшированные данные, чтобы календарь появился сразу.",
        ["Resolved {0} and downloaded a fresh local image."] = "Дата {0} обработана, свежая картинка скачана локально.",
        ["Resolved {0} successfully."] = "Дата {0} успешно обработана.",
        ["Resolved {0} using a local image file."] = "Дата {0} обработана через локальный файл изображения.",
        ["Resolving preview location..."] = "Определяем путь к превью...",
        ["Resolving..."] = "Определяем...",
        ["Restore count: {0}"] = "Восстановлений: {0}",
        ["Style saved, but wallpaper was not reapplied"] = "Режим сохранен, но обои не переустановлены",
        ["The backend did not complete the requested action."] = "Backend не завершил запрошенное действие.",
        ["The backend resolved preview metadata, but WinUI could not render the image from {0}."] = "Backend получил метаданные превью, но WinUI не смог отрисовать изображение из {0}.",
        ["The backend returned a successful workflow, but the preview image could not be opened by the host."] = "Backend успешно завершил сценарий, но host не смог открыть изображение превью.",
        ["The calendar is the primary date selector now."] = "Календарь теперь основной выбор даты.",
        ["The calendar kept its cached state because background refresh did not finish cleanly."] = "Календарь оставил кэшированное состояние, потому что фоновое обновление завершилось не полностью.",
        ["The current wallpaper was reapplied using {0}. ({1} ms)"] = "Текущие обои переустановлены в режиме {0}. ({1} мс)",
        ["The selected APOD date is currently unavailable."] = "Выбранная дата APOD сейчас недоступна.",
        ["The selected APOD entry does not contain downloadable image content."] = "Выбранная запись APOD не содержит изображения для скачивания.",
        ["The selected date does not currently resolve to previewable image content."] = "Для выбранной даты сейчас нет изображения, которое можно показать в превью.",
        ["The preview area will show the selected APOD image. Unsupported dates and not-yet-published days will use placeholders instead of noisy text."] = "В области превью появится выбранное изображение APOD. Для неподдерживаемых дат и еще не опубликованных дней будут показаны заглушки вместо лишнего текста.",
        ["The UI will ignore stale results if another date is requested before this one finishes."] = "Если выбрать другую дату раньше завершения запроса, UI проигнорирует устаревший результат.",
        ["Tray context menu opened."] = "Контекстное меню трея открыто.",
        ["Tray icon visible: {0}"] = "Иконка в трее видна: {0}",
        ["Tray icon created."] = "Значок в трее создан.",
        ["Tray spike not initialized yet."] = "Трей-проверка еще не инициализирована.",
        ["Tray spike is starting."] = "Трей-проверка запускается.",
        ["Tray spike status is unavailable."] = "Статус трей-проверки недоступен.",
        ["Backend snapshot refreshed."] = "Снимок backend обновлен.",
        ["Exit requested from tray or UI."] = "Запрошен выход из трея или UI.",
        ["Packaged startup task cannot be enabled from its current state: {0}."] = "Упакованную задачу автозапуска нельзя включить из текущего состояния: {0}.",
        ["Unable to load calendar month state."] = "Не удалось загрузить состояние месяца календаря.",
        ["Unable to load preview."] = "Не удалось загрузить превью.",
        ["Unable to load tray icon from {0}."] = "Не удалось загрузить значок трея из {0}.",
        ["Unable to load the initial snapshot."] = "Не удалось загрузить начальное состояние.",
        ["Unable to create tray icon in the packaged WinUI host."] = "Не удалось создать значок трея в упакованном WinUI-хосте.",
        ["Unable to open the current user's Windows startup registry key."] = "Не удалось открыть ключ реестра автозапуска Windows для текущего пользователя.",
        ["Unable to persist wallpaper style."] = "Не удалось сохранить режим обоев.",
        ["Unable to reapply the current wallpaper with the selected style."] = "Не удалось переустановить текущие обои с выбранным режимом.",
        ["Unable to resolve the current executable path for Windows startup registration."] = "Не удалось определить путь к текущему исполняемому файлу для регистрации автозапуска Windows.",
        ["Unable to save the auto-check preference."] = "Не удалось сохранить настройку автообновления.",
        ["Unable to shut down backend host."] = "Не удалось завершить работу backend-хоста.",
        ["Windows did not enable the packaged startup task. Current state: {0}."] = "Windows не включила упакованную задачу автозапуска. Текущее состояние: {0}.",
        ["Unavailable"] = "Недоступно",
        ["Unexpected preview workflow state."] = "Неожиданное состояние сценария превью.",
        ["Use Download, Download and apply, or Apply latest. Wallpaper style changes are saved through the backend and can reapply the current image."] = "Используйте Скачать, Применить или автообновление. Режим обоев сохраняется через backend и может переустановить текущее изображение.",
        ["Wallpaper style changed to {0}. Reapplying the selected image."] = "Режим обоев изменен на {0}. Переустанавливаем выбранное изображение.",
        ["Wallpaper style saved"] = "Режим обоев сохранен",
        ["We couldn't load this preview right now."] = "Сейчас не удалось загрузить это превью.",
        ["Window hidden to tray: {0}"] = "Окно скрыто в трей: {0}",
        ["Window hidden to tray."] = "Окно скрыто в трей.",
        ["Window restored from tray context menu."] = "Окно восстановлено из контекстного меню трея.",
        ["Window restored from tray double-click."] = "Окно восстановлено двойным щелчком по значку в трее.",
        ["Workflow succeeded, but the image did not render in the host."] = "Сценарий завершился успешно, но host не отрисовал изображение.",
        ["{0} Backend: {1} ms. Existing preview reused without rerender."] = "{0} Backend: {1} мс. Существующее превью использовано без повторной отрисовки.",
        ["{0} Backend: {1} ms. Rendering preview image..."] = "{0} Backend: {1} мс. Отрисовываем превью...",
        ["{0} complete"] = "{0}: готово",
        ["{0} failed"] = "{0}: ошибка",
        ["{0} unavailable"] = "{0}: недоступно",
        ["© 2026 APOD Wallpaper. All rights reserved."] = "© 2026 APOD Wallpaper. Все права защищены.",
        ["Unchecked"] = "Не проверено",
        ["available"] = "доступно",
        ["future"] = "будет",
        ["local"] = "локально",
        ["loading"] = "загрузка",
        ["not yet"] = "пока нет",
        ["ready"] = "доступно",
        ["unchecked"] = "не проверено",
        ["the resolved source"] = "полученного источника",
        ["unknown"] = "неизвестно",
        ["video"] = "видео",
    };

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguage { get; private set; } = apod_wallpaper.ApplicationSettingsSnapshot.LanguageEnglish;

    public static bool IsRussian => CurrentLanguage == apod_wallpaper.ApplicationSettingsSnapshot.LanguageRussian;

    public static CultureInfo DateCulture => IsRussian ? RussianCulture : EnglishCulture;

    public static void ApplyLanguage(string? language)
    {
        var normalized = apod_wallpaper.ApplicationSettingsSnapshot.NormalizeLanguage(language);
        var changed = !string.Equals(CurrentLanguage, normalized, StringComparison.Ordinal);
        CurrentLanguage = normalized;

        var culture = EnglishCulture;
        if (normalized == apod_wallpaper.ApplicationSettingsSnapshot.LanguageEnglish)
        {
            culture = EnglishCulture;
        }
        else if (normalized == apod_wallpaper.ApplicationSettingsSnapshot.LanguageRussian)
        {
            culture = RussianCulture;
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (changed)
            LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return IsRussian && Russian.TryGetValue(text, out var translated)
            ? translated
            : text;
    }

    public static string GetStableKey(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return CanonicalKeys.Value.TryGetValue(text, out var key)
            ? key
            : text;
    }

    public static string GetBackendMessageOrDefault(string? message, string fallback)
    {
        return string.IsNullOrWhiteSpace(message) ? Get(fallback) : Get(message);
    }

    public static string Format(string format, params object[] args)
    {
        return string.Format(DateCulture, Get(format), args);
    }

    public static string WallpaperStyleName(apod_wallpaper.WallpaperStyle style)
    {
        return Get(style.ToString());
    }

    private static IReadOnlyDictionary<string, string> BuildCanonicalKeys()
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in Russian)
        {
            keys.TryAdd(pair.Key, pair.Key);
            keys.TryAdd(pair.Value, pair.Key);
        }

        return keys;
    }
}
