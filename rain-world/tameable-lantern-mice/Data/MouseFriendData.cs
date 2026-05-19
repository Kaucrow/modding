using MouseFriends.AI;

namespace MouseFriends.Data
{
    internal class MouseFriendData
    {
        public MouseFriendData(AbstractCreature creature)
        {
            this.Creature = creature;
        }

        public AbstractCreature Creature { get; }
        public bool IsTamed { get; set; }
        public PhysicalObject GrabTarget { get; set; }
        public PhysicalObject Grabbed { get; set;  }
        public int CurrentFood { get; }

        public MouseFriendAbstractAI abstractAI
        {
            get
            {
                return this.Creature.abstractAI as MouseFriendAbstractAI;
            }
        }
    }
}
