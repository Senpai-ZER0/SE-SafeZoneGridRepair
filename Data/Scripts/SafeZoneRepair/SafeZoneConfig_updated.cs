using System.Collections.Generic;
using ProtoBuf;

namespace SafeZoneRepair
{
    /// <summary>
    /// Конфигурация отдельной безопасной зоны.
    /// </summary>
    [ProtoContract]
    public class SafeZoneConfig
    {
        /// <summary>
        /// Имя зоны (для удобства администратора).
        /// </summary>
        [ProtoMember(1)]
        public string ZoneName { get; set; }

        /// <summary>
        /// Отображаемое имя зоны для UI.
        /// </summary>
        [ProtoMember(2)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Уникальный идентификатор зоны (EntityId). Используется для привязки конфига к конкретной зоне.
        /// </summary>
        [ProtoMember(9)]
        public long ZoneEntityId { get; set; }

        /// <summary>
        /// Скорость ремонта для этой зоны (единиц прочности в секунду).
        /// </summary>
        [ProtoMember(9)]
        public float WeldingSpeed { get; set; } = 1f;

        /// <summary>
        /// Множитель стоимости ремонта в этой зоне. Итоговая цена = (сумма компонентов) * CostModifier * 100.
        /// </summary>
        [ProtoMember(9)]
        public float CostModifier { get; set; } = 0.1f;

        /// <summary>
        /// Включён ли ремонт в этой зоне.
        /// </summary>
        [ProtoMember(9)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Разрешён ли ремонт проекций в этой зоне.
        /// </summary>
        [ProtoMember(9)]
        public bool AllowProjections { get; set; } = true;

        /// <summary>
        /// Задержка между постройкой блоков проекций (в секундах).
        /// </summary>
        [ProtoMember(9)]
        public float ProjectionBuildDelay { get; set; } = 1f;

        /// <summary>
        /// Список запрещённых компонентов (подтипы). Блоки, требующие хотя бы один такой компонент, не будут ремонтироваться.
        /// </summary>
        [ProtoMember(9)]
        public List<string> ForbiddenComponents { get; set; } = new List<string>();
    }
}