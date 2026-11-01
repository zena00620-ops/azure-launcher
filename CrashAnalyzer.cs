namespace Luba.Core;

public static class CrashAnalyzer
{
    public static string Analyze(string text, int exitCode)
    {
        var t = text ?? "";
        var tips = new List<string>();
        void Add(string s) { if (!tips.Contains(s)) tips.Add(s); }
        bool Has(params string[] keys) => keys.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (Has("OutOfMemoryError", "GC overhead"))
            Add("• Не хватает памяти. Увеличь ОЗУ в «Настройках» (системе оставь минимум 1 ГБ) или убери часть модов.");
        if (Has("Could not reserve enough space", "Invalid maximum heap size", "Too small maximum heap"))
            Add("• ОЗУ выставлена больше, чем доступно. Уменьши её в «Настройках».");
        if (Has("UnsupportedClassVersionError", "compiled by a more recent version"))
            Add("• Не та версия Java. Очисти поле «Путь к Java» в настройках — лаунчер сам поставит нужную.");
        if (Has("Mod resolution failed", "Incompatible mod set", "Missing or unsupported mandatory dependencies",
                "Could not find required mod", "FormattedException", "ModResolutionException"))
            Add("• У модов не хватает зависимостей или они конфликтуют. Проверь, что все моды именно под эту версию Minecraft и под этот загрузчик (Fabric/Forge).");
        if (Has("MixinApplyError", "InvalidMixinException", "Mixin apply failed"))
            Add("• Какой-то мод конфликтует с другим (Mixin). Уберите недавно добавленный мод.");
        if (Has("NoSuchMethodError", "NoClassDefFoundError", "ClassNotFoundException", "AbstractMethodError"))
            Add("• Мод не подходит по версии (или нет его библиотеки/API). Обнови его или удали.");
        if (Has("GLFW error", "WGL", "Pixel format not accelerated", "OpenGL", "Failed to create window"))
            Add("• Проблема с видеокартой/драйвером (OpenGL). Обнови драйвер видеокарты. Если вылет после установки оптимизации (Sodium/Embeddium) — выключи галку «Лёгкая оптимизация» в сборке.");
        if (exitCode == -1073740791 || exitCode == -1073741819)
            Add("• Аварийное падение процесса (драйвер или повреждённый мод). Обнови драйвер видеокарты и попробуй без модов.");
        if (Has("Exception in server tick loop", "Failed to start the minecraft server"))
            Add("• Сломался встроенный сервер/мир. Попробуй создать новый мир или проверить моды.");

        if (tips.Count == 0)
            Add("• Точная причина не распознана. Смотри crash-report и лог ниже; если вылет после добавления модов — убери их по одному.");

        return string.Join("\n\n", tips);
    }
}
