using PurrNet;
using System.Collections.Generic;
using UnityEngine;

namespace OfficeAI
{
    /// <summary>
    /// Spawn et gère un pool de NPCs de bureau.
    /// À placer sur un NetworkManager ou un objet dédié.
    /// </summary>
    public class OfficeNPCManager : NetworkBehaviour
    {
        [Header("Spawn")]
        [SerializeField] private GameObject npcPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private int npcCount = 5;

        [Header("Bureaux partagés")]
        [SerializeField] private Transform[] deskPoints;

        private readonly List<OfficerNPC> _npcs = new();

        protected override void OnSpawned()
        {
            base.OnSpawned();
            if (!isServer) return;
            SpawnNPCs();
        }

        private void SpawnNPCs()
        {
            for (int i = 0; i < npcCount && i < spawnPoints.Length; i++)
            {
                var go = Instantiate(npcPrefab,
                    spawnPoints[i].position,
                    spawnPoints[i].rotation);

                var npc = go.GetComponent<OfficerNPC>();
                if (npc == null) continue;

                // FIX: assigner un bureau aléatoire au NPC avant qu'il spawne
                if (deskPoints != null && deskPoints.Length > 0)
                    npc.assignedDesk = deskPoints[Random.Range(0, deskPoints.Length)];

                _npcs.Add(npc);
            }
        }
    }
}