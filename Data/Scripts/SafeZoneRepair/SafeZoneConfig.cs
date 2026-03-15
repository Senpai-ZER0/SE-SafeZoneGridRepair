using System.Collections.Generic;
using ProtoBuf;

namespace SafeZoneRepair
{
    [ProtoContract]
    public class ComponentPriceModifierEntry
    {
        [ProtoMember(1)]
        public string ComponentSubtypeId { get; set; }

        [ProtoMember(2)]
        public float Multiplier { get; set; } = 1f;
    }


    /// <summary>
    /// XML-обёртка для списка конфигов зон. Нужна потому, что SerializeToXML(List<T>)
    /// в окружении Space Engineers нестабилен как корневой тип.
    /// </summary>
    public class SafeZoneConfigCollection
    {
        public List<SafeZoneConfig> Zones { get; set; } = new List<SafeZoneConfig>();
    }

    /// <summary>
    /// Конфигурация отдельной безопасной зоны.
    /// </summary>
    [ProtoContract]
    public class SafeZoneConfig
    {
        [ProtoMember(1)] public string ZoneName { get; set; }
        [ProtoMember(2)] public string DisplayName { get; set; }
        [ProtoMember(3)] public long ZoneEntityId { get; set; }
        [ProtoMember(4)] public float WeldingSpeed { get; set; } = 1f;
        [ProtoMember(5)] public float CostModifier { get; set; } = 1f;
        [ProtoMember(6)] public bool Enabled { get; set; } = true;
        [ProtoMember(7)] public bool AllowProjections { get; set; } = true;
        [ProtoMember(8)] public float ProjectionBuildDelay { get; set; } = 1f;
        [ProtoMember(9)] public List<string> ForbiddenComponents { get; set; } = new List<string>();
        [ProtoMember(10)] public float ProjectionWeldingSpeed { get; set; } = 1f;
        [ProtoMember(11)] public bool DebugMode { get; set; } = false;
        [ProtoMember(12)] public string ZoneCreationType { get; set; } = "SafeZoneBlock";
        [ProtoMember(13)] public List<ComponentPriceModifierEntry> ComponentPriceModifiers { get; set; } = new List<ComponentPriceModifierEntry>();

        // --- Метаданные процедурного профиля ---
        [ProtoMember(14)] public string AssignedProfileId { get; set; }
        [ProtoMember(15)] public string AssignedVariantId { get; set; }
        [ProtoMember(16)] public string AssignmentSource { get; set; }
        [ProtoMember(17)] public int VariantSeed { get; set; } = 0;
        [ProtoMember(18)] public bool WasManuallyEdited { get; set; } = false;
        [ProtoMember(19)] public string ProfileSourceName { get; set; }
        [ProtoMember(20)] public string PlayerServiceName { get; set; }
        [ProtoMember(21)] public string PlayerRestrictionsSummary { get; set; }
        [ProtoMember(22)] public string PlayerDetailsDescription { get; set; }
    }
}
