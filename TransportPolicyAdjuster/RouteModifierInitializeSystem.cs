using Game.Policies;
using Game.Prefabs;
using Game.Routes;
using HarmonyLib;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TransportPolicyAdjuster
{
    [HarmonyPatch]
    public class RouteModifierInitializeSystem
    {
        [BurstCompile]
        public struct InitializeRouteModifiersJob : IJobChunk
        {
            [ReadOnly]
            public RouteModifierRefreshData m_RouteModifierRefreshData;

            [ReadOnly]
            public BufferTypeHandle<Policy> m_PolicyType;

            public ComponentTypeHandle<Route> m_RouteType;

            public BufferTypeHandle<RouteModifier> m_RouteModifierType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Route> nativeArray = chunk.GetNativeArray(ref m_RouteType);
                BufferAccessor<RouteModifier> bufferAccessor = chunk.GetBufferAccessor(ref m_RouteModifierType);
                BufferAccessor<Policy> bufferAccessor2 = chunk.GetBufferAccessor(ref m_PolicyType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    DynamicBuffer<Policy> policies = bufferAccessor2[i];
                    if (policies.Length != 0)
                    {
                        Route route = nativeArray[i];
                        m_RouteModifierRefreshData.RefreshRouteOptions(ref route, policies);
                        nativeArray[i] = route;
                        if (bufferAccessor.Length != 0)
                        {
                            DynamicBuffer<RouteModifier> modifiers = bufferAccessor[i];
                            m_RouteModifierRefreshData.RefreshRouteModifiers(modifiers, policies);
                        }
                    }
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        public struct RouteModifierRefreshData
        {
            public ComponentLookup<PolicySliderData> m_PolicySliderData;

            public ComponentLookup<RouteOptionData> m_RouteOptionData;

            public BufferLookup<RouteModifierData> m_RouteModifierData;

            public RouteModifierRefreshData(SystemBase system)
            {
                m_PolicySliderData = system.GetComponentLookup<PolicySliderData>(isReadOnly: true);
                m_RouteOptionData = system.GetComponentLookup<RouteOptionData>(isReadOnly: true);
                m_RouteModifierData = system.GetBufferLookup<RouteModifierData>(isReadOnly: true);
            }

            public void Update(SystemBase system)
            {
                m_PolicySliderData.Update(system);
                m_RouteOptionData.Update(system);
                m_RouteModifierData.Update(system);
            }

            public void RefreshRouteOptions(ref Route route, DynamicBuffer<Policy> policies)
            {
                route.m_OptionMask = 0u;
                for (int i = 0; i < policies.Length; i++)
                {
                    Policy policy = policies[i];
                    if ((policy.m_Flags & PolicyFlags.Active) != 0 && m_RouteOptionData.HasComponent(policy.m_Policy))
                    {
                        RouteOptionData routeOptionData = m_RouteOptionData[policy.m_Policy];
                        route.m_OptionMask |= routeOptionData.m_OptionMask;
                    }
                }
            }

            public void RefreshRouteModifiers(DynamicBuffer<RouteModifier> modifiers, DynamicBuffer<Policy> policies)
            {
                modifiers.Clear();
                for (int i = 0; i < policies.Length; i++)
                {
                    Policy policy = policies[i];
                    if ((policy.m_Flags & PolicyFlags.Active) == 0 || !m_RouteModifierData.HasBuffer(policy.m_Policy))
                    {
                        continue;
                    }
                    DynamicBuffer<RouteModifierData> dynamicBuffer = m_RouteModifierData[policy.m_Policy];
                    for (int j = 0; j < dynamicBuffer.Length; j++)
                    {
                        RouteModifierData modifierData = dynamicBuffer[j];
                        float delta;
                        if (m_PolicySliderData.HasComponent(policy.m_Policy))
                        {
                            PolicySliderData policySliderData = m_PolicySliderData[policy.m_Policy];
                            float a = (policy.m_Adjustment - policySliderData.m_Range.min) / (policySliderData.m_Range.max - policySliderData.m_Range.min);
                            a = math.select(a, 0f, policySliderData.m_Range.min == policySliderData.m_Range.max);
                            delta = math.lerp(modifierData.m_Range.min, modifierData.m_Range.max, a);
                        }
                        else
                        {
                            delta = modifierData.m_Range.min;
                        }
                        AddModifier(modifiers, modifierData, delta);
                    }
                }
            }

            public static void AddModifier(DynamicBuffer<RouteModifier> modifiers, RouteModifierData modifierData, float delta)
            {
                while (modifiers.Length <= (int)modifierData.m_Type)
                {
                    modifiers.Add(default(RouteModifier));
                }
                RouteModifier value = modifiers[(int)modifierData.m_Type];
                switch (modifierData.m_Mode)
                {
                    case ModifierValueMode.Relative:
                        value.m_Delta.y = value.m_Delta.y * (1f + delta) + delta;
                        break;
                    case ModifierValueMode.Absolute:
                        value.m_Delta.x += delta;
                        break;
                    case ModifierValueMode.InverseRelative:
                        delta = 1f / math.max(0.001f, 1f + delta) - 1f;
                        value.m_Delta.y = value.m_Delta.y * (1f + delta) + delta;
                        break;
                }
                modifiers[(int)modifierData.m_Type] = value;
            }
        }

        [HarmonyPatch(typeof(Game.Policies.RouteModifierInitializeSystem), nameof(OnUpdate))]
        [HarmonyPrefix]
        public static bool OnUpdate(ref Game.Policies.RouteModifierInitializeSystem __instance)
        {
            var m_RouteModifierRefreshData = __instance.GetMemberValue<RouteModifierRefreshData>("m_RouteModifierRefreshData");
            m_RouteModifierRefreshData.Update(__instance);
            var typeHandle = __instance.GetMemberValue<object>("__TypeHandle");
            var __Game_Routes_RouteModifier_RW_BufferTypeHandle = typeHandle.GetMemberValue<BufferTypeHandle<RouteModifier>>("__Game_Routes_RouteModifier_RW_BufferTypeHandle");
            __Game_Routes_RouteModifier_RW_BufferTypeHandle.Update(ref __instance.CheckedStateRef);
            var __Game_Routes_Route_RW_ComponentTypeHandle = typeHandle.GetMemberValue<ComponentTypeHandle<Route>>("__Game_Routes_Route_RW_ComponentTypeHandle");
            __Game_Routes_Route_RW_ComponentTypeHandle.Update(ref __instance.CheckedStateRef);
            var __Game_Policies_Policy_RO_BufferTypeHandle = typeHandle.GetMemberValue<BufferTypeHandle<Policy>>("__Game_Policies_Policy_RO_BufferTypeHandle");
            __Game_Policies_Policy_RO_BufferTypeHandle.Update(ref __instance.CheckedStateRef);
            InitializeRouteModifiersJob initializeRouteModifiersJob = new()
            {
                m_RouteModifierRefreshData = m_RouteModifierRefreshData,
                m_PolicyType = __Game_Policies_Policy_RO_BufferTypeHandle,
                m_RouteType = __Game_Routes_Route_RW_ComponentTypeHandle,
                m_RouteModifierType = __Game_Routes_RouteModifier_RW_BufferTypeHandle
            };
            InitializeRouteModifiersJob jobData = initializeRouteModifiersJob;

            var dependency = __instance.GetMemberValue<JobHandle>("Dependency");
            __instance.SetMemberValue("Dependency", JobChunkExtensions.ScheduleParallel(jobData, __instance.GetMemberValue<EntityQuery>("m_CreatedQuery"), dependency));

            return false;
        }
    }
}
