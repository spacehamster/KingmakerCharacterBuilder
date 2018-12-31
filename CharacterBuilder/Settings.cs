using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace CharacterBuilder
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool DefaultPointBuy25 = true;
        public bool AutoSelectSkills = false;
        public bool DisableRemovePlanOnChange = false;
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}