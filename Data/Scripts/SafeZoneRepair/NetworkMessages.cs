using ProtoBuf;
using VRageMath; // для Vector3I

namespace SafeZoneRepair
{
    [ProtoContract]
    public class RepairNotificationMessage
    {
        [ProtoMember(1)] public long BlockEntityId;
        [ProtoMember(2)] public bool RepairsComplete;
        [ProtoMember(3)] public float Cost;
        [ProtoMember(4)] public Vector3I Position;
        [ProtoMember(5)] public string BlockName;
        [ProtoMember(6)] public long PlayerId;
    }

    [ProtoContract]
    public class TerminalActionMessage
    {
        [ProtoMember(1)] public long GridEntityId;
        [ProtoMember(2)] public string Action; // "Toggle" или "Status"
    }

    [ProtoContract]
    public class TerminalStatusMessage
    {
        [ProtoMember(1)] public string Text;
        [ProtoMember(2)] public string Color;
        [ProtoMember(3)] public int Time; // миллисекунды
        [ProtoMember(4)] public long PlayerId;
    }


    [ProtoContract]
    public class RepairUiStateMessage
    {
        [ProtoMember(1)] public long PlayerId;
        [ProtoMember(2)] public bool InRepairZone;
        [ProtoMember(3)] public string ZoneName;
        [ProtoMember(4)] public bool RepairEnabled;
        [ProtoMember(5)] public string StatusText;
        [ProtoMember(6)] public string LastRepairText;
        [ProtoMember(7)] public long LastEventUtcTicks;
    }

}
