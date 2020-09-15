namespace mprCADmanager.Enums
{
    /// <summary>
    /// Вариант фильтрации по принадлежности виду
    /// </summary>
    public enum BelongingToViewVariant
    {
        /// <summary>
        /// Все
        /// </summary>
        All = 0,

        /// <summary>
        /// Неопределенные
        /// </summary>
        Unidentified = 1,

        /// <summary>
        /// Принадлежащие виду
        /// </summary>
        ViewSpecific = 2,

        /// <summary>
        /// Не принадлежащие виду
        /// </summary>
        ModelImports = 3
    }
}
