using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace CleanSweep.Core.Localization;

public static class I18n
{
    public const string DefaultLanguage = "en";
    public static readonly string[] Languages = { "en", "ru", "uk" };

    public static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["ru"] = "Русский",
        ["uk"] = "Українська"
    };

    public static string NormalizeLanguageCode(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return DefaultLanguage;
        }

        string normalized = lang.Trim();
        int dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
        {
            normalized = normalized.Substring(0, dashIndex);
        }

        normalized = normalized.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return DefaultLanguage;
        }

        normalized = normalized.ToLowerInvariant();
        return Translations.ContainsKey(normalized) ? normalized : DefaultLanguage;
    }

    private static readonly Dictionary<string, (string one, string few, string many)> CategoryWordForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = ("category", "categories", "categories"),
        ["ru"] = ("категория", "категории", "категорий"),
        ["uk"] = ("категорія", "категорії", "категорій")
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["window_title"] = "CleanSweep",
            ["header_subtitle"] = "Fast PC cleanup — no background process and no subscription",
            ["scan_button"] = "Find junk",
            ["scan_button_again"] = "Scan again",
            ["scanning"] = "Scanning…",
            ["clean_button"] = "Clean selected",
            ["cleaning"] = "Cleaning…",
            ["ready_center"] = "Ready",
            ["ready_sub"] = "Press “Find junk”",
            ["junk_found_sub"] = "junk found",
            ["no_junk_center"] = "No junk found",
            ["no_junk_sub"] = "Disk is already clean",
            ["freed_sub"] = "freed",
            ["disk_free"] = "Free space: {free} of {total}",
            ["nothing_selected"] = "Nothing selected",
            ["selected_prefix"] = "Selected",
            ["hint_permanent"] = "Sensitive categories are unchecked by default. Review your selections before cleaning.",
            ["confirm_title"] = "Confirm deletion",
            ["confirm_text"] = "About {size} will be freed.\nFiles are deleted permanently. Continue?",
            ["confirm_details"] = "Selected categories:\n{categories}",
            ["preview_title"] = "Cleanup preview",
            ["preview_summary"] = "Previewing {count} items. About {size} is ready to be cleaned.",
            ["preview_summary_limited"] = "Showing {shown} of {totalCount} selected items. About {size} is ready to be cleaned.",
            ["preview_empty"] = "No user-visible files were found in this selection.",
            ["preview_continue"] = "Continue",
            ["preview_cancel"] = "Cancel",
            ["preview_folder"] = "Folder",
            ["preview_risk_green"] = "Trash-like files",
            ["preview_risk_yellow"] = "Can be important",
            ["preview_risk_red"] = "Important",
            ["preview_importance_system"] = "System-managed — deleting may break Windows or apps",
            ["preview_importance_user"] = "Likely personal/user data",
            ["preview_importance_temporary"] = "Likely temporary/cache data",
            ["preview_importance_unknown"] = "Unknown — review carefully",
            ["preview_importance_empty_folder"] = "Empty folder — contains no files",
            ["preview_importance_note"] = "Importance is estimated from file location and type; contents are not inspected. Unknown does not mean safe.",
            ["preview_recent_warning"] = "recently modified — deletion may affect active work",
            ["message_ok"] = "OK",
            ["confirm_yes"] = "Yes",
            ["confirm_no"] = "No",
            ["done_title"] = "Done",
            ["done_text"] = "Freed {size}.",
            ["skipped_intro"] = "\n\nSome files were skipped (in use by another program):\n",
            ["clean_label"] = "Cleaned",
            ["empty_label"] = "Clean",
            ["access_denied"] = "Access denied",
            ["access_denied_named"] = "{detail}: access denied",
            ["recycle_bin_failed"] = "Could not empty the Recycle Bin",
            ["settings_title"] = "Settings",
            ["settings_language"] = "Language",
            ["settings_disk"] = "Disk",
            ["settings_disk_hint"] = "Affects the Recycle Bin category and the free space shown above",
            ["settings_close"] = "Close",
            ["disk_stats_title"] = "Disk usage",
            ["disk_stats_capacity"] = "CAPACITY",
            ["disk_stats_used"] = "USED",
            ["disk_stats_free"] = "FREE",
            ["disk_stats_folders"] = "Largest folders",
            ["disk_stats_largest_files"] = "Largest files",
            ["disk_stats_name"] = "Folder",
            ["disk_stats_size"] = "Size",
            ["disk_stats_files"] = "Files",
            ["disk_stats_file"] = "File",
            ["disk_stats_cancel"] = "Cancel scan",
            ["disk_stats_cancelling"] = "Cancelling…",
            ["disk_stats_scanning"] = "Scanned {count} items · {path}",
            ["disk_stats_cancelled"] = "Scan cancelled.",
            ["disk_stats_error"] = "Could not scan this drive: {error}",
            ["disk_stats_summary"] = "Scanned {count} files totaling {size}. Scanned file size is separate from drive usage.",
            ["disk_stats_partial"] = "{count} paths could not be read.",
            ["disk_stats_root_files"] = "Files in drive root",
            ["disk_stats_up"] = "Up",
            ["disk_stats_search_hint"] = "Search file names in this folder",
            ["disk_stats_search_top_hint"] = "Search within the 10 largest files",
            ["disk_stats_browsing"] = "Loading {path}…",
            ["disk_stats_browsing_progress"] = "Checked {count} items · {path}",
            ["disk_stats_browse_complete"] = "{count} files and {folderCount} folders in this folder.",
            ["disk_stats_browse_limited"] = "Showing the {shown} largest files of {count} files in this folder.",
            ["cleanup_more_errors_omitted"] = "Additional errors not shown: {count}.",
            ["disk_stats_files_in_folder"] = "Files in {folder}",
            ["disk_stats_top_files_hint"] = "Showing the 10 largest files on this drive. Select a folder to browse its contents.",
            ["disk_stats_select_folder_hint"] = "Select this folder and its contents for review and deletion.",
            ["disk_stats_folder_list_hint"] = "Double-click a folder to open it. Check a folder to review and delete its contents.",
            ["disk_stats_preparing_folder_delete"] = "Preparing the selected files and folders for review…",
            ["disk_stats_folders_deleted"] = "Deleted {count} folders.",
            ["folder_cleanup_failed"] = "Could not delete folder {detail}; it may contain protected or inaccessible items.",
            ["disk_stats_delete_failure"] = "The selected files or folders could not be deleted: {error}",
            ["disk_stats_complete"] = "Scan complete.",
            ["disk_stats_delete_selected"] = "Review & delete",
            ["disk_stats_delete_hint"] = "Review selected files and folders before deletion. Recently modified files are allowed after confirmation.",
            ["disk_stats_deleting"] = "Deleting selected files and folders…",
            ["disk_stats_no_selected_files"] = "The selected files are no longer available.",
            ["disk_stats_delete_done"] = "Deleted {count} files and freed {size}.",
            ["disk_stats_delete_skipped"] = "{count} files were skipped because they are recent, linked, or no longer available.",
            ["disk_stats_delete_errors"] = "Could not delete {count} selected items.",
            ["file_cleanup_failed"] = "Could not delete {detail}",
            ["preview_recent_skipped"] = "recent — protected from deletion",
            ["startup_button_tooltip"] = "Startup programs",
            ["startup_title"] = "Startup Programs",
            ["startup_hint"] = "Turning something off here is fully reversible — nothing is deleted, you can turn it back on anytime.",
            ["startup_empty"] = "No startup programs found",
            ["startup_toggle_failed"] = "Couldn't change this item. It may need administrator rights.",
            ["startup_admin_badge"] = "requires administrator",
            ["startup_source_hkcu_run"] = "Registry (this user)",
            ["startup_source_hklm_run"] = "Registry (all users)",
            ["startup_source_user_startup"] = "Startup folder (this user)",
            ["startup_source_common_startup"] = "Startup folder (all users)",
            ["cat_temp_name"] = "Temporary files",
            ["cat_temp_desc"] = "Files created by Windows and apps while running, and no longer needed",
            ["cat_browser_cache_name"] = "Browser cache",
            ["cat_browser_cache_desc"] = "Chrome, Edge and Firefox — images and scripts saved to speed up page loads",
            ["cat_thumbnails_name"] = "Thumbnail cache",
            ["cat_thumbnails_desc"] = "Image and video previews that Windows Explorer regenerates automatically",
            ["cat_crash_reports_name"] = "Crash reports",
            ["cat_crash_reports_desc"] = "Crash dumps Windows keeps in case they get sent to Microsoft",
            ["cat_memory_dumps_name"] = "Memory dump files",
            ["cat_memory_dumps_desc"] = "Large files Windows saves after a crash — useful for diagnosing it, otherwise dead weight",
            ["cat_recent_items_name"] = "Recent items",
            ["cat_recent_items_desc"] = "Your recent-files and jump-list history — Windows rebuilds this as you go",
            ["cat_incomplete_downloads_name"] = "Incomplete downloads",
            ["cat_incomplete_downloads_desc"] = "Leftover .crdownload / .part files from downloads that never finished",
            ["cat_shader_cache_name"] = "Shader cache",
            ["cat_shader_cache_desc"] = "Compiled graphics shaders your GPU driver recreates automatically as needed",
            ["cat_recycle_bin_name"] = "Recycle Bin",
            ["cat_recycle_bin_desc"] = "Deleted files that still take up space on disk",
            ["cat_cache_name"] = "App cache",
            ["cat_cache_desc"] = "Contents of {path}"
        },
        ["ru"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["window_title"] = "CleanSweep",
            ["header_subtitle"] = "Быстрая очистка ПК — без фонового процесса и без подписки",
            ["scan_button"] = "Найти мусор",
            ["scan_button_again"] = "Сканировать снова",
            ["scanning"] = "Сканирование…",
            ["clean_button"] = "Очистить выбранное",
            ["cleaning"] = "Очистка…",
            ["ready_center"] = "Готово",
            ["ready_sub"] = "Нажмите «Найти мусор»",
            ["junk_found_sub"] = "найдено мусора",
            ["no_junk_center"] = "Мусор не найден",
            ["no_junk_sub"] = "Диск уже чист",
            ["freed_sub"] = "освобождено",
            ["disk_free"] = "Свободно на диске: {free} из {total}",
            ["nothing_selected"] = "Ничего не выбрано",
            ["selected_prefix"] = "Выбрано",
            ["hint_permanent"] = "Чувствительные категории выключены по умолчанию. Проверьте выбор перед очисткой.",
            ["confirm_title"] = "Подтвердите удаление",
            ["confirm_text"] = "Будет освобождено примерно {size}.\nФайлы удаляются безвозвратно. Продолжить?",
            ["confirm_details"] = "Выбранные категории:\n{categories}",
            ["preview_title"] = "Предпросмотр очистки",
            ["preview_summary"] = "Показано {count} элементов. Около {size} будет очищено.",
            ["preview_summary_limited"] = "Показаны {shown} из {totalCount} выбранных элементов. Будет очищено около {size}.",
            ["preview_empty"] = "В этой выборке не найдено файлов, которые можно безопасно удалить.",
            ["preview_continue"] = "Продолжить",
            ["preview_cancel"] = "Отмена",
            ["preview_folder"] = "Папка",
            ["preview_risk_green"] = "Мусорные файлы",
            ["preview_risk_yellow"] = "Может быть важно",
            ["preview_risk_red"] = "Важно",
            ["preview_importance_system"] = "Системный файл — удаление может повредить Windows или приложения",
            ["preview_importance_user"] = "Возможно, личные данные пользователя",
            ["preview_importance_temporary"] = "Вероятно, временные данные или кэш",
            ["preview_importance_unknown"] = "Неизвестно — проверьте внимательно",
            ["preview_importance_empty_folder"] = "Пустая папка — файлов нет",
            ["preview_importance_note"] = "Важность оценивается по расположению и типу файла; содержимое не проверяется. Неизвестный файл не считается безопасным.",
            ["preview_recent_warning"] = "недавно изменён — удаление может повлиять на текущую работу",
            ["message_ok"] = "ОК",
            ["confirm_yes"] = "Да",
            ["confirm_no"] = "Нет",
            ["done_title"] = "Готово",
            ["done_text"] = "Освобождено {size}.",
            ["skipped_intro"] = "\n\nНекоторые файлы пропущены (были заняты другой программой):\n",
            ["clean_label"] = "Очищено",
            ["empty_label"] = "Чисто",
            ["access_denied"] = "Нет доступа",
            ["access_denied_named"] = "{detail}: доступ запрещён",
            ["recycle_bin_failed"] = "Не удалось очистить корзину",
            ["settings_title"] = "Настройки",
            ["settings_language"] = "Язык",
            ["settings_disk"] = "Диск",
            ["settings_disk_hint"] = "Влияет на категорию «Корзина» и свободное место, показанное выше",
            ["settings_close"] = "Закрыть",
            ["disk_stats_title"] = "Использование диска",
            ["disk_stats_capacity"] = "ОБЪЁМ",
            ["disk_stats_used"] = "ЗАНЯТО",
            ["disk_stats_free"] = "СВОБОДНО",
            ["disk_stats_folders"] = "Самые большие папки",
            ["disk_stats_largest_files"] = "Самые большие файлы",
            ["disk_stats_name"] = "Папка",
            ["disk_stats_size"] = "Размер",
            ["disk_stats_files"] = "Файлы",
            ["disk_stats_file"] = "Файл",
            ["disk_stats_cancel"] = "Отменить сканирование",
            ["disk_stats_cancelling"] = "Отмена…",
            ["disk_stats_scanning"] = "Проверено элементов: {count} · {path}",
            ["disk_stats_cancelled"] = "Сканирование отменено.",
            ["disk_stats_error"] = "Не удалось просканировать диск: {error}",
            ["disk_stats_summary"] = "Просканировано файлов: {count}, общий размер: {size}. Размер найденных файлов не равен занятому месту на диске.",
            ["disk_stats_partial"] = "Не удалось прочитать путей: {count}.",
            ["disk_stats_root_files"] = "Файлы в корне диска",
            ["disk_stats_up"] = "Вверх",
            ["disk_stats_search_hint"] = "Поиск файлов по имени в этой папке",
            ["disk_stats_search_top_hint"] = "Поиск среди 10 самых больших файлов",
            ["disk_stats_browsing"] = "Загрузка {path}…",
            ["disk_stats_browsing_progress"] = "Проверено элементов: {count} · {path}",
            ["disk_stats_browse_complete"] = "В папке: файлов — {count}, подпапок — {folderCount}.",
            ["disk_stats_browse_limited"] = "Показаны {shown} самых больших файлов из {count}.",
            ["cleanup_more_errors_omitted"] = "Остальные ошибки не показаны: {count}.",
            ["disk_stats_files_in_folder"] = "Файлы в папке {folder}",
            ["disk_stats_top_files_hint"] = "Показаны 10 самых больших файлов на диске. Выберите папку, чтобы просмотреть её содержимое.",
            ["disk_stats_select_folder_hint"] = "Выберите папку и её содержимое для проверки и удаления.",
            ["disk_stats_folder_list_hint"] = "Дважды щёлкните папку, чтобы открыть. Отметьте её флажком для проверки и удаления содержимого.",
            ["disk_stats_preparing_folder_delete"] = "Подготовка выбранных файлов и папок к проверке…",
            ["disk_stats_folders_deleted"] = "Удалено папок: {count}.",
            ["folder_cleanup_failed"] = "Не удалось удалить папку {detail}; возможно, в ней есть защищённые или недоступные элементы.",
            ["disk_stats_delete_failure"] = "Не удалось удалить выбранные файлы или папки: {error}",
            ["disk_stats_complete"] = "Сканирование завершено.",
            ["disk_stats_delete_selected"] = "Проверить и удалить",
            ["disk_stats_delete_hint"] = "Перед удалением проверьте выбранные файлы и папки. Недавно изменённые файлы удаляются после подтверждения.",
            ["disk_stats_deleting"] = "Удаление выбранных файлов и папок…",
            ["disk_stats_no_selected_files"] = "Выбранные файлы больше недоступны.",
            ["disk_stats_delete_done"] = "Удалено файлов: {count}, освобождено {size}.",
            ["disk_stats_delete_skipped"] = "Пропущено файлов: {count} (недавно изменены, являются ссылками или недоступны).",
            ["disk_stats_delete_errors"] = "Не удалось удалить элементов: {count}.",
            ["file_cleanup_failed"] = "Не удалось удалить {detail}",
            ["preview_recent_skipped"] = "недавно изменён — защищён от удаления",
            ["startup_button_tooltip"] = "Автозагрузка",
            ["startup_title"] = "Автозагрузка",
            ["startup_hint"] = "Отключение здесь полностью обратимо — ничего не удаляется, включить обратно можно в любой момент.",
            ["startup_empty"] = "Программ автозагрузки не найдено",
            ["startup_toggle_failed"] = "Не удалось изменить этот пункт. Возможно, нужны права администратора.",
            ["startup_admin_badge"] = "требует прав администратора",
            ["startup_source_hkcu_run"] = "Реестр (этот пользователь)",
            ["startup_source_hklm_run"] = "Реестр (все пользователи)",
            ["startup_source_user_startup"] = "Папка автозагрузки (этот пользователь)",
            ["startup_source_common_startup"] = "Папка автозагрузки (все пользователи)",
            ["cat_temp_name"] = "Временные файлы",
            ["cat_temp_desc"] = "Файлы, которые Windows и программы создают во время работы и больше не используют",
            ["cat_browser_cache_name"] = "Кэш браузеров",
            ["cat_browser_cache_desc"] = "Chrome, Edge и Firefox — картинки и скрипты сайтов, сохранённые для ускорения загрузки",
            ["cat_thumbnails_name"] = "Кэш миниатюр",
            ["cat_thumbnails_desc"] = "Превью картинок и видео — Проводник Windows пересоздаёт их автоматически",
            ["cat_crash_reports_name"] = "Отчёты об ошибках",
            ["cat_crash_reports_desc"] = "Дампы сбоев программ, которые Windows хранит на случай отправки в Microsoft",
            ["cat_memory_dumps_name"] = "Дампы памяти",
            ["cat_memory_dumps_desc"] = "Крупные файлы, которые Windows сохраняет после сбоя — полезны для диагностики, в остальном бесполезны",
            ["cat_recent_items_name"] = "Недавние файлы",
            ["cat_recent_items_desc"] = "История недавних файлов и списков переходов — Windows пересоздаёт её по ходу работы",
            ["cat_incomplete_downloads_name"] = "Недокачанные загрузки",
            ["cat_incomplete_downloads_desc"] = "Файлы .crdownload / .part от загрузок, которые так и не завершились",
            ["cat_shader_cache_name"] = "Кэш шейдеров",
            ["cat_shader_cache_desc"] = "Скомпилированные шейдеры видеокарты — драйвер пересоздаёт их сам по мере необходимости",
            ["cat_recycle_bin_name"] = "Корзина",
            ["cat_recycle_bin_desc"] = "Удалённые файлы, которые всё ещё занимают место на диске",
            ["cat_cache_name"] = "Кэш приложений",
            ["cat_cache_desc"] = "Содержимое {path}"
        },
        ["uk"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["window_title"] = "CleanSweep",
            ["header_subtitle"] = "Швидке очищення ПК — без фонових процесів і без підписки",
            ["scan_button"] = "Знайти сміття",
            ["scan_button_again"] = "Сканувати знову",
            ["scanning"] = "Сканування…",
            ["clean_button"] = "Очистити вибране",
            ["cleaning"] = "Очищення…",
            ["ready_center"] = "Готово",
            ["ready_sub"] = "Натисніть «Знайти сміття»",
            ["junk_found_sub"] = "знайдено сміття",
            ["no_junk_center"] = "Сміття не знайдено",
            ["no_junk_sub"] = "Диск вже чистий",
            ["freed_sub"] = "звільнено",
            ["disk_free"] = "Вільно на диску: {free} з {total}",
            ["nothing_selected"] = "Нічого не обрано",
            ["selected_prefix"] = "Обрано",
            ["hint_permanent"] = "Чутливі категорії вимкнені за замовчуванням. Перевірте вибір перед очищенням.",
            ["confirm_title"] = "Підтвердьте видалення",
            ["confirm_text"] = "Буде звільнено приблизно {size}.\nФайли видаляються назавжди. Продовжити?",
            ["confirm_details"] = "Вибрані категорії:\n{categories}",
            ["preview_title"] = "Попередній перегляд очищення",
            ["preview_summary"] = "Показано {count} елементів. Близько {size} буде очищено.",
            ["preview_summary_limited"] = "Показано {shown} із {totalCount} вибраних елементів. Буде очищено близько {size}.",
            ["preview_empty"] = "У цьому виборі не знайдено файлів, які можна безпечно видалити.",
            ["preview_continue"] = "Продовжити",
            ["preview_cancel"] = "Скасувати",
            ["preview_folder"] = "Папка",
            ["preview_risk_green"] = "Сміттєві файли",
            ["preview_risk_yellow"] = "Може бути важливо",
            ["preview_risk_red"] = "Важливо",
            ["preview_importance_system"] = "Системний файл — видалення може пошкодити Windows або програми",
            ["preview_importance_user"] = "Можливо, особисті дані користувача",
            ["preview_importance_temporary"] = "Ймовірно, тимчасові дані або кеш",
            ["preview_importance_unknown"] = "Невідомо — перевірте уважно",
            ["preview_importance_empty_folder"] = "Порожня папка — файлів немає",
            ["preview_importance_note"] = "Важливість оцінюється за розташуванням і типом файлу; вміст не перевіряється. Невідомий файл не вважається безпечним.",
            ["preview_recent_warning"] = "нещодавно змінений — видалення може вплинути на поточну роботу",
            ["message_ok"] = "Гаразд",
            ["confirm_yes"] = "Так",
            ["confirm_no"] = "Ні",
            ["done_title"] = "Готово",
            ["done_text"] = "Звільнено {size}.",
            ["skipped_intro"] = "\n\nДеякі файли пропущено (були зайняті іншою програмою):\n",
            ["clean_label"] = "Очищено",
            ["empty_label"] = "Чисто",
            ["access_denied"] = "Немає доступу",
            ["access_denied_named"] = "{detail}: немає доступу",
            ["recycle_bin_failed"] = "Не вдалося очистити кошик",
            ["settings_title"] = "Налаштування",
            ["settings_language"] = "Мова",
            ["settings_disk"] = "Диск",
            ["settings_disk_hint"] = "Впливає на категорію «Кошик» і вільне місце, показане вище",
            ["settings_close"] = "Закрити",
            ["disk_stats_title"] = "Використання диска",
            ["disk_stats_capacity"] = "ЄМНІСТЬ",
            ["disk_stats_used"] = "ЗАЙНЯТО",
            ["disk_stats_free"] = "ВІЛЬНО",
            ["disk_stats_folders"] = "Найбільші папки",
            ["disk_stats_largest_files"] = "Найбільші файли",
            ["disk_stats_name"] = "Папка",
            ["disk_stats_size"] = "Розмір",
            ["disk_stats_files"] = "Файли",
            ["disk_stats_file"] = "Файл",
            ["disk_stats_cancel"] = "Скасувати сканування",
            ["disk_stats_cancelling"] = "Скасування…",
            ["disk_stats_scanning"] = "Перевірено елементів: {count} · {path}",
            ["disk_stats_cancelled"] = "Сканування скасовано.",
            ["disk_stats_error"] = "Не вдалося просканувати диск: {error}",
            ["disk_stats_summary"] = "Проскановано файлів: {count}, загальний розмір: {size}. Розмір знайдених файлів відрізняється від зайнятого місця на диску.",
            ["disk_stats_partial"] = "Не вдалося прочитати шляхів: {count}.",
            ["disk_stats_root_files"] = "Файли в корені диска",
            ["disk_stats_up"] = "Вгору",
            ["disk_stats_search_hint"] = "Пошук файлів за назвою в цій папці",
            ["disk_stats_search_top_hint"] = "Пошук серед 10 найбільших файлів",
            ["disk_stats_browsing"] = "Завантаження {path}…",
            ["disk_stats_browsing_progress"] = "Перевірено елементів: {count} · {path}",
            ["disk_stats_browse_complete"] = "У папці: файлів — {count}, підпапок — {folderCount}.",
            ["disk_stats_browse_limited"] = "Показано {shown} найбільших файлів із {count}.",
            ["cleanup_more_errors_omitted"] = "Інші помилки не показано: {count}.",
            ["disk_stats_files_in_folder"] = "Файли в папці {folder}",
            ["disk_stats_top_files_hint"] = "Показано 10 найбільших файлів на диску. Виберіть папку, щоб переглянути її вміст.",
            ["disk_stats_select_folder_hint"] = "Виберіть папку та її вміст для перевірки й видалення.",
            ["disk_stats_folder_list_hint"] = "Двічі клацніть папку, щоб відкрити. Позначте її прапорцем для перевірки й видалення вмісту.",
            ["disk_stats_preparing_folder_delete"] = "Підготовка вибраних файлів і папок до перевірки…",
            ["disk_stats_folders_deleted"] = "Видалено папок: {count}.",
            ["folder_cleanup_failed"] = "Не вдалося видалити папку {detail}; можливо, у ній є захищені або недоступні елементи.",
            ["disk_stats_delete_failure"] = "Не вдалося видалити вибрані файли або папки: {error}",
            ["disk_stats_complete"] = "Сканування завершено.",
            ["disk_stats_delete_selected"] = "Переглянути й видалити",
            ["disk_stats_delete_hint"] = "Перевірте вибрані файли й папки перед видаленням. Нещодавно змінені файли видаляються після підтвердження.",
            ["disk_stats_deleting"] = "Видалення вибраних файлів і папок…",
            ["disk_stats_no_selected_files"] = "Вибрані файли більше недоступні.",
            ["disk_stats_delete_done"] = "Видалено файлів: {count}, звільнено {size}.",
            ["disk_stats_delete_skipped"] = "Пропущено файлів: {count} (нещодавно змінені, є посиланнями або недоступні).",
            ["disk_stats_delete_errors"] = "Не вдалося видалити елементів: {count}.",
            ["file_cleanup_failed"] = "Не вдалося видалити {detail}",
            ["preview_recent_skipped"] = "нещодавно змінений — захищений від видалення",
            ["startup_button_tooltip"] = "Автозавантаження",
            ["startup_title"] = "Автозавантаження",
            ["startup_hint"] = "Вимкнення тут повністю оборотне — нічого не видаляється, увімкнути назад можна будь-коли.",
            ["startup_empty"] = "Програм автозавантаження не знайдено",
            ["startup_toggle_failed"] = "Не вдалося змінити цей пункт. Можливо, потрібні права адміністратора.",
            ["startup_admin_badge"] = "потребує прав адміністратора",
            ["startup_source_hkcu_run"] = "Реєстр (цей користувач)",
            ["startup_source_hklm_run"] = "Реєстр (усі користувачі)",
            ["startup_source_user_startup"] = "Папка автозавантаження (цей користувач)",
            ["startup_source_common_startup"] = "Папка автозавантаження (усі користувачі)",
            ["cat_temp_name"] = "Тимчасові файли",
            ["cat_temp_desc"] = "Файли, які Windows і програми створюють під час роботи і більше не використовують",
            ["cat_browser_cache_name"] = "Кеш браузерів",
            ["cat_browser_cache_desc"] = "Chrome, Edge і Firefox — зображення та скрипти сайтів, збережені для прискорення завантаження",
            ["cat_thumbnails_name"] = "Кеш мініатюр",
            ["cat_thumbnails_desc"] = "Прев'ю зображень і відео — Провідник Windows створює їх автоматично",
            ["cat_crash_reports_name"] = "Звіти про помилки",
            ["cat_crash_reports_desc"] = "Дампи збоїв програм, які Windows зберігає на випадок надсилання до Microsoft",
            ["cat_memory_dumps_name"] = "Дампи пам'яті",
            ["cat_memory_dumps_desc"] = "Великі файли, які Windows зберігає після збою — корисні для діагностики, в іншому не потрібні",
            ["cat_recent_items_name"] = "Останні файли",
            ["cat_recent_items_desc"] = "Історія останніх файлів і списків переходів — Windows відтворює її по ходу роботи",
            ["cat_incomplete_downloads_name"] = "Незавершені завантаження",
            ["cat_incomplete_downloads_desc"] = "Файли .crdownload / .part від завантажень, які так і не завершились",
            ["cat_shader_cache_name"] = "Кеш шейдерів",
            ["cat_shader_cache_desc"] = "Скомпільовані шейдери відеокарти — драйвер створює їх сам за потреби",
            ["cat_recycle_bin_name"] = "Кошик",
            ["cat_recycle_bin_desc"] = "Видалені файли, які все ще займають місце на диску",
            ["cat_cache_name"] = "Кеш програм",
            ["cat_cache_desc"] = "Вміст {path}"
        }
    };

    private static string _currentLanguage = DefaultLanguage;

    static I18n()
    {
        _currentLanguage = LoadSavedLanguage();
    }

    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetLanguage(value);
    }

    public static string LoadSavedLanguage()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\CleanSweep");
            var val = key?.GetValue("Language")?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                return NormalizeLanguageCode(val);
            }
        }
        catch
        {
            // Ignore
        }
        return DefaultLanguage;
    }

    public static string LoadSavedDrive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\CleanSweep");
            var val = key?.GetValue("Drive")?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                return val;
            }
        }
        catch
        {
            // Ignore
        }
        return Utils.FileSystemUtils.SystemDrive();
    }

    public static void SaveDrive(string drive)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\CleanSweep");
            key?.SetValue("Drive", drive, RegistryValueKind.String);
        }
        catch
        {
            // Ignore
        }
    }

    public static string SetLanguage(string lang)
    {
        string normalized = NormalizeLanguageCode(lang);

        _currentLanguage = normalized;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\CleanSweep");
            key?.SetValue("Language", normalized, RegistryValueKind.String);
        }
        catch
        {
            // Ignore
        }

        return _currentLanguage;
    }

    public static void ResetLanguage()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\CleanSweep", true);
            key?.DeleteValue("Language", false);
        }
        catch
        {
            // Ignore
        }
        _currentLanguage = DefaultLanguage;
    }

    public static string Tr(string key, Dictionary<string, string>? args = null)
    {
        return Tr(_currentLanguage, key, args);
    }

    public static string Tr(string lang, string key, Dictionary<string, string>? args = null)
    {
        string? result = null;

        if (Translations.TryGetValue(lang, out var dict))
        {
            dict.TryGetValue(key, out result);
        }

        if (result == null && Translations.TryGetValue(DefaultLanguage, out var defaultDict))
        {
            defaultDict.TryGetValue(key, out result);
        }

        result ??= key;

        if (args != null)
        {
            foreach (var (placeholder, val) in args)
            {
                result = result.Replace("{" + placeholder + "}", val);
            }
        }

        return result;
    }

    public static string PluralCategoryWord(string lang, int count)
    {
        if (!CategoryWordForms.TryGetValue(lang, out var forms))
        {
            forms = CategoryWordForms[DefaultLanguage];
        }

        if (string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(lang, "uk", StringComparison.OrdinalIgnoreCase))
        {
            int n = Math.Abs(count) % 100;
            int n1 = n % 10;
            if (n is > 10 and < 20)
            {
                return forms.many;
            }
            if (n1 == 1)
            {
                return forms.one;
            }
            if (n1 is >= 2 and <= 4)
            {
                return forms.few;
            }
            return forms.many;
        }

        return count == 1 ? forms.one : forms.few;
    }
}
