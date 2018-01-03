using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ModdedAbilityHijack : Ability
{

    public ModdedAbilityHijack(Ability.AbilityData _data, UID _owner) : base(_data, _owner)
    {
        ModdedAbilityHijack.AbilityData abilityData = (ModdedAbilityHijack.AbilityData)_data;
        this.m_HijackedAbilityIDs = abilityData.m_HijackedAbilityIDs;
        this.m_AIEntity.m_Modifiers.RegisterEventCallback(ModifierType.EnergyRegenRate, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.m_Modifiers.RegisterEventCallback(ModifierType.EnergyRegenRateMultiplier, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.m_Modifiers.RegisterEventCallback(ModifierType.JammerAmount, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.RegisterEventCallback(AIEventNotification.Killed, new AIEntityEvent(this.NotifyMyDeath));

    }


    public override Ability.SaveAbility ToSave()
    {
        return new ModdedAbilityHijack.SaveAbilityHijack(this);
    }


    public override void FromSave(Ability.SaveAbility a)
    {
        base.FromSave(a);
        ModdedAbilityHijack.SaveAbilityHijack saveAbilityHijack = a as ModdedAbilityHijack.SaveAbilityHijack;
    }

    public static System.Collections.Generic.List<AIEntity> HijackedEntities
    {
        get
        {
            return ModdedAbilityHijack.m_HijackedEntities;
        }
    }

    
    public void OnModifierChanged(ModifierType modType)
    {
        if (modType == ModifierType.JammerAmount)
        {
            base.UpdateEnabled();
        }
        else
        {
            this.UpdateEnergyRegen();
        }
    }

    private void TargetHijackEnd(Transform _target)
    {
        AIEntity component = _target.GetComponent<AIEntity>();
        if (component)
        {
            this.UnHijack(component);
        }
    }

    private void TargetHijacked(Transform target)
    {
        if (target == null)
        {
            Debug.Log("ModdedAbilityHijack: TargetHijacked Cancelled");
            this.Cancel();
            return;
        }
        Debug.Log("ModdedAbilityHijack: TargetHijacked " + target.name);
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
            foreach (int num in this.m_HijackedAbilityIDs)
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

    
    private void RemoveHijackedModifiers(AIEntity aie)
    {
        aie.m_Modifiers.RemoveModifier(ModifierType.AccuracyOffset, this.m_UID);
        aie.m_Modifiers.RemoveModifier(ModifierType.SpeedOffset, this.m_UID);
        aie.m_Modifiers.RemoveModifier(ModifierType.DamagePercentageOffset, this.m_UID);
    }

    
    public static float GetHijackTime(int levelRequired, int currentLevel)
    {
        return ModdedAbilityHijack.HijackTime[Mathf.Clamp(currentLevel - levelRequired, 0, ModdedAbilityHijack.HijackTime.Length)] * Mathf.Lerp(1f, ModdedAbilityHijack.HaijackLevelTimeMultiplier, Mathf.Clamp01((float)levelRequired / 5f));
    }

    
    public void NotifyMyDeath(AIEntity _whoDied, AIEventNotification _event)
    {
        this.Remove();
        if (!SrNetworkManager.RemoteClient)
        {
            System.Collections.Generic.List<AIEntity> list = ModdedAbilityHijack.HijackedEntities.FindAll((AIEntity x) => x.IsHijackedBy(_whoDied)).ToList<AIEntity>();
            foreach (AIEntity aientity in list)
            {
                aientity.Unhijack();
            }
        }
    }

    
    public void NotifyDeath(AIEntity _whoDied, AIEventNotification _event)
    {
        if (_whoDied.transform == this.m_Target)
        {
            this.Cancel();
        }
        else
        {
            this.TargetHijackEnd(_whoDied.transform);
        }
    }

    
    private void UnHijack(AIEntity _target)
    {
        Debug.Log("ModdedAbilityHijack: UnHijack " + _target.name);
        _target.UnRegisterEventCallback(AIEventNotification.Killed, new AIEntityEvent(this.NotifyDeath));
        _target.UnRegisterEventCallback(AIEventNotification.Bribed, new AIEntityEvent(this.NotifyDeath));
        _target.UnRegisterEventCallback(AIEventNotification.Despawn, new AIEntityEvent(this.NotifyDeath));
        _target.UnRegisterEventCallback(AIEventNotification.Unhijacked, new AIEntityEvent(this.NotifyDeath));
        _target.m_Locomotion.RemoveWalkableLayer(NavMeshLayers.JustAgents);
        if (base.isServer)
        {
            this.RemoveHijackedAbilities(_target);
        }
        this.RemoveHijackedModifiers(_target);
        this.UpdateEnergyRegen();
        base.UpdateEnabled();
        ModdedAbilityHijack.HijackedEntities.Remove(_target);
    }

    
    protected override void ActivatingStart()
    {
        Debug.Log("ModdedAbilityHijack: ActivatingStart");
        base.ActivatingStart();
        //UID component = this.m_Target.GetComponent<UID>();
        //if (component == null)
        //{
        //    Debug.LogWarning("TODO: set this up to work for a position as well as a target");
        //}
        if (this.m_Target != null)
        {
            AIEntity component2 = this.m_Target.GetComponent<AIEntity>();
            this.m_AIEntity.HijackTarget(this.m_Target, base.GetRange(), ModdedAbilityHijack.GetHijackTime(component2.HijackLevelRequired, this.m_AIEntity.GetHijackerLevel()), new AbilityHijack.TargetHijackedDelegate(this.TargetHijacked), true, AudioManager.Get().m_HijackTimer);
            this.UpdateEnergyRegen();
        }
    }

    // Token: 0x06001A0F RID: 6671 RVA: 0x000D4D8C File Offset: 0x000D2F8C
    public override void Cancel()
    {
        if (this.m_Target != null)
        {
            AIEntity component = this.m_Target.GetComponent<AIEntity>();
            if (component != null)
            {
                if (!(component is CivilianAI))
                {
                    component.StartSearch(true, this.m_Owner.transform.position);
                }
                else
                {
                    component.Flee(component.transform.position + new Vector3((UnityEngine.Random.value - 0.5f) * 10f, 0f, (UnityEngine.Random.value - 0.5f) * 10f), 10f, 0f, 30f);
                }
            }
        }
        if (DebugOptions.ms_DebugEnabled)
        {
            Debug.Log("ModdedAbilityHijack: Cancel " + Utils.GetStackTrace(8));
        }
        this.UpdateEnergyRegen();
        this.m_Target = null;
        base.Cancel();
    }

    // Token: 0x06001A10 RID: 6672 RVA: 0x000D4E70 File Offset: 0x000D3070
    protected override int FindValidTargetPrepped(RaycastHit[] hits)
    {
        float num = (float)this.m_AIEntity.GetHijackerLevel();
        this.m_CursorTex = Manager.GetInputControl().m_CursorTextureInteract;
        for (int i = 0; i < hits.Length; i++)
        {
            AIEntity component = base.GetTransform(hits[i]).GetComponent<AIEntity>();
            if (ModdedAbilityHijack.PossibleHijackTarget(component, this.m_Owner))
            {
                if (component.HijackLevelRequired > 0 && !component.IsReturningToBase() && !this.TargetIsResearcher(component))
                {
                    if ((float)component.HijackLevelRequired <= num && !this.m_AIEntity.IsJammed && !component.IsScrambled())
                    {
                        return i;
                    }
                    this.m_CursorTex = Manager.GetInputControl().m_CursorTextureInteractFailLevel;
                }
                else
                {
                    this.m_CursorTex = Manager.GetInputControl().m_CursorTextureInteractFailLevel;
                }
            }
        }
        return -1;
    }

    // Token: 0x06001A11 RID: 6673 RVA: 0x000D4F4C File Offset: 0x000D314C
    protected override void HandleMouseOutPrepped(Transform target)
    {
        Debug.Log("ModdedAbilityHijack: HandleMouseOutPrepped");
    }

    // Token: 0x06001A12 RID: 6674 RVA: 0x000D4F5C File Offset: 0x000D315C
    public static bool ValidHijackTarget(UID _uid, AIEntity _hijacker)
    {
        return _uid && ModdedAbilityHijack.ValidHijackTarget(_uid.GetComponent<AIEntity>(), _hijacker);
    }

    // Token: 0x06001A13 RID: 6675 RVA: 0x000D4F78 File Offset: 0x000D3178
    public static bool ValidHijackTarget(AIEntity _aie, AIEntity _hijacker)
    {
        return ModdedAbilityHijack.PossibleHijackTarget(_aie, _hijacker.m_UID);
    }

    // Token: 0x06001A14 RID: 6676 RVA: 0x000D4F88 File Offset: 0x000D3188
    private static bool PossibleHijackTarget(AIEntity _aie, UID _owner)
    {
        if (!(_aie != null) || _owner.IsAlly(_aie.m_UID) || !_aie.ValidHijackTarget())
        {
            return false;
        }
        StateReturnToBase stateReturnToBase = (StateReturnToBase)_aie.m_Stack.Find(typeof(StateReturnToBase), true);
        return stateReturnToBase == null || stateReturnToBase.m_Why != StateReturnToBase.Why.Cloned;
    }

    // Token: 0x06001A15 RID: 6677 RVA: 0x000D4FF8 File Offset: 0x000D31F8
    public override string GetTargetToolTip(GameObject target)
    {
        float num = (float)this.m_AIEntity.GetHijackerLevel();
        AIEntity component = target.GetComponent<AIEntity>();
        if (this.m_State == Ability.AbilityState.prepped && ModdedAbilityHijack.PossibleHijackTarget(component, this.m_Owner))
        {
            int hijackLevelRequired = component.HijackLevelRequired;
            if ((hijackLevelRequired <= 0 || component.IsReturningToBase()) && this.TargetIsResearcher(component))
            {
                return TextManager.GetLoc("HIJACK_TOOLTIP_03", true, false);
            }
            if ((float)hijackLevelRequired > num)
            {
                return string.Format(TextManager.GetLoc("HIJACK_TOOLTIP_02", true, false), hijackLevelRequired);
            }
            if (this.m_AIEntity.IsJammed)
            {
                return TextManager.GetLoc("HIJACK_TOOLTIP_04", true, false);
            }
        }
        return string.Empty;
    }

    // Token: 0x06001A16 RID: 6678 RVA: 0x000D50B0 File Offset: 0x000D32B0
    private bool TargetIsResearcher(AIEntity aie)
    {
        if (aie != null)
        {
            VIP component = aie.GetComponent<VIP>();
            if (component != null)
            {
                return VIP.IsResearcher(aie);
            }
        }
        return false;
    }

    // Token: 0x06001A17 RID: 6679 RVA: 0x000D50E4 File Offset: 0x000D32E4
    public override bool HandleMessage(Message _msg)
    {
        bool result = base.HandleMessage(_msg);
        EventMessageType type = _msg.m_Type;
        if (type == EventMessageType.KNOCKEDBACK || type == EventMessageType.STUNNED || type == EventMessageType.KILLED)
        {
            this.Cancel();
            result = true;
        }
        return result;
    }

    // Token: 0x06001A18 RID: 6680 RVA: 0x000D512C File Offset: 0x000D332C
    protected override void PreppedStart()
    {
        base.PreppedStart();
        Debug.Log("ModdedAbilityHijack: PreppedStart");
    }

    // Token: 0x06001A19 RID: 6681 RVA: 0x000D5140 File Offset: 0x000D3340
    public override void Remove()
    {
        base.Remove();
        this.m_AIEntity.m_Modifiers.UnRegisterEventCallback(ModifierType.EnergyRegenRate, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.m_Modifiers.UnRegisterEventCallback(ModifierType.EnergyRegenRateMultiplier, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.m_Modifiers.UnRegisterEventCallback(ModifierType.JammerAmount, new Modifiers.OnModifierChangedDelegate(this.OnModifierChanged));
        this.m_AIEntity.UnRegisterEventCallback(AIEventNotification.Killed, new AIEntityEvent(this.NotifyMyDeath));
    }

    // Token: 0x06001A1A RID: 6682 RVA: 0x000D51C4 File Offset: 0x000D33C4
    private void RemoveHijackedAbilities(AIEntity hijackedEntity)
    {
        foreach (int abilityId in this.m_HijackedAbilityIDs)
        {
            hijackedEntity.ServerRemoveAbility(abilityId);
        }
    }

    // Token: 0x06001A1B RID: 6683 RVA: 0x000D51F8 File Offset: 0x000D33F8
    public override void TransferFrom(Ability otherAbility)
    {
        System.Collections.Generic.List<AIEntity> list = ModdedAbilityHijack.HijackedEntities.ToList<AIEntity>();
        foreach (AIEntity aientity in list)
        {
            aientity.Unhijack();
            this.FinalizeTargetHijacked(aientity.transform);
        }
    }

    // Token: 0x06001A1C RID: 6684 RVA: 0x000D5264 File Offset: 0x000D3464
    protected override bool GetDesiredEnabledValue()
    {
        return base.GetDesiredEnabledValue() && ModdedAbilityHijack.HijackedEntities.Count < this.m_AIEntity.GetHijackerLevel() && !this.m_AIEntity.IsJammed;
    }

    // Token: 0x06001A1D RID: 6685 RVA: 0x000D52A8 File Offset: 0x000D34A8
    private float GetTotalHijackCost()
    {
        float num = 0f;
        foreach (AIEntity aientity in ModdedAbilityHijack.HijackedEntities)
        {
            num += ModdedAbilityHijack.m_HijackEnergyRegenOffsets[Mathf.Clamp(aientity.HijackLevelRequired - 1, 0, ModdedAbilityHijack.m_HijackEnergyRegenOffsets.Length - 1)];
        }
        return num;
    }

    // Token: 0x06001A1E RID: 6686 RVA: 0x000D5324 File Offset: 0x000D3524
    public override string GetToolTip()
    {
        string text = base.GetToolTip();
        text = text + "\n " + string.Format(TextManager.GetLoc("TOOLTIP_HIJACKED_COUNT", true, false), ModdedAbilityHijack.HijackedEntities.Count, this.m_AIEntity.GetHijackerLevel());
        Energy component = this.m_AIEntity.GetComponent<Energy>();
        float totalHijackCost = this.GetTotalHijackCost();
        float energyRegenRate = component.GetEnergyRegenRate(true, true);
        if (energyRegenRate > 0f)
        {
            text = text + "\n " + string.Format(TextManager.GetLoc("TOOLTIP_HIJACK_COST", true, false), Mathf.Clamp01(totalHijackCost / energyRegenRate));
        }
        float energyRegenPenalty = this.GetEnergyRegenPenalty();
        if (energyRegenPenalty < 0f)
        {
            text = text + "\n " + string.Format(TextManager.GetLoc("TOOLTIP_HIJACK_PENALTY", true, false), Mathf.Clamp(energyRegenPenalty, -0.65f, 0f));
        }
        if (this.m_AIEntity.IsJammed)
        {
            text = text + "\n " + TextManager.GetLoc("HIJACK_TOOLTIP_04", true, false);
        }
        return text;
    }

    // Token: 0x06001A1F RID: 6687 RVA: 0x000D5438 File Offset: 0x000D3638
    public static void OnReset()
    {
        ModdedAbilityHijack.m_HijackedEntities.Clear();
    }

    // Token: 0x04001799 RID: 6041
    private const float m_AimOffset = 0.03f;

    // Token: 0x0400179A RID: 6042
    private const float m_DamageOffset = 0.03f;

    // Token: 0x0400179B RID: 6043
    private const float m_SpeedOffset = 0.03f;

    // Token: 0x0400179C RID: 6044
    private const float m_AgentMaxAimOffset = 0.15f;

    // Token: 0x0400179D RID: 6045
    private const float m_AgentMaxDamageOffset = 0.2f;

    // Token: 0x0400179E RID: 6046
    private const float m_AgentMaxSpeedOffset = 0.07f;

    // Token: 0x0400179F RID: 6047
    private const float m_JammedAimPenalty = 0.2f;

    // Token: 0x040017A0 RID: 6048
    private const float m_JammedDamagePenalty = 0.35f;

    // Token: 0x040017A1 RID: 6049
    private const float m_JammedSpeedPenalty = 0.07f;

    // Token: 0x040017A2 RID: 6050
    private const float m_EnergyRegenPenaltyMultiplier = 0.07f;

    // Token: 0x040017A3 RID: 6051
    private int[] m_HijackedAbilityIDs;

    // Token: 0x040017A4 RID: 6052
    public static float[] m_HijackEnergyRegenOffsets = new float[]
    {
        0.4f,
        0.65f,
        1f,
        1.4f,
        2f
    };

    // Token: 0x040017A5 RID: 6053
    private static readonly float[] HijackTime = new float[]
    {
        7f,
        6f,
        5f,
        4f,
        3f
    };

    // Token: 0x040017A6 RID: 6054
    private static readonly float HaijackLevelTimeMultiplier = 1.75f;

    // Token: 0x040017A7 RID: 6055
    protected static System.Collections.Generic.List<AIEntity> m_HijackedEntities = new System.Collections.Generic.List<AIEntity>();

    // Token: 0x0200033B RID: 827
    public class SaveAbilityHijack : Ability.SaveAbility
    {
        // Token: 0x06001A20 RID: 6688 RVA: 0x000D5444 File Offset: 0x000D3644
        public SaveAbilityHijack()
        {
        }

        // Token: 0x06001A21 RID: 6689 RVA: 0x000D5458 File Offset: 0x000D3658
        public SaveAbilityHijack(ModdedAbilityHijack a) : base(a)
        {
        }


        public System.Collections.Generic.List<uint> m_HijackedUIDs = new System.Collections.Generic.List<uint>();
    }


    [System.Serializable]
    public new class AbilityData : AbilityHijack.AbilityData
    {

        public AbilityData()
        {
        }


        public AbilityData(int uid) : base(uid)
        {
        }


        public override Ability Create(UID owner)
        {
            return new ModdedAbilityHijack(this, owner);
        }
    }

    public delegate void TargetHijackedDelegate(Transform t);

    public delegate void AttackCompleteDelegate(bool wasInterrupted);
}

