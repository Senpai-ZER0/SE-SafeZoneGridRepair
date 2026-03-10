namespace SafeZoneRepair
{
    /// <summary>
    /// Информация о ремонте конкретного блока.
    /// </summary>
    public class BlockRepairInfo
    {
        /// <summary>
        /// Оставшаяся стоимость ремонта (последний шаг).
        /// </summary>
        public long TotalCost;

        /// <summary>
        /// Полная стоимость ремонта блока (используется для уведомлений).
        /// </summary>
        public long InitialCost;

        /// <summary>
        /// IdentityId игрока, который пилотировал корабль в момент добавления блока.
        /// </summary>
        public long PilotIdentityId;
    }
}