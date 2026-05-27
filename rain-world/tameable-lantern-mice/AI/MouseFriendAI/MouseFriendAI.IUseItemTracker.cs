namespace MouseFriends.AI
{
    internal partial class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents,
        IUseARelationshipTracker
    {
        public bool TrackItem(AbstractPhysicalObject obj)
        {
            return obj.realizedObject == null || !(obj.realizedObject is Weapon) || (obj.realizedObject as Weapon).mode != Weapon.Mode.StuckInWall;
        }

        public void SeeThrownWeapon(PhysicalObject obj, Creature thrower)
        {
            if (base.tracker.RepresentationForObject(thrower, false) == null)
            {
                base.noiseTracker.mysteriousNoises += 20f;
                base.noiseTracker.mysteriousNoiseCounter = 200;
            }
        }
    }
}
