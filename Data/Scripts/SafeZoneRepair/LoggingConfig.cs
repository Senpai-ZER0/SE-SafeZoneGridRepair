using ProtoBuf;

namespace SafeZoneRepair
{
    [ProtoContract]
    public class LoggingConfig
    {
        [ProtoMember(1)]
        public bool EnableLogGeneral { get; set; } = false;

        [ProtoMember(2)]
        public bool EnableLogZoneDetection { get; set; } = false;

        [ProtoMember(3)]
        public bool EnableLogBlockNeedsRepair { get; set; } = false;

        [ProtoMember(4)]
        public bool EnableLogRepairAction { get; set; } = false;

        [ProtoMember(5)]
        public bool EnableLogCostCalculation { get; set; } = false;

        [ProtoMember(6)]
        public bool EnableLogErrors { get; set; } = false;
    }
}