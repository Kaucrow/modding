namespace MouseFriends.AI
{
    internal partial class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents,
        IUseARelationshipTracker
    {
        public void GiftRecieved(SocialEventRecognizer.OwnedItemOnGround giftOfferedToMe)
        {
            if (giftOfferedToMe.owner is Player)
            {
                SocialMemory.Relationship orInitiateRelationship =
                    this.creature.realizedCreature.State.socialMemory
                    .GetOrInitiateRelationship(giftOfferedToMe.owner.abstractCreature.ID);

                orInitiateRelationship.InfluenceLike(1f);
                orInitiateRelationship.InfluenceTempLike(1f);
            }

            // DEBUG
            /*
            this.travelTo = giftOfferedToMe.item.abstractPhysicalObject.pos;
            */
        }
    }
}
