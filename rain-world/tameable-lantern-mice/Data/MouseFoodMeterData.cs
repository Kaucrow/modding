using System;
namespace MouseFriends.Data
{
    internal class MouseFoodMeterData
    {
        public MouseFoodMeterData(LanternMouse mouse, Player dummyPlayer, int stackIndex)
        {
            Mouse = mouse;
            DummyPlayer = dummyPlayer;
            StackIndex = stackIndex;
        }

        public LanternMouse Mouse { get; }
        public Player DummyPlayer { get; }
        public int StackIndex { get; }
        public float DeathFade { get; set; } = 0f;
    }
}
