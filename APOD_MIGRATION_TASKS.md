# APOD NASA Science migration

Журнал задач и результатов. Начало: 2026-09-29.

## Договоренности

- Основной источник: JSON `https://science.nasa.gov/wp-json/wp/v2/apod-basic/yyMMdd`, без API key.
- Существующий `IApodClient`, имена публичных методов и возвращаемые типы сохраняются.
- Вместо массового `_old` переименования: отдельный parser/source adapter, затем явное отделение legacy реализации за тем же фасадом. Legacy остается доступен, но не запускается автоматически длинной цепочкой повторов при каждом сбое.
- `url` нового JSON является ссылкой на статью, не preview. `hdurl` может быть заглушкой или video poster. Основной media block `basic_html` является источником кандидатов изображения; metadata/head/explanation не являются media block.
- Каждая завершенная задача: проверка, обновление этого журнала, отдельный commit. Без push, version bump и installer до отдельного разрешения.
- Более ранний подробный исследовательский план локально: `.codex/APOD_SOURCE_MIGRATION_PLAN.md`.

## Очередь

| ID | Задача | Зависимости | Статус |
|---|---|---|---|
| NASA-00 | Журнал и границы миграции | - | Done |
| NASA-01 | Pure JSON parser, date URL builder, offline fixtures/tests | NASA-00 | Done |
| NASA-02 | Раздельные preview/original, проверка CDN и качества | NASA-01 | Done |
| NASA-03 | Ограниченный transport, отмена, ошибки, in-flight guard | NASA-01 | Done |
| NASA-04 | Подключение source за IApodClient, сохранение legacy | NASA-02, NASA-03 | To Do |
| NASA-05 | Canonical PostUrl, backward-compatible cache, кнопка NASA | NASA-04 | To Do |
| NASA-06 | Today probe, latest, Global Random без side effects | NASA-03, NASA-04, NASA-05 | To Do |
| NASA-07 | Совместимость существующих range запросов и пагинация | NASA-04 | To Do |
| NASA-08 | Сквозная регрессия preview/download/apply/favorite/scheduler, RU/EN | NASA-05, NASA-06, NASA-07 | To Do |
| NASA-09 | Ручная проверка и выпуск | NASA-08 | To Do |
| NASA-10 | Быстрое фоновое обновление открытого месяца для всех | NASA-09 | Backlog |

## NASA-00: журнал и границы

Результат: создан этот tracked документ, приложение не изменено. Рабочая ветка `feature/nasa-science-source`. Последующие результаты фиксировать здесь; коммиты не отправлять на сервер.

## NASA-01: pure parser

Цель: преобразовать новый JSON в проверенный результат с обычной ApodEntry и отдельным canonical PostUrl, не выполняя сетевых запросов, сохранения настроек или скачивания.

Приемка:
- Exact date проверяется, invalid/missing date и неподдерживаемая структура не становятся available.
- URL builder корректен для 1995, 1999/2000, leap day; будущее определяется вызывающим workflow, не часовыми предположениями parser.
- Image извлекается из primary block; src/href различаются, когда источник действительно дает разные ссылки.
- Заглушки, og:image и изображения из explanation не становятся оригиналом.
- Video/iframe не превращается в image при наличии hdurl; text-only без изображения является unsupported, поврежденная структура остается ошибкой.
- HTML entities декодируются в ссылках и тексте; HTML не исполняется.
- Тесты offline: изображения, legacy GIF, poster video, text-only, пустой/невалидный JSON, чужая дата, опасные URL, HTML formatting.
- Старый parser и production source пока не переключены.

Результат (2026-09-29): добавлены `ApodScienceParser` и `ApodScienceRecord`. JSON десериализуется структурно через DataContractJsonSerializer. Нормализованный результат содержит обычную ApodEntry и отдельный PostUrl. Parser pure, без сети/файлов/download/apply. Ограничен размер JSON и время каждого regex; ошибочная дата, неизвестный тип, неполный HTML и опасные URL отклоняются.

Из нескольких ссылок на картинку выбирается img/src и связанный href только внутри date-bearing center в body. Head/og:image, JSON hdurl и ссылки из explanation не участвуют. Видео проверяется раньше img, пустая корректная публикация получает media_type=other. Совпадающие preview/original сохранены как есть: parser не выдумывает low-res.

Проверки: шесть новых групп smoke сначала упали на NotImplementedException (RED), затем прошли вместе со старым набором (GREEN). Четыре минимизированных synthetic fixture файла явно помечены как адаптированные, не как сырые NASA ответы. Проверены 14 невалидных вариантов, даты/век/leap day, plain text/entities, poster, text-only, архивная заглушка.

Отдельно реальный compiled parser вызван для JSON с NASA: 1995-06-16 -> e_lens.gif, 2012-03-12 -> other без image URLs, 2014-10-01 -> оригинал JPG, 2026-08-31 -> video без poster, 2026-09-27 -> M31Before_Scherer_4298.jpg. На запрос 2026-09-29 сервер на момент проверки вернул HTTP 404; parser к неуспешному ответу не применялся. Это не утверждение о доступности даты позднее.

Решения: не добавлялась новая HTML dependency и не менялись csproj; поддержан проверенный ограниченный basic_html template с timeout, а неизвестная структура вызывает ошибку вместо эвристического выбора любой картинки. JSON DTO игнорирует hdurl намеренно, поскольку он не доказывает наличие оригинала. Production ApodClient, legacy parser, scheduler и UI не изменены. Полная миграция еще не готова: NASA-02..09 остаются открыты.

Итоговая проверка NASA-01: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, smoke tests passed. `git diff --check` без ошибок whitespace (только уведомление Git об автоматической нормализации LF/CRLF). Installer не собирался, push не выполнялся.

## NASA-02: preview/original

В исследованной выборке из 10 image-постов src=href, поэтому нельзя обещать low-res только по img/src. Проверить server-side resize на NASA CDN по настоящим pixel dimensions/размеру ответа; не выдумывать URL для content/dam и сторонних host. Не менять original URL ради preview. При отсутствии server preview допустим original с ограниченным decode и кешем, но это не экономия сетевого трафика. Проверить большие, вертикальные, ultra-wide и GIF изображения. Нужна отдельная проверка перед интеграцией.

Результат (2026-09-29): добавлен pure helper `ApodScienceImageUrls`, подключен только к новому parser. Для проверенного NASA namespace JPEG/PNG формируется preview по маршруту `dynamicimage/assets/science/` с query `w=800&h=800&fit=clip` и оригинал по маршруту `content/dam/science/` без resizing. HdUrl никогда не заменяется URL preview. `ApodScienceRecord.SourcePreviewUrl/SourceOriginalUrl` сохраняют исходные ссылки отдельно. Legacy/production source еще не переключен.

Обнаружена дополнительная проблема NASA: dynamicimage без query по умолчанию уменьшает файл, поэтому href из primary HTML не всегда максимальное разрешение. Соответствие двух маршрутов проверено реальными GET и декодированием bytes в памяти, файлы в проект не сохранялись:

| Изображение | Полный content/dam файл | CDN preview 800x800 fit=clip |
|---|---|---|
| M31Before_Scherer_4298.jpg | 4298x3394; 2 938 690 bytes | 800x631; 143 142 bytes |
| Aurora_over_Fall.jpg | 1536x2048; 2 350 581 bytes | 600x800; 72 498 bytes |
| VelaSNR-3_bigCedic.jpg (2015) | 2000x1327; 1 288 891 bytes | 800x531; 161 418 bytes |
| STScI-01KX6D0XBHSYP7Q1EM5QTC43RP.png | 3505x3505; 17 298 985 bytes | 800x800; 858 836 bytes |
| e_lens.gif (1995) | 204x204; 5 775 bytes | HTTP 404; преобразование GIF отключено |

Для M31 dynamicimage без параметров дал только 1280x1010 (376 276 bytes); даже вариант w=4298&h=3394&fit=clip дал 4298x3391, а не точные размеры исходного файла. Поэтому для download выбран прямой asset, не вариант CDN с большими размерами. Это проверка размера/формата, не доказательство неизменности CDN на всех датах. Визуальная проверка ultrawide/viewer остается в NASA-08; в проверенной live-выборке ultrawide не найден.

Решения: mapping применяется только к точному HTTPS host assets.science.nasa.gov, двум проверенным path prefixes и JPEG/PNG. GIF/WebP, неизвестные host/path/query, query у static asset, подписи, дубли параметров, fit=crop и fragments остаются без изменения. Это проверенное соответствие namespace, не универсальная замена URL любых NASA файлов. Preview и original варианты сохраняют разные имена файлов, если таковы src/href. Не вводился новый пакет и не менялись csproj.

TDD: три проверки сначала упали на старом поведении, после реализации все smoke прошли. Добавлены проверки static PNG, разных preview/original файлов, сохранения source URLs и девяти случаев, которые нельзя переписывать. Старые проверки video/text-only/GIF/чужой даты остаются зелеными.

Итоговая проверка NASA-02: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, smoke tests passed. Installer, version и push не выполнялись.

Обязательство интеграции NASA-03/04/08: обработать 404/не-image response сформированного asset URL с ограниченным fallback к сохраненному source URL; для preview допустим исходник, для download нельзя молча выдавать уменьшенный dynamicimage за full-resolution original. Source URLs не терять при последующем кешировании. Helper сам не делает сеть и не гарантирует существование всех вычисленных адресов.

## NASA-03: transport

GET JSON с bounded body и общим timeout, корректные 404/429/5xx и schema errors, cancellation, ограниченная concurrency и отсутствие повторной загрузки одной даты. Никаких ключей в запросе нового источника/логах. /html и JSON не являются независимыми сервисами. Не переносить старые 30-second retry chains. Проверки fake transport; реальные запросы отдельно от offline smoke.

Результат (2026-09-29): добавлен `ApodScienceSource`. Один GET без API key, cookies и автоматических redirect; HttpClient переиспользуется. Общий сетевой бюджет 8 секунд включает очередь, headers и body. JSON ограничен 1 MiB decompressed bytes независимо от Content-Length, проверяется content type, затем вызывается существующий pure parser с exact-date validation. Image bytes transport не загружает.

Одновременно выполняются максимум два запроса разных дат на экземпляр source. Запросы одной даты делят текущую сетевую операцию, но каждый получает собственную mutable ApodEntry. Отмена одного ожидающего не отменяет остальных; отмена последнего останавливает общий запрос. Завершенные/ошибочные операции удаляются из in-flight registry, metadata cache здесь не вводится. При интеграции NASA-04 source должен переиспользоваться, а не создаваться на каждый запрос.

Ошибки: HTTP 404 -> ApodEntryUnavailableException только для запрошенной даты; 3xx/403/5xx -> ApodScienceRequestException, timeout отдельно от пользовательской отмены. Ошибочная схема/чужая дата не становятся available. Для 429 учитывается Retry-After (delta или HTTP date); при отсутствии header пауза 1 минута, невалидное отрицательное/нулевое значение дает минимум 1 секунду. Пауза общая для дат одного source, проверяется перед отправкой после очереди. Уже отправленные запросы не отзываются при получении 429 другим запросом. Автоматических повторов и скрытого обращения к legacy/html нет; постоянный negative cache по 404 не создается.

Проверки: первые две новые группы smoke сначала упали на NotImplementedException (RED), затем прошли. Третья группа добавлена для body timeout и concurrency. Offline покрыты valid JSON, pre-cancel без сети, redirects, 403/404/500/503/429, Retry-After across dates, неверный MIME/schema/date, oversized declared/actual body, timeout headers/body, освобождение body, dedup и независимость результатов, отмена одного/всех ожидающих, concurrency=2, отмена очереди и повтор после ошибки. Тестовый transport не требует изменений csproj или новых dependencies.

Live отдельно: compiled source с настоящим HttpClient получил 2026-09-27 как image примерно за 1175 ms, 2012-03-12 как other за 948 ms. Это единичные измерения, не обещание скорости. 1995-06-17 проверен как отсутствующая публикация. Никаких image downloads, изменений пользовательского кеша или обоев при live-проверке не было.

Решения: default 8 секунд и concurrency=2 выбраны как ограниченный стартовый бюджет, не как гарантированная скорость NASA; без автоматической retry chain. Microsoft отдельно указывает, что ResponseHeadersRead не покрывает timeout чтения content: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption . Поэтому body ограничен собственным token и dispose при отмене. Production ApodClient, scheduler и UI не изменены; CDN image fallback остается задачей NASA-04/08.

Итог: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, smoke tests passed. Installer/version/push не выполнялись.

## NASA-04: совместимый фасад

Сохранить sync и async методы IApodClient и их возвращаемые значения. Изолировать старый ApodClient как legacy реализацию без изменений ее поведения; новый фасад делегирует основному source. Не вводить .Result/.Wait/.GetAwaiter().GetResult() мосты. Fallback должен иметь явную политику и общий бюджет, а не бесконечные повторы. Personal API key остается настройкой legacy источника, а не обязательным ключом нового.

## NASA-05: ссылки и кеш

Сохранить canonical PostUrl в entry/cache и вернуть его через GetPostUrl/OpenPost, workflow results и NASA button. Старые записи без поля читаются. Не менять yyyy-MM-dd filenames, пути библиотеки, favorites и local files. Ошибка обновления remote metadata не удаляет старые данные. About NASA link обновить при интеграции; copy/translate получают plain text.

## NASA-06: today/latest/random

Probe проверяет точную дату публикации по JSON, не скачивает изображение и не применяет обои. Сохранить throttle, midnight bypass, guards и transient override. Latest учитывает фактическую дату, а не просто HTTP 200. Random сохраняет ограниченный reroll, общий бюджет и кеш результата для preview. Local/favorites остаются offline. Scheduler timing и apply logic не менять.

## NASA-07: месячные запросы

В текущем коде они существуют: ShouldWarmMonth -> WarmMonthAsync -> GetCalendarMonthStateAsync -> GetMonthState -> GetMonthStatus -> RefreshMonthStatus -> GetEntries. UI путь использует sync core внутри Task.Run. Warmup разрешен только с personal key; с DEMO_KEY он выключен. Существуют также async range методы.

Сохранить текущую интенсивность сети при миграции. Новый диапазон дает 25 элементов на страницу, август проверен как 25+6; читать пагинацию, проверять дату, dedup, partial failure. Не считать отсутствующие во временно неполном ответе даты unavailable. Year cache-first, stale requestVersion guards сохранить.

## NASA-08: регрессия

Проверить все потребители: download, apply (manual выключает auto), favorite-with-download, progress, latest scheduler, favorites rotation, calendar month/year, NASA, translation, About/Settings. Parser не должен сделать video poster обоями. Проверить старый cache, offline, slow network, midnight, tray restore и быструю навигацию. Новые пользовательские строки RU/EN. Build + offline smoke + отдельные live/ручные проверки; не объявлять всю миграцию готовой только по build.

## NASA-09: выпуск

Сначала ручная проверка пользователя. Затем отдельное разрешение на version/installer/tag/push. До этого каждый task commit локальный. Не запускать setup автоматически.

## NASA-10: быстрый месяц (после смены источника)

Предпочесть один диапазон с пагинацией и асинхронным обновлением кеша, а не 31 одновременный GET. Фактическое время измерять; два последовательных ответа и лимиты NASA не гарантируют те же 2 секунды, что один день. Только metadata, без image bytes. Ограничить concurrency, поддержать cancellation и отсутствие мигания, не включать сетевое сканирование целого года. Отдельно согласовать включение warmup для no-key пользователей.
