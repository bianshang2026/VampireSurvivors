using UnityEngine;

namespace BiopunkRV
{
    /// <summary>Remembers only the contribution of a method, preserving unrelated upgrades.</summary>
    public class MethodBonusState : MonoBehaviour
    {
        private float attackContribution, speedContribution;

        public void Apply(PlayerStats stats, float damageBonus, float speedBonus)
        {
            stats.attack -= attackContribution;
            stats.attackSpeed -= speedContribution;
            attackContribution = stats.attack * Mathf.Max(0f, damageBonus);
            speedContribution = Mathf.Max(0f, speedBonus);
            stats.attack += attackContribution;
            stats.attackSpeed += speedContribution;
        }
    }
}
