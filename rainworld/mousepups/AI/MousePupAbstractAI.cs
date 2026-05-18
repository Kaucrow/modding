using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicePups.AI
{
    public class MousePupAbstractAI : AbstractCreatureAI
    {
        public MousePupAbstractAI(World world, AbstractCreature parent) : base(world, parent)
        {
        }

        public bool isTamed;
    }
}
