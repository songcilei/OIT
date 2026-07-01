using System.Collections.Generic;

namespace CapsuleAOTool
{
    internal static class CapsuleAORegistry
    {
        internal static readonly List<CapsuleAO> ActiveCapsules = new List<CapsuleAO>();

        internal static void Register(CapsuleAO capsule)
        {
            if (capsule != null && !ActiveCapsules.Contains(capsule))
            {
                ActiveCapsules.Add(capsule);
            }
        }

        internal static void Unregister(CapsuleAO capsule)
        {
            ActiveCapsules.Remove(capsule);
        }
    }
}
