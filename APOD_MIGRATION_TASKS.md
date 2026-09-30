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
| NASA-04 | Подключение source за IApodClient, сохранение legacy | NASA-02, NASA-03 | Done |
| NASA-04a | Выделение legacy и совместимый фасад | NASA-03 | Done |
| NASA-04b | Sync transport без async-to-sync bridge и новый IApodClient adapter | NASA-04a | Done |
| NASA-04c | Переключение production source после сохранения metadata и проверки потребителей | NASA-04c1, NASA-04c2, NASA-04c3, NASA-05, NASA-06, NASA-07 | Done |
| NASA-04c1 | Общий in-flight registry для sync/async одной даты | NASA-04b | Done |
| NASA-04c2 | Ограниченный asset fallback и canonical URL старого кеша | NASA-02, NASA-05, NASA-07a | Done |
| NASA-04c3 | Единая composition source/cache для workflow, probe и Random; production switch | NASA-04c1, NASA-04c2, NASA-06, NASA-07 | Done |
| NASA-05 | Canonical PostUrl, backward-compatible cache, кнопка NASA | NASA-04b | Done |
| NASA-06 | Today probe, latest, Global Random без side effects | NASA-03, NASA-04b, NASA-05 | Done (integrated) |
| NASA-07 | Совместимость существующих range запросов и пагинация | NASA-04b | Done (integrated) |
| NASA-07a | Проверенный статический кадр из metadata для 2026-08-05 | NASA-07 | Done |
| NASA-08 | Сквозная регрессия preview/download/apply/favorite/scheduler, RU/EN | NASA-04c, NASA-05, NASA-06, NASA-07 | In Progress |
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

NASA-04a (2026-09-29): старый клиент перенесен в `LegacyApodClient.cs`, содержимое проверено сравнением с HEAD: изменено только имя класса. `ApodClient` теперь совместимый фасад с внутренним constructor injection для IApodClient. Все семь методов передают параметры, результаты и исключения выбранному source, не добавляют retry/fallback и не блокируют Task. Публичный контракт IApodClient не менялся. Default пока явно LegacyApodClient: новый JSON source еще не используется приложением.

Причина разделения: граф вызовов подтвердил sync путь GetEntryByDate -> preview/download/apply/latest и sync GetEntries для month status. NASA-03 предоставляет только async transport. Переключение только GetEntryAsync оставило бы разное поведение в разных сценариях; блокирующий Task bridge противоречит договоренности. Дополнительно PostUrl/SourcePreviewUrl/SourceOriginalUrl пока живут только в ApodScienceRecord и не сохраняются через существующий entry/cache. Поэтому NASA-04 остается In Progress. NASA-04b готовит native sync transport и adapter, NASA-05/06/07 могут работать с ним до production switch; их зависимость от NASA-04 означает NASA-04b, а не завершение NASA-04c. Итоговое переключение выполняется после этих зависимостей, без циклической очереди задач.

Проверки NASA-04a: новая группа smoke сначала упала на NotImplementedException, затем прошла. Проверены все семь делегируемых методов, неизменность дат включая time component, pending async result, sync/async ошибки без fallback, null source. Полный `dotnet build apod_wallpaper.sln -c Release`: 0 warnings / 0 errors, smoke tests passed. Это проверка сохранения контрактов, не подтверждение завершения миграции. UI, scheduler, settings, version, installer и remote не менялись.

### NASA-04c: уточнение статуса и оставшихся зависимостей

NASA-04 не завершена: это родительская задача, а не только выделение legacy. NASA-04a/b выполнены; production default по-прежнему LegacyApodClient, controller использует parameterless legacy probe. Выполненные NASA-05/06/07 подготовили контракт и staged реализации, но не включили их в приложение. Ранее дополнительные условия переключения были записаны только в тексте, а таблица зависимостей была неполной. Теперь они выделены в NASA-04c1..3, а NASA-08 явно зависит от production switch. Это не пропущенная отметка Done.

NASA-04c1: sync и async GetEntry используют общий registry по DateTime.Date. Первый sync consumer запускает один native sync transport worker; остальные sync consumers ждут Monitor, async consumers ждут Task completion. Это не блокирующий async-to-sync мост. Worker независим от первого caller: отмена одного ожидания не отменяет остальных; уход последнего отменяет сеть. Результат JSON разбирается отдельно для каждого потребителя, завершенные/ошибочные flight удаляются. Range transport, общий semaphore=2, Retry-After и timeout сохранены. Очередь ThreadPool перед стартом sync worker не включена в его сетевой timeout; при отмене caller ожидание прерывается независимо от старта worker.

Проверки NASA-04c1: новый тест сначала воспроизвел два независимых sync/async запроса, затем прошел. Покрыты sync-first, async-first, два sync consumers, независимость mutable Entry, отмена первого при живых остальных, отмена последнего, повтор после отмены/503 и отсутствие постоянного кеша flight. Source/cache composition и asset fallback намеренно не помечены выполненными.

Итог NASA-04c1: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, все smoke tests passed. Изменены только source, smoke tests и журнал. Без version/installer/push.

### NASA-04c2: миграция старых записей кеша

Закрыта часть canonical URL/старых media links. При выбранном Science client удаленная старая запись без валидного canonical URL обновляется даже при заполненных title/explanation. Возвращается новая запись целиком, а не смесь старых image URLs и новых source URLs. При ошибке старая запись остается доступна и не перезаписывается; автоматическая миграция той же даты имеет session cooldown 10 минут. Force refresh сохраняет явный обход кеша. Latest использует уже полученные свежие metadata вместо предпочтения старой записи и лишнего GET.

Локальное preview с заполненными metadata не ждет сеть ради canonical URL. Явная кнопка NASA идет через новый async resolver в service/workflow и существующий ApplicationController.GetPostUrlAsync: получает exact-date JSON только при отсутствии canonical cache, сохраняет metadata с прежним LocalImagePath и возвращает ссылку на статью. При сетевой ошибке возвращается штатная ошибка OperationResult, а не случайная сегодняшняя страница. Чистый синхронный GetPostUrl остается offline lookup для payload. Legacy клиент сохраняет прежнее поведение; production source еще не переключен.

Offline тест сначала воспроизвел немигрирующий старый кеш. Проверены sync/async old-cache refresh, latest без повторного GET, reuse мигрированного кеша, offline сохранение и cooldown, быстрый local preview, явное разрешение canonical URL, повтор без сети и сохранение LocalImagePath в настоящем disk cache. Все тестовые данные в отдельной временной директории, пользовательские файлы не менялись.

Остаток NASA-04c2: asset fallback в preview/download еще не реализован. Этот этап не меняет image transport, retry chains или разрешение скачиваемого изображения. Не объявлять NASA-04c2/04c завершенными до закрытия этого остатка.

Итог проверки old-cache этапа: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, smoke tests passed. Вызов кнопки NASA в MainPage проверен по коду: используется Backend.GetPostUrlAsync. Ручной UI/network тест после production switch остается NASA-08. Без version/installer/push.

### NASA-04c2: preview asset fallback

Добавлен ApodPreviewAsset и подключен к remote preview в MainPage для записей с преобразованным SourcePreviewUrl. Кандидаты: вычисленный preview, затем максимум один исходный source URL, только если он преобразуется helper в тот же preview. Подмена другим asset запрещена. Fallback только после 404 или не-image/невалидного изображения. Redirect, 403, 429, 5xx, timeout и превышение размера не запускают резервный запрос. После провала не передаем remote URL в BitmapImage для скрытого третьего запроса.

Сетевой бюджет общий 30 секунд на headers/body обеих попыток; максимум 64 MiB на ответ. Уникальный temporary file, проверка декодируемости на background thread перед публикацией в кеше, атомарная замена старого файла. Валидный кеш повторно не скачивается. CPU decode проверяется на отмену до/после, но не объявляется жестко прерываемым. Локальные preview и непреобразованные/legacy URLs сохраняют прежний путь. Оригиналы, apply/download и scheduler этот этап не меняет.

Тест сначала упал на stub. После реализации offline проверены 404/битое изображение -> source, обе ссылки отсутствуют (ровно 2 попытки), повтор из кеша без сети, 302/403/429/503 без fallback, timeout, pre-cancellation, declared oversize, запрет другого asset, очистка temporary files. Final Release build успешен: 0 warnings / 0 errors, smoke tests passed. Новый файл ApodPreviewAsset.cs включен в отдельный локальный commit. Без version/installer/push. Live WinUI проверка остается NASA-08.

Документация: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption — ResponseHeadersRead сам не ограничивает чтение body, поэтому использован общий cancellation budget и dispose response при отмене.

Остаток NASA-04c2: bounded original download без молчаливого перехода на уменьшенный dynamicimage. NASA-04c3 и NASA-08 еще открыты. Предварительная оценка всего плана около 70–75% выполнено; это оценка объема, не готовность к релизу.

### NASA-04c2: оригиналы NASA CDN

Добавлен ApodOriginalAsset, подключен в обе ветки EnsureImageDownloaded (sync/async). Обрабатывает проверенные static content/dam ссылки NASA, совпадающие с mapping сохраненного SourceOriginalUrl. Один GET, без redirect/retry/перехода на уменьшенный dynamicimage. При 404/non-image/ошибке download завершается штатным исключением workflow: подтвержденного альтернативного full-resolution адреса у нас нет, поэтому не выдаем resized image за original. Это осознанный fail-closed выбор вместо небезопасного fallback.

Общий сетевой бюджет 2 минуты на headers/body, предел 256 MiB, потоковая запись во временный файл. После проверки image файл публикуется атомарно; до этого прежний файл не затирается. Исходные bytes сохраняются без повторного JPEG encoding. Async path сохраняет progress (bytes/total/speed). Sync использует HttpClient.Send на net8 и native HttpWebRequest на net48; нового async-to-sync bridge нет. Как и ранее, на net48 DNS может превышать платформенный timeout; decode не является жестко прерываемой операцией. GIF/непреобразованные сторонние URLs остаются в прежнем загрузчике, не расширяем эту задачу до переписывания всего legacy transport.

Тесты: сначала RED, затем проверены sync/async byte identity, HTTP 302/404/429/503 и битое изображение без downgrade/retry, сохранность существующего файла, timeout обеих веток, oversize, cleanup временных файлов. Service integration с fake wallpaper applier доказывает отсутствие apply/local-cache-path при failed download, successful apply валидного original, progress и local reuse без сети. Реальные обои и пользовательские файлы не менялись. NASA-04c2 закрыта; следующий этап NASA-04c3 — shared composition и production switch.

Финальная проверка original-download этапа: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, все smoke tests passed. Без installer/version/push; production default еще legacy до NASA-04c3.

### NASA-04c3: production switch

ApodClient по умолчанию теперь выбирает ApodScienceClient. ApplicationController создает один ApodScienceSource и один ApodMetadataCache, передает их workflow/wallpaper service, JSON-probe и Global Random. Отдельные mutable snapshots кеша для Random не создаются. Controller сохраняет проверенную metadata успешного probe; последующее preview использует общий кеш. Сам probe по-прежнему не скачивает image bytes и не вызывает apply. Default constructors вне controller тоже выбирают Science через ApodClient.

Удалена legacy API-key validation из операций публикаций (preview/download/apply/latest и scheduler lookup): новый JSON keyless, ожидание старой проверки ключа там больше не нужно. Явная проверка ключа и его настройки сохранены для legacy API, не удалены. Scheduler timing/day locks, favorite rotation, manual apply semantics, UI throttle и warmup gating не менялись. Месячные диапазоны теперь идут через новый source, но фоновый warmup для всех пользователей не включен: это NASA-10.

TDD: тест default source сначала подтвердил legacy, после переключения прошел. Controller integration на fake JSON transport проверяет probe -> shared cache -> preview без второй сети, Random с пустым кешем -> preview без второго GET, canonical NASA link без сети. Startup/test controller construction сам сеть не запускает. Старые offline smoke остаются в наборе. Реальные UI, tray restore, slow network, архивные GIF/видео и ручные download/apply сценарии требуют NASA-08/09; production switch не равен готовому релизу.

Предыдущие записи о staged/legacy default выше и ниже — история этапов до этого переключения. Текущий статус определяется таблицей и этим разделом. Без version/installer/push.

Итог NASA-04c3: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, smoke tests passed. Общая предварительная готовность около 85%; оставшийся объем — NASA-08/09, NASA-10 по-прежнему отдельный backlog после миграции.

## NASA-05: ссылки и кеш

Результат NASA-04b (2026-09-29): `ApodScienceSource.GetEntry` выполняет native sync GET и Stream.Read, без Task.Result/Wait/GetAwaiter bridge. В net8 используется HttpClient.Send, в net48 отдельный compatibility HttpWebRequest с Abort по cancellation. Общие с async: semaphore на две операции, Retry-After cooldown, HTTP/MIME/размер validation, ограничение decompressed bytes и UTF-8 decode. Общий cancellation budget включает очередь, headers и body; body dispose прерывает зависшее чтение. Для net48 остается платформенная оговорка Microsoft: DNS resolution может превысить заданный Timeout; жесткую 8-секундную гарантию на net48 не заявляем. Основное приложение работает на net8.

Добавлен staged `ApodScienceClient : IApodClient`: sync/async дата идут через новый source, ошибки не запускают legacy fallback. Latest/range пока явно делегируются legacy до NASA-06/07; API-key validation остается только в legacy. Default ApodClient не переключен. Адаптер пока возвращает Entry из record; перенос PostUrl/source URLs в сохраняемую модель обязателен в NASA-05 до production activation.

Проверки NASA-04b: тест sync bounds сначала упал на stub, после реализации прошел; добавлены sync body timeout, общий sync->async Retry-After и маршрутизация staged adapter. Проверены 302/403/404/500/503, invalid MIME/schema, declared/streamed oversize, pre-cancel, headers/body timeout, отсутствие legacy fallback при ошибке даты. Полный Release build: 0 warnings / 0 errors, smoke tests passed. Live compiled net8 sync transport: 2026-09-27=image (1458 ms), 2012-03-12=other (792 ms), 1995-06-17=ApodEntryUnavailableException (740 ms). Это отдельные измерения, не гарантия latency. Image bytes не скачивались.

Обязательное перед NASA-04c: проверить/устранить дубли одной даты при смешанных sync+async и одновременных sync вызовах. На этом шаге in-flight sharing NASA-03 сохраняется только для async; общая concurrency и cooldown уже распространяются на оба пути. Не подменять этот пункт blocking Task bridge. Также не забыть CDN fallback из NASA-02. Документация платформы: https://learn.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.timeout и https://learn.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.abort . Installer/version/remote не менялись.

Сохранить canonical PostUrl в entry/cache и вернуть его через GetPostUrl/OpenPost, workflow results и NASA button. Старые записи без поля читаются. Не менять yyyy-MM-dd filenames, пути библиотеки, favorites и local files. Ошибка обновления remote metadata не удаляет старые данные. About NASA link обновить при интеграции; copy/translate получают plain text.

Результат NASA-05 (2026-09-29): PostUrl, SourcePreviewUrl и SourceOriginalUrl добавлены в ApodEntry и ApodCachedEntry как необязательные поля. Parser заполняет Entry до выхода через staged adapter; FromEntry/ToEntry и metadata enrichment сохраняют ссылки. Upsert/UpsertRange сохраняют известный PostUrl, если новый legacy результат его не содержит. Source fallback URLs сохраняются только при неизменном соответствующем primary URL, чтобы не подменить новый asset старым. LocalImagePath и схемы имен файлов не менялись.

GetPostUrl/OpenPost используют canonical URL из кеша, повторно проверяя HTTPS, host science.nasa.gov, default port, отсутствие credentials и путь /image-article/. Произвольный URI из кеша не запускается. Существующие workflow results и кнопка NASA уже проходят через этот метод. About NASA ведет на https://science.nasa.gov/apod/. Lookup не делает дополнительных запросов. Для старого кеша без canonical URL пока остается legacy date URL; NASA-08 должна проверить получение canonical metadata для таких дат после переключения источника, не перенаправлять их молча на сегодняшнюю публикацию. Новых UI строк нет.

Проверки: тест сначала воспроизвел потерю PostUrl между record и entry. После реализации проверены disk roundtrip всех ссылок, старый JSON без новых полей, одиночный/range upsert, смена image asset, unsafe cached URL, отсутствие сети при lookup и сохранение disk cache при провале force refresh. Все файлы теста во временной директории; пользовательский кеш не менялся.

Build: единственный полный `dotnet build apod_wallpaper.sln -c Release` скомпилировал Core для обеих платформ и WinUI, но завершился с 1 ошибкой smoke из-за неполного synthetic old-cache fixture (отсутствовали старые timestamp-поля, DateTime.MinValue нельзя сериализовать при UTC+3). Fixture исправлен на реалистичный старый формат. После этого focused Release build SmokeTests успешен, 0 warnings / 0 errors, весь smoke suite passed. Production код после полного build не менялся. Повторный полный build не запускался согласно ограничению AGENTS.md. Отсутствующие timestamps в поврежденном старом кеше не исправлялись в этой задаче.

Без installer/version/push. Production default source еще legacy; NASA-04c остается открыта.

## NASA-06: today/latest/random

Probe проверяет точную дату публикации по JSON, не скачивает изображение и не применяет обои. Сохранить throttle, midnight bypass, guards и transient override. Latest учитывает фактическую дату, а не просто HTTP 200. Random сохраняет ограниченный reroll, общий бюджет и кеш результата для preview. Local/favorites остаются offline. Scheduler timing и apply logic не менять.

Результат NASA-06: в staged ApodScienceClient sync/async latest больше не делегируются legacy. Проверяется максимум четыре даты, начиная с более поздней local/UTC today, с общим бюджетом 8 секунд. Переход назад только после 404; schema mismatch/429/5xx/timeout прерывают lookup. Видео и text-only считаются настоящими публикациями, а не поводом искать более старую картинку; выбор обоев остается существующему workflow.

ApodPageAvailabilityProbe получил явно выбираемый JSON-путь через constructor injection ApodScienceSource. Проверенная публикация возвращает Available и внутреннюю Entry, 404 -> Unavailable, неверная дата/schema/timeout/HTTP failure -> Unknown. Никаких image downloads, apply или scheduler вызовов. Parameterless probe сохраняет старый путь до NASA-04c, старые response evaluator tests сохранены. UI throttle, midnight bypass, in-progress и transient override не менялись.

Global Random ограничен 10 попытками и общим бюджетом 8 секунд, отдельный probe максимум 2 секунды. Перевыбор только для подтвержденной unavailable даты, Unknown останавливает попытки, чтобы не множить запросы при outage. При JSON-probe и переданном общем metadata cache успешная Entry сохраняется для preview. Добавлены injection date picker/cache для детерминированных тестов. Local/downloaded/favorites выбор не изменен.

Важно для NASA-04c: ApplicationController пока создает legacy probe; JSON-probe и кеширование Random в приложении еще не включены. Нужно передать общий source и тот же экземпляр metadata cache, который использует wallpaper service. Отдельный долго живущий ApodMetadataCache создавать нельзя: его in-memory snapshot способен перезаписать изменения другого экземпляра. Default клиента тоже пока legacy. Изменение общего бюджета Random уже действует независимо от источника.

Проверки: RED latest-тест подтвердил прежнюю legacy-делегацию. После реализации проверены sync/async 404 lookback, остановка на 503, video latest, общий timeout, wrong-date probe, 404/timeout result, reroll отсутствующей архивной даты, сохранение canonical metadata, остановка Random на 503 и attempt cap. Тест wrong-date probe выявил необходимость отдельно ловить InvalidDataException; исправлено, весь smoke suite passed. Финальный `dotnet build apod_wallpaper.sln -c Release`: 0 warnings / 0 errors. Реальную UI activation/restore проверку выполнить после NASA-04c. Installer/version/push не выполнялись.

## NASA-07: месячные запросы

В текущем коде они существуют: ShouldWarmMonth -> WarmMonthAsync -> GetCalendarMonthStateAsync -> GetMonthState -> GetMonthStatus -> RefreshMonthStatus -> GetEntries. UI путь использует sync core внутри Task.Run. Warmup разрешен только с personal key; с DEMO_KEY он выключен. Существуют также async range методы.

Сохранить текущую интенсивность сети при миграции. Новый диапазон дает 25 элементов на страницу, август проверен как 25+6; читать пагинацию, проверять дату, dedup, partial failure. Не считать отсутствующие во временно неполном ответе даты unavailable. Year cache-first, stale requestVersion guards сохранить.

Результат NASA-07: added `ApodScienceSource.Range.cs`, sync/async GetEntries за существующим staged IApodClient. URL содержит date_from/date_to/page; X-WP-Total и X-WP-TotalPages обязательны, ограничены и должны оставаться неизменными между страницами. Общий сетевой бюджет 8 секунд на весь диапазон, страницы последовательно, прежние ограничения body/concurrency/Retry-After сохранены. Допустимый диапазон 1..366 дат, максимум 32 страницы; существующий календарь запрашивает месяц. Это не включает автоматическое сканирование года.

Каждая запись проходит существующий exact-date/media parser; дата обязана попадать в диапазон. Одинаковые дубликаты сворачиваются, конфликтующие отклоняются; итог отсортирован по дате, число уникальных записей должно совпасть с Total. Все страницы накапливаются локально и возвращаются только целиком. HTTP/schema/pagination failure не возвращает частичный успех; range 404 не классифицирует отдельную дату как unavailable. Пустой подтвержденный диапазон возвращает пустой список, без синтетических записей unavailable. Legacy API больше не вызывается range-методами staged adapter; остается только key validation. Default production adapter не переключен.

TDD: новый range test сначала упал на NotImplementedException, затем прошел. Дополнительно проверены ошибка второй страницы, wrong date, missing headers, null/не-array JSON, dedup, нехватка unique entries, изменение Total, empty range, timeout и отсутствие legacy delegation. Финальный Release build: 0 warnings / 0 errors, smoke tests passed.

Live: NASA range август 2026 действительно вернул 25 записей первой страницы, Total=31 и TotalPages=2. Но compiled range client отклонил август: у 2026-08-05 media_type=image при пустом primary center (в нем дата, но нет img/video). Отдельная проверка каждой записи подтвердила дефект этой даты, не transport. Никакие hdurl/og:image вместо отсутствующего primary image не подставлялись. Сентябрь 2026-09-01..28 успешно прошел compiled sync client: 28 записей, 26 image и 2 video, границы и сортировка корректны. Image bytes не загружались.

Обязательное перед NASA-04c/08: решить поведение month warmup при единичной поврежденной публикации. Сейчас намеренно fail-closed весь диапазон; старый кеш не заменяется частичным результатом, но исправные дни этого месяца не обновляются. Можно сохранять отдельно проверенные записи только с явным partial-result контрактом, не выдавая это за полный месяц. Не ослаблять parser и не выбирать случайную картинку из metadata. Installer/version/push не выполнялись.

### NASA-07a: подтвержденный кадр из metadata (Done)

Уточнение к предыдущему расследованию: публикация 2026-08-05 не лишена изображения. Primary HTML center пуст, но JSON hdurl и HTML og:image согласованно указывают на saturn_spokes_frame.jpg в каталоге NASA за август 2026. Поэтому прежняя классификация этой записи как поврежденной была слишком строгой.

Добавлен ограниченный fallback только для media_type=image без primary img: hdurl должен совпадать с og:image, принадлежать HTTPS assets.science.nasa.gov и каталогу года/месяца публикации. Embedded video, placeholder, другой host, несовпадение ссылок и неверный каталог отклоняются. Primary image сохраняет приоритет; video poster не становится обоями. Сохраняются исходные ссылки и IsFallbackImage. Это поддержка подтвержденного статического кадра NASA, не новый декодер произвольных GIF.

Smoke fixture metadata-frame.json синтетический минимизированный пример структуры ответа 260805. Тест сначала воспроизвел отказ прежнего parser, затем проверил fallback, отрицательные случаи и apply через существующий service с fake wallpaper applier. Реальные обои не менялись. Focused smoke build прошел без warnings/errors.

Live: static original вернул HTTP 200, image/jpeg, 63 659 bytes, 1133x716; существующий DownloadedImageFile сохранил файл, LocalImageValidator подтвердил его корректность. Compiled range client теперь успешно возвращает все 31 публикацию августа, включая этот кадр. Общий fail-closed контракт для действительно невалидных записей пока сохранен; это не partial-result реализация. Production switch остается NASA-04c, installer/version/push не выполнялись.

Итог NASA-07a: `dotnet build apod_wallpaper.sln -c Release` успешно, 0 warnings / 0 errors, все smoke tests passed.

## NASA-08: регрессия

Первый этап: тестами воспроизведены два интеграционных дефекта. Новый источник `nasa_science` теперь отображается через существующий тип Api, а не Unknown (sync/async preview). Подтвержденная текстовая публикация `other` с валидным canonical Science URL не запрашивается повторно только из-за возраста кеша. Календарь сохраняет известное отсутствие изображения. Force refresh продолжает обращаться к источнику; старые неподтвержденные записи `other` сохраняют обычное обновление. Тесты используют fixtures, временный каталог и fake client, без сети и установки обоев.

Проверка первого этапа: Release solution build успешен, 0 warnings / 0 errors, все smoke tests passed. Оба дефекта предварительно воспроизведены красными регрессионными тестами. Изменены только ApodWallpaperService, smoke tests и этот журнал.

Второй этап: для workflow ошибок добавлены стабильные RU/EN сообщения: 429, HTTP failure, timeout, invalid response и generic failure. Отсутствующая публикация в sync/async workflow остается Unavailable, но больше не выводит технический текст исключения. Добавлены отсутствовавшие переводы для будущей даты и публикации без изображения. Тест сначала воспроизвел отсутствие сообщения 429, затем проверил mapping, ключи перевода и оба 404 workflow на fake transport. Release solution build успешен: 0 warnings / 0 errors, smoke tests passed. Сетевые retries/timeouts, scheduler и UI layout не менялись.

Третий этап (2026-09-30): добавлен opt-in `scripts/test-science-source.ps1`, который проверяет собранный production ApodClient через native sync transport. Только metadata, без изменения settings/cache/images/wallpaper; последовательные запросы с паузой 1 секунду. Запуск: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-science-source.ps1 -CheckRange` (ExecutionPolicy действует только для этого процесса, системная политика не меняется). Требуется предварительный Release build.

Live результат: 12/12 дат успешно разобраны: 1995-06-16, 1995-06-20, 2000-01-01, 2003-06-01, 2012-03-12, 2014-10-01, 2015-01-01, 2026-08-05, 2026-08-31, 2026-09-01, 2026-09-27, 2026-09-28. Проверены точная дата, canonical URL, наличие preview/original для image и отсутствие изображения для video/other. Ранние GIF сохраняют оригинальную ссылку; 260805 использует согласованный metadata frame; 120312 = other; 260831 = video. Отдельный paginated range вернул 31/31 публикацию августа в правильном порядке. Одиночные metadata запросы заняли 760–1058 ms в этом запуске, это не измерение image download/UI performance. Байты фото этим скриптом не проверяются.

Проверка ошибок на публичном controller выявила пять отсутствующих RU ключей: load, download, apply, apply latest, NASA URL. Добавлены переводы и behavioral smoke на HTTP 503. Внутренние технические детали не выводятся в Error.Message. Общая Release сборка скомпилировала Core (оба target) и WinUI без warnings, но завершилась ошибкой нового теста счетчика запросов. Причина установлена: старый ApplyLatest делает три metadata lookup при 503. Тест локализации теперь явно фиксирует это существующее поведение; повторный focused SmokeTests build прошел, 0 warnings/errors, все smoke passed. Полная solution сборка повторно в этом этапе не запускалась по правилу репозитория.

**NASA-08a (Done): повторные latest lookup при ошибке.** Исходная цепочка: workflow.GetLatestPublishedDateAsync -> service.GetLatestPublishedDateAsync внутри GetLatestAvailableImageEntryAsync -> GetEntryByDateAsync(today). Оба определения latest проглатывали ошибку и подставляли UTC today. Один ApplyLatest при 503 делал 3 запроса; тест пяти facade операций видел 7 запросов вместо 5.

Исправление: workflow получает подтвержденную latest publication через GetLatestPublishedEntry(forceRefresh) и передает ту же запись во внутренний apply service. Прямой service вызов без переданной записи сам делает строгий lookup. Если latest содержит изображение, используется эта запись без повторного GET; если video/other, поиск начинается со вчера относительно даты публикации (существующая граница 7 дней сохранена). Ошибки не подменяются сегодняшней датой внутри apply. Методы календарного fallback, код scheduler/day-lock, favorites rotation и публичные facade интерфейсы не менялись.

Проверка NASA-08a: сначала два regression tests воспроизвели лишние запросы. Затем sync/async, force on/off, HTTP 503/429/302, timeout подтвердили отсутствие повторного поиска; 404 сохраняет максимум 4 кандидата latest и Unavailable. Прямой service тоже не перезапускает поиск. Подтверждены отсутствие apply/cache mutation при ошибке, сохранение календарного UTC-today fallback, sync/async переход через два video дня к локальному изображению, одна загрузка latest metadata на успешный forced apply, использование свежего session cache офлайн без сети и принудительный refresh с сохранением старого кеша при неудаче. Facade test теперь ожидает 5 запросов, не 7. Existing scheduler/day-lock smoke прошли; это не ручной тест работающего scheduler. Полный Release solution build успешен: 0 warnings / 0 errors, все smoke tests passed. Реальные обои не менялись.

Осталось в NASA-08: завершить матрицу сквозных проверок, прежде всего favorite-with-download и ручное выключение auto при apply. UI/tray/midnight/быстрая навигация и визуальное качество не подтверждены этими этапами. NASA-09 остается ручной проверкой перед выпуском. Предварительная общая готовность около 90–95%, NASA-10 не входит в обязательный объем миграции.

Проверить все потребители: download, apply (manual выключает auto), favorite-with-download, progress, latest scheduler, favorites rotation, calendar month/year, NASA, translation, About/Settings. Parser не должен сделать video poster обоями. Проверить старый cache, offline, slow network, midnight, tray restore и быструю навигацию. Новые пользовательские строки RU/EN. Build + offline smoke + отдельные live/ручные проверки; не объявлять всю миграцию готовой только по build.

## NASA-09: выпуск

Сначала ручная проверка пользователя. Затем отдельное разрешение на version/installer/tag/push. До этого каждый task commit локальный. Не запускать setup автоматически.

## NASA-10: быстрый месяц (после смены источника)

Предпочесть один диапазон с пагинацией и асинхронным обновлением кеша, а не 31 одновременный GET. Фактическое время измерять; два последовательных ответа и лимиты NASA не гарантируют те же 2 секунды, что один день. Только metadata, без image bytes. Ограничить concurrency, поддержать cancellation и отсутствие мигания, не включать сетевое сканирование целого года. Отдельно согласовать включение warmup для no-key пользователей.
