using MicePupsMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MicePups.AI;

namespace MicePups.Data
{
    internal class PupData
    {
        public PupData(AbstractCreature creature)
        {
            this.creature = creature;
        }

        public AbstractCreature creature;
        public PhysicalObject grabTarget;

        public MousePupAbstractAI abstractAI
        {
            get
            {
                return this.creature.abstractAI as MousePupAbstractAI;
            }
        }
    }
}
