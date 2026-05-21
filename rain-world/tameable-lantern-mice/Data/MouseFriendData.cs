using RWCustom;
using System.Collections.Generic;
using MouseFriends.AI;

namespace MouseFriends.Data
{
    internal class MouseFriendData
    {
        internal MouseFriendData(AbstractCreature creature)
        {
            this.Creature = creature;
        }

        internal AbstractCreature Creature { get; }
        internal WorldCoordinate Destination { get; set; }
        internal PhysicalObject GrabTarget { get; set; }
        internal int FoodInStomach { get; set; } = 0;
        internal int MaxFoodInStomach { get; set; } = 3;
        internal float FollowCloseness { get; set; }
        internal int ToldToPlay { get; set; } = 0;

        internal int ThrowAtTarget { get; set; } = 0;
        internal List<IntVector2> _CachedFloodFillList { get; set; } = new List<IntVector2>(50);
        internal List<IntVector2> PreviousAttackPositions { get; set; } = new List<IntVector2>();
        internal List<IntVector2> List { get; set; } = new List<IntVector2>(50);
        internal WorldCoordinate AttackPos { get; set; }
        internal WorldCoordinate TestThrowPos { get; set; }
        internal int ChangeAttackPositionDelay { get; set; } = 0;

        internal bool IsFull => FoodInStomach >= MaxFoodInStomach;

        internal MouseFriendAbstractAI abstractAI
        {
            get
            {
                return this.Creature.abstractAI as MouseFriendAbstractAI;
            }
        }
    }
}
