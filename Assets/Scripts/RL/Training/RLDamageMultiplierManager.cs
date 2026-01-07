using System;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Legacy shim kept for scenes that still reference the RL namespace.
    /// Inherits the real manager in Vampire namespace to avoid duplicate logic.
    /// </summary>
    [Obsolete("Use Vampire.RLDamageMultiplierManager instead")]
    public class RLDamageMultiplierManager : Vampire.RLDamageMultiplierManager { }
}
