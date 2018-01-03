using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Example Satellite Reign pause mod.
/// </summary>
public class Pause : ISrPlugin
{

    string[] abilityNames;
    int[] abilityIds;

    /// <summary>
    /// Plugin initialization 
    /// </summary>
    public void Initialize()
    {
        Debug.Log("Initializing Mod");

        AbilityManager abilityManager = Manager.GetAbilityManager();

        abilityManager.GetAbilityNamesAndIDs(out abilityIds, out abilityNames);
        Debug.Log("Number of Abilities: " + abilityIds.Length.ToString());

        abilityManager.m_AbilityData.Add(new ModdedAbilityHijack.AbilityData(999));

        var field = typeof(AbilityManager).GetField("__ALLAbilityData", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        field.SetValue(abilityManager, null);

        abilityManager.GetAbilityNamesAndIDs(out abilityIds, out abilityNames);
        Debug.Log("Number of Abilities after Patch: " + abilityIds.Length.ToString());

        string output = "";
        for (int i = 0; i < abilityIds.Length; i++)
        {
            output += abilityIds[i] + ": " + abilityNames[i] + "\n";
        }
        Debug.Log(output);

        // TODO log all ability data
    }

    /// <summary>
    /// Called once per frame.
    /// </summary>
    public void Update()
    {
        if (Manager.Get().GameInProgress)
        {
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                try
                {
                    List<AgentAI> agents = AgentAI.GetAgents();
                    AgentAI soldier = agents[0];

                    Abilities abilities = soldier.GetAbilities();

                    Manager.GetUIManager().ShowMessagePopup(abilities.ToString());
                    abilities.AddAbility(999);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message + "\n" + e.StackTrace);
                    Manager.GetUIManager().ShowMessagePopup(e.Message);
                }
            }
        }
    }

    public string GetName()
    {
        return "Test Mod";
    }
}

