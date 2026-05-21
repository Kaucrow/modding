namespace MouseFriends.AI
{
    internal class MouseFriendAbstractAI : AbstractCreatureAI
    {
        internal bool IsTamed { get; set; } = false;
        internal WorldCoordinate? ToldToStay { get; set; } = null;

        internal MouseFriendAbstractAI(World world, AbstractCreature parent) : base(world, parent) {}
    }
}
