using System;
using System.Reflection;
using UnityEngine;

public class ModdedAbilityHijack_old : ModdedAbilityHijack
{

    // private bool m_RequiresActivating = true;

    public ModdedAbilityHijack_old(Ability.AbilityData _data, UID _owner) : base(_data, _owner) {
        Debug.Log("called ctor of ModdedAbilityHijack");
        

        var field = typeof(Ability).GetField("m_RequiresActivating", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        var value = field.GetValue(this);
        Debug.Log(value);
        field.SetValue(this, true);
    }

    new public static bool ValidHijackTarget(AIEntity _aie, AIEntity _hijacker)
    {
        Manager.GetUIManager().ShowMessagePopup("called with level " + _aie.GetHijackerLevel());
        return false;
    }

    protected new void ActivatingStart()
    {
        Debug.Log("ModdedAbilityHijack: ActivatingStart");
        // TODO this crashes because of call cycle
        var method = typeof(Ability).GetMethod("ActivatingStart", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        method.Invoke(this, null);
        base.ActivatingStart();

        //UID component = this.m_Target.GetComponent<UID>();
        //if (component == null)
        //{
        //    Debug.LogWarning("TODO: set this up to work for a position as well as a target");
        //}
        //  else
        if (this.m_Target != null)
        {
            AIEntity component2 = this.m_Target.GetComponent<AIEntity>();
            this.m_AIEntity.HijackTarget(this.m_Target, base.GetRange(), ModdedAbilityHijack.GetHijackTime(component2.HijackLevelRequired, this.m_AIEntity.GetHijackerLevel()), new ModdedAbilityHijack_old.TargetHijackedDelegate(this.TargetHijacked), true, AudioManager.Get().m_HijackTimer);
            this.UpdateEnergyRegen();
        }
    }

    private void TargetHijacked(Transform target)
    {
        if (target == null)
        {
            base.Log("AbilityHijack: TargetHijacked Cancelled", 0);
            this.Cancel();
            return;
        }
        base.Log("AbilityHijack: TargetHijacked " + target.name, 0);
        base.ActiveStart();
        this.FinalizeTargetHijacked(target);
        this.ActiveFinished(this.m_Target);
        this.m_Target = null;
    }

    private void FinalizeTargetHijacked(Transform target)
    {
        AIEntity component = target.GetComponent<AIEntity>();
        if (base.isServer)
        {
            component.Hijack(this.m_AIEntity, false);

            var field = typeof(ModdedAbilityHijack_old).BaseType.GetField("m_HijackedAbilityIDs", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            int[] hijackedAbilityIDs = (int[])field.GetValue(this);

            foreach (int num in hijackedAbilityIDs)
            {
                if (component.GetAbilities().CanAddAbility(num))
                {
                    component.ServerAddAbility(num);
                }
            }
        }
        component.m_Locomotion.AddWalkableLayer(NavMeshLayers.JustAgents);
        component.RegisterEventCallback(AIEventNotification.Killed, new AIEntityEvent(this.NotifyDeath));
        component.RegisterEventCallback(AIEventNotification.Bribed, new AIEntityEvent(this.NotifyDeath));
        component.RegisterEventCallback(AIEventNotification.Despawn, new AIEntityEvent(this.NotifyDeath));
        component.RegisterEventCallback(AIEventNotification.Unhijacked, new AIEntityEvent(this.NotifyDeath));
        this.UpdateEnergyRegen();
        // TODO
        ModdedAbilityHijack.HijackedEntities.Add(component);
    }

    private void UpdateEnergyRegen()
    {
        if (this.m_Energy == null || this.m_AIEntity == null)
        {
            return;
        }
        this.m_Mods.AddModifier(ModifierType.HijackEnergyCost, this.GetTotalHijackCost(), 0f, this.m_UID, ModifierType.NONE, null);
        float energyRegenPenalty = this.GetEnergyRegenPenalty();
        bool isJammed = this.m_AIEntity.IsJammed;
        System.Collections.Generic.List<AIEntity> hijackedEntities = ModdedAbilityHijack.HijackedEntities;
        foreach (AIEntity aientity in hijackedEntities)
        {
            this.AddHijackedModifiers(aientity, Mathf.Clamp(energyRegenPenalty, -0.7f, (!isJammed && !aientity.IsJammed) ? 0.03f : -0.17f), Mathf.Clamp(energyRegenPenalty, -0.4f, (!isJammed && !aientity.IsJammed) ? 0.03f : -0.04f), Mathf.Clamp(energyRegenPenalty, -0.4f, (!isJammed && !aientity.IsJammed) ? 0.03f : -0.32f));
        }
        float t = Mathf.Clamp01((float)hijackedEntities.Count / (float)ModdedAbilityHijack.m_HijackEnergyRegenOffsets.Length);
        this.AddHijackedModifiers(this.m_AIEntity, Mathf.Lerp(0f, -0.15f, t), Mathf.Lerp(0f, -0.07f, t), Mathf.Lerp(0f, -0.2f, t));
    }

    private float GetTotalHijackCost()
    {
        float num = 0f;
        foreach (AIEntity aientity in ModdedAbilityHijack.HijackedEntities)
        {
            num += ModdedAbilityHijack.m_HijackEnergyRegenOffsets[Mathf.Clamp(aientity.HijackLevelRequired - 1, 0, ModdedAbilityHijack.m_HijackEnergyRegenOffsets.Length - 1)];
        }
        return num;
    }

    private float GetEnergyRegenPenalty()
    {
        if (this.m_Energy == null)
        {
            return 0f;
        }
        return Mathf.Min(0f, this.m_Energy.GetEnergyRegenRate(false, false) * 0.07f);
    }

    private void AddHijackedModifiers(AIEntity aie, float accuracyOffset, float speedOffset, float damageOffset)
    {
        aie.m_Modifiers.AddModifier(ModifierType.AccuracyOffset, accuracyOffset, 0f, this.m_UID, ModifierType.NONE, null);
        aie.m_Modifiers.AddModifier(ModifierType.SpeedOffset, speedOffset, 0f, this.m_UID, ModifierType.NONE, null);
        aie.m_Modifiers.AddModifier(ModifierType.DamagePercentageOffset, damageOffset, 0f, this.m_UID, ModifierType.NONE, null);
    }
    new public class AbilityData : ModdedAbilityHijack.AbilityData
    {
        public AbilityData(int uid) : base(uid) {
            Debug.Log("called ctor of ModdedAbilityHijack.AbilityData");
            this.m_Name = "Modded Hijack";
        }

        public override Ability Create(UID owner)
        {
            return new ModdedAbilityHijack_old(this, owner);
        }
    }
}
