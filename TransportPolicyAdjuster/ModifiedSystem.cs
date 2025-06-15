using System.Runtime.CompilerServices;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Policies;
using Game.Prefabs;
using Game.PSI;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using UnityEngine.Scripting;
using PolicyEventInfo = Game.Policies.ModifiedSystem.PolicyEventInfo;
using PolicyRange = Game.Policies.ModifiedSystem.PolicyRange;

namespace TransportPolicyAdjuster
{
    public partial class ModifiedSystem : GameSystemBase
    {
        [BurstCompile]
        private struct ModifyPolicyJob : IJobChunk
        {
            [ReadOnly]
            public DistrictModifierInitializeSystem.DistrictModifierRefreshData m_DistrictModifierRefreshData;

            [ReadOnly]
            public BuildingModifierInitializeSystem.BuildingModifierRefreshData m_BuildingModifierRefreshData;

            [ReadOnly]
            public TransportPolicyAdjuster.RouteModifierInitializeSystem.RouteModifierRefreshData m_RouteModifierRefreshData;

            [ReadOnly]
            public CityModifierUpdateSystem.CityModifierRefreshData m_CityModifierRefreshData;

            [ReadOnly]
            public NativeList<ArchetypeChunk> m_EffectProviderChunks;

            [ReadOnly]
            public ComponentTypeHandle<Modify> m_ModifyType;

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.ServiceUpgrade> m_ServiceUpgradeData;

            [ReadOnly]
            public Entity m_TicketPricePolicy;

            public ComponentLookup<District> m_DistrictData;

            public ComponentLookup<Building> m_BuildingData;

            public ComponentLookup<Extension> m_ExtensionData;

            public ComponentLookup<Route> m_RouteData;

            public ComponentLookup<City> m_CityData;

            public BufferLookup<DistrictModifier> m_DistrictModifiers;

            public BufferLookup<BuildingModifier> m_BuildingModifiers;

            public BufferLookup<RouteModifier> m_RouteModifiers;

            public BufferLookup<CityModifier> m_CityModifiers;

            public BufferLookup<Policy> m_Policies;

            public EntityCommandBuffer m_CommandBuffer;

            public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

            public NativeQueue<PolicyEventInfo>.ParallelWriter m_PolicyEventInfos;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Modify> nativeArray = chunk.GetNativeArray(ref m_ModifyType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Modify modify = nativeArray[i];
                    Extension componentData;
                    BuildingOptionData componentData2;
                    if (m_Policies.TryGetBuffer(modify.m_Entity, out var bufferData))
                    {
                        m_CommandBuffer.AddComponent<Updated>(modify.m_Entity);
                        int num = 0;
                        while (true)
                        {
                            if (num < bufferData.Length)
                            {
                                Policy policy = bufferData[num];
                                if (policy.m_Policy == modify.m_Policy)
                                {
                                    if ((modify.m_Flags & PolicyFlags.Active) == 0)
                                    {
                                        CheckFreeBusTicketEventTrigger(policy);
                                        if (!m_DistrictModifierRefreshData.m_PolicySliderData.HasComponent(policy.m_Policy))
                                        {
                                            bufferData.RemoveAt(num);
                                            RefreshEffects(modify.m_Entity, modify.m_Policy, bufferData);
                                            m_PolicyEventInfos.Enqueue(new PolicyEventInfo
                                            {
                                                m_Activated = false,
                                                m_Entity = modify.m_Policy,
                                                m_PolicyRange = GetPolicyRange(modify.m_Entity, modify.m_Policy)
                                            });
                                            break;
                                        }
                                        if (m_DistrictModifierRefreshData.m_PolicySliderData[policy.m_Policy].m_Default == policy.m_Adjustment)
                                        {
                                            bufferData.RemoveAt(num);
                                            RefreshEffects(modify.m_Entity, modify.m_Policy, bufferData);
                                            m_PolicyEventInfos.Enqueue(new PolicyEventInfo
                                            {
                                                m_Activated = false,
                                                m_Entity = modify.m_Policy,
                                                m_PolicyRange = GetPolicyRange(modify.m_Entity, modify.m_Policy)
                                            });
                                            break;
                                        }
                                    }
                                    policy.m_Flags = modify.m_Flags;
                                    policy.m_Adjustment = modify.m_Adjustment;
                                    bufferData[num] = policy;
                                    RefreshEffects(modify.m_Entity, modify.m_Policy, bufferData);
                                    break;
                                }
                                num++;
                                continue;
                            }
                            if ((modify.m_Flags & PolicyFlags.Active) != 0)
                            {
                                bufferData.Add(new Policy(modify.m_Policy, modify.m_Flags, modify.m_Adjustment));
                                RefreshEffects(modify.m_Entity, modify.m_Policy, bufferData);
                                m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.PolicyActivated, modify.m_Policy, Entity.Null, Entity.Null));
                                m_PolicyEventInfos.Enqueue(new PolicyEventInfo
                                {
                                    m_Activated = true,
                                    m_Entity = modify.m_Policy,
                                    m_PolicyRange = GetPolicyRange(modify.m_Entity, modify.m_Policy)
                                });
                            }
                            break;
                        }
                    }
                    else if (m_ExtensionData.TryGetComponent(modify.m_Entity, out componentData) && m_BuildingModifierRefreshData.m_BuildingOptionData.TryGetComponent(modify.m_Policy, out componentData2) && BuildingUtils.HasOption(componentData2, BuildingOption.Inactive))
                    {
                        m_CommandBuffer.AddComponent<Updated>(modify.m_Entity);
                        if ((modify.m_Flags & PolicyFlags.Active) != 0)
                        {
                            componentData.m_Flags |= ExtensionFlags.Disabled;
                        }
                        else
                        {
                            componentData.m_Flags &= ~ExtensionFlags.Disabled;
                        }
                        m_ExtensionData[modify.m_Entity] = componentData;
                    }
                    if (m_ServiceUpgradeData.HasComponent(modify.m_Entity) && m_OwnerData.TryGetComponent(modify.m_Entity, out var componentData3))
                    {
                        m_CommandBuffer.AddComponent<Updated>(componentData3.m_Owner);
                    }
                }
            }

            private void CheckFreeBusTicketEventTrigger(Policy policy)
            {
                if (m_TicketPricePolicy == policy.m_Policy)
                {
                    m_TriggerBuffer.Enqueue(new TriggerAction
                    {
                        m_TriggerType = TriggerType.FreePublicTransport,
                        m_Value = 0f,
                        m_TriggerPrefab = policy.m_Policy,
                        m_SecondaryTarget = Entity.Null,
                        m_PrimaryTarget = Entity.Null
                    });
                }
            }

            private PolicyRange GetPolicyRange(Entity entity, Entity policy)
            {
                if (m_DistrictModifierRefreshData.m_DistrictOptionData.HasComponent(policy) && m_DistrictData.HasComponent(entity))
                {
                    return PolicyRange.District;
                }
                if (m_DistrictModifierRefreshData.m_DistrictModifierData.HasBuffer(policy) && m_DistrictModifiers.HasBuffer(entity))
                {
                    return PolicyRange.District;
                }
                if (m_BuildingModifierRefreshData.m_BuildingOptionData.HasComponent(policy) && m_BuildingData.HasComponent(entity))
                {
                    return PolicyRange.Building;
                }
                if (m_BuildingModifierRefreshData.m_BuildingModifierData.HasBuffer(policy) && m_BuildingModifiers.HasBuffer(entity))
                {
                    return PolicyRange.Building;
                }
                if (m_RouteModifierRefreshData.m_RouteOptionData.HasComponent(policy) && m_RouteData.HasComponent(entity))
                {
                    return PolicyRange.Route;
                }
                if (m_RouteModifierRefreshData.m_RouteModifierData.HasBuffer(policy) && m_RouteModifiers.HasBuffer(entity))
                {
                    return PolicyRange.Route;
                }
                if (m_CityModifierRefreshData.m_CityOptionData.HasComponent(policy) && m_CityData.HasComponent(entity))
                {
                    return PolicyRange.City;
                }
                if (m_CityModifierRefreshData.m_CityModifierData.HasBuffer(policy) && m_CityModifiers.HasBuffer(entity))
                {
                    return PolicyRange.City;
                }
                return PolicyRange.None;
            }

            private void RefreshEffects(Entity entity, Entity policy, DynamicBuffer<Policy> policies)
            {
                if (m_DistrictModifierRefreshData.m_DistrictOptionData.HasComponent(policy) && m_DistrictData.HasComponent(entity))
                {
                    District district = m_DistrictData[entity];
                    m_DistrictModifierRefreshData.RefreshDistrictOptions(ref district, policies);
                    m_DistrictData[entity] = district;
                }
                if (m_DistrictModifierRefreshData.m_DistrictModifierData.HasBuffer(policy) && m_DistrictModifiers.HasBuffer(entity))
                {
                    DynamicBuffer<DistrictModifier> modifiers = m_DistrictModifiers[entity];
                    m_DistrictModifierRefreshData.RefreshDistrictModifiers(modifiers, policies);
                }
                if (m_BuildingModifierRefreshData.m_BuildingOptionData.HasComponent(policy) && m_BuildingData.HasComponent(entity))
                {
                    Building building = m_BuildingData[entity];
                    m_BuildingModifierRefreshData.RefreshBuildingOptions(ref building, policies);
                    m_BuildingData[entity] = building;
                }
                if (m_BuildingModifierRefreshData.m_BuildingModifierData.HasBuffer(policy) && m_BuildingModifiers.HasBuffer(entity))
                {
                    DynamicBuffer<BuildingModifier> modifiers2 = m_BuildingModifiers[entity];
                    m_BuildingModifierRefreshData.RefreshBuildingModifiers(modifiers2, policies);
                }
                if (m_RouteModifierRefreshData.m_RouteOptionData.HasComponent(policy) && m_RouteData.HasComponent(entity))
                {
                    Route route = m_RouteData[entity];
                    m_RouteModifierRefreshData.RefreshRouteOptions(ref route, policies);
                    m_RouteData[entity] = route;
                }
                if (m_RouteModifierRefreshData.m_RouteModifierData.HasBuffer(policy) && m_RouteModifiers.HasBuffer(entity))
                {
                    DynamicBuffer<RouteModifier> modifiers3 = m_RouteModifiers[entity];
                    m_RouteModifierRefreshData.RefreshRouteModifiers(modifiers3, policies);
                }
                if (m_CityModifierRefreshData.m_CityOptionData.HasComponent(policy) && m_CityData.HasComponent(entity))
                {
                    Game.City.City city = m_CityData[entity];
                    m_CityModifierRefreshData.RefreshCityOptions(ref city, policies);
                    m_CityData[entity] = city;
                }
                if (m_CityModifierRefreshData.m_CityModifierData.HasBuffer(policy) && m_CityModifiers.HasBuffer(entity))
                {
                    NativeList<CityModifierData> tempModifierList = new NativeList<CityModifierData>(10, Allocator.Temp);
                    DynamicBuffer<CityModifier> modifiers4 = m_CityModifiers[entity];
                    m_CityModifierRefreshData.RefreshCityModifiers(modifiers4, policies, m_EffectProviderChunks, tempModifierList);
                    tempModifierList.Dispose();
                }
                m_CommandBuffer.AddComponent<Updated>(entity);
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public ComponentTypeHandle<Modify> __Game_Policies_Modify_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentLookup<Owner> __Game_Common_Owner_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.ServiceUpgrade> __Game_Buildings_ServiceUpgrade_RO_ComponentLookup;

            public ComponentLookup<District> __Game_Areas_District_RW_ComponentLookup;

            public ComponentLookup<Building> __Game_Buildings_Building_RW_ComponentLookup;

            public ComponentLookup<Extension> __Game_Buildings_Extension_RW_ComponentLookup;

            public ComponentLookup<Route> __Game_Routes_Route_RW_ComponentLookup;

            public ComponentLookup<Game.City.City> __Game_City_City_RW_ComponentLookup;

            public BufferLookup<DistrictModifier> __Game_Areas_DistrictModifier_RW_BufferLookup;

            public BufferLookup<BuildingModifier> __Game_Buildings_BuildingModifier_RW_BufferLookup;

            public BufferLookup<RouteModifier> __Game_Routes_RouteModifier_RW_BufferLookup;

            public BufferLookup<CityModifier> __Game_City_CityModifier_RW_BufferLookup;

            public BufferLookup<Policy> __Game_Policies_Policy_RW_BufferLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                __Game_Policies_Modify_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Modify>(isReadOnly: true);
                __Game_Common_Owner_RO_ComponentLookup = state.GetComponentLookup<Owner>(isReadOnly: true);
                __Game_Buildings_ServiceUpgrade_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.ServiceUpgrade>(isReadOnly: true);
                __Game_Areas_District_RW_ComponentLookup = state.GetComponentLookup<District>();
                __Game_Buildings_Building_RW_ComponentLookup = state.GetComponentLookup<Building>();
                __Game_Buildings_Extension_RW_ComponentLookup = state.GetComponentLookup<Extension>();
                __Game_Routes_Route_RW_ComponentLookup = state.GetComponentLookup<Route>();
                __Game_City_City_RW_ComponentLookup = state.GetComponentLookup<Game.City.City>();
                __Game_Areas_DistrictModifier_RW_BufferLookup = state.GetBufferLookup<DistrictModifier>();
                __Game_Buildings_BuildingModifier_RW_BufferLookup = state.GetBufferLookup<BuildingModifier>();
                __Game_Routes_RouteModifier_RW_BufferLookup = state.GetBufferLookup<RouteModifier>();
                __Game_City_CityModifier_RW_BufferLookup = state.GetBufferLookup<CityModifier>();
                __Game_Policies_Policy_RW_BufferLookup = state.GetBufferLookup<Policy>();
            }
        }

        private EntityQuery m_EventQuery;

        private EntityQuery m_EffectProviderQuery;

        private ModificationBarrier4 m_ModificationBarrier;

        private TriggerSystem m_TriggerSystem;

        private NativeQueue<PolicyEventInfo> m_PolicyEventInfos;

        private DistrictModifierInitializeSystem.DistrictModifierRefreshData m_DistrictModifierRefreshData;

        private BuildingModifierInitializeSystem.BuildingModifierRefreshData m_BuildingModifierRefreshData;

        private TransportPolicyAdjuster.RouteModifierInitializeSystem.RouteModifierRefreshData m_RouteModifierRefreshData;

        private CityModifierUpdateSystem.CityModifierRefreshData m_CityModifierRefreshData;

        private Entity m_TicketPricePolicy;

        private TypeHandle __TypeHandle;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_PolicyEventInfos = new NativeQueue<PolicyEventInfo>(Allocator.Persistent);
            m_DistrictModifierRefreshData = new DistrictModifierInitializeSystem.DistrictModifierRefreshData(this);
            m_BuildingModifierRefreshData = new BuildingModifierInitializeSystem.BuildingModifierRefreshData(this);
            m_RouteModifierRefreshData = new TransportPolicyAdjuster.RouteModifierInitializeSystem.RouteModifierRefreshData(this);
            m_CityModifierRefreshData = new CityModifierUpdateSystem.CityModifierRefreshData(this);
            PrefabSystem orCreateSystemManaged = base.World.GetOrCreateSystemManaged<PrefabSystem>();
            EntityQuery entityQuery = GetEntityQuery(ComponentType.ReadOnly<UITransportConfigurationData>());
            UITransportConfigurationPrefab singletonPrefab = orCreateSystemManaged.GetSingletonPrefab<UITransportConfigurationPrefab>(entityQuery);
            m_TicketPricePolicy = orCreateSystemManaged.GetEntity(singletonPrefab.m_TicketPricePolicy);
            m_EventQuery = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<Modify>());
            m_EffectProviderQuery = GetEntityQuery(ComponentType.ReadOnly<CityEffectProvider>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Destroyed>(), ComponentType.Exclude<Temp>());
            m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
            RequireForUpdate(m_EventQuery);
        }

        [Preserve]
        protected override void OnUpdate()
        {
            JobHandle outJobHandle;
            NativeList<ArchetypeChunk> effectProviderChunks = m_EffectProviderQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle);
            m_DistrictModifierRefreshData.Update(this);
            m_BuildingModifierRefreshData.Update(this);
            m_RouteModifierRefreshData.Update(this);
            m_CityModifierRefreshData.Update(this);
            NativeQueue<TriggerAction> nativeQueue = (m_TriggerSystem.Enabled ? m_TriggerSystem.CreateActionBuffer() : new NativeQueue<TriggerAction>(Allocator.TempJob));
            JobHandle jobHandle = JobChunkExtensions.Schedule(new ModifyPolicyJob
            {
                m_DistrictModifierRefreshData = m_DistrictModifierRefreshData,
                m_BuildingModifierRefreshData = m_BuildingModifierRefreshData,
                m_RouteModifierRefreshData = m_RouteModifierRefreshData,
                m_CityModifierRefreshData = m_CityModifierRefreshData,
                m_EffectProviderChunks = effectProviderChunks,
                m_ModifyType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Policies_Modify_RO_ComponentTypeHandle, ref base.CheckedStateRef),
                m_OwnerData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Common_Owner_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ServiceUpgradeData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_ServiceUpgrade_RO_ComponentLookup, ref base.CheckedStateRef),
                m_TriggerBuffer = nativeQueue.AsParallelWriter(),
                m_DistrictData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Areas_District_RW_ComponentLookup, ref base.CheckedStateRef),
                m_BuildingData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_Building_RW_ComponentLookup, ref base.CheckedStateRef),
                m_ExtensionData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_Extension_RW_ComponentLookup, ref base.CheckedStateRef),
                m_RouteData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Routes_Route_RW_ComponentLookup, ref base.CheckedStateRef),
                m_CityData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_City_City_RW_ComponentLookup, ref base.CheckedStateRef),
                m_DistrictModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Areas_DistrictModifier_RW_BufferLookup, ref base.CheckedStateRef),
                m_BuildingModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Buildings_BuildingModifier_RW_BufferLookup, ref base.CheckedStateRef),
                m_RouteModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Routes_RouteModifier_RW_BufferLookup, ref base.CheckedStateRef),
                m_CityModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_City_CityModifier_RW_BufferLookup, ref base.CheckedStateRef),
                m_Policies = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Policies_Policy_RW_BufferLookup, ref base.CheckedStateRef),
                m_PolicyEventInfos = m_PolicyEventInfos.AsParallelWriter(),
                m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer(),
                m_TicketPricePolicy = m_TicketPricePolicy
            }, m_EventQuery, JobHandle.CombineDependencies(base.Dependency, outJobHandle));
            effectProviderChunks.Dispose(jobHandle);
            m_ModificationBarrier.AddJobHandleForProducer(jobHandle);
            if (m_TriggerSystem.Enabled)
            {
                m_TriggerSystem.AddActionBufferWriter(jobHandle);
            }
            else
            {
                nativeQueue.Dispose(jobHandle);
            }
            base.Dependency = jobHandle;
            jobHandle.Complete();
            while (m_PolicyEventInfos.Count > 0)
            {
                Telemetry.Policy(m_PolicyEventInfos.Dequeue());
            }
        }

        [Preserve]
        protected override void OnDestroy()
        {
            m_PolicyEventInfos.Dispose();
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        [Preserve]
        public ModifiedSystem()
        {
        }
    }
}
