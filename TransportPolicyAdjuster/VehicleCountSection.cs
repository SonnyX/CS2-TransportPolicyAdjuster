using Colossal.UI.Binding;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.UI.InGame;
using HarmonyLib;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TransportPolicyAdjuster
{
    [HarmonyPatch]
    public class VehicleCountSection
    {
        [BurstCompile]
        public struct CalculateVehicleCountJob : IJob
        {
            [ReadOnly]
            public Entity m_SelectedEntity;

            [ReadOnly]
            public Entity m_SelectedPrefab;

            [ReadOnly]
            public Entity m_VehicleCountPolicy;

            [ReadOnly]
            public ComponentLookup<VehicleTiming> m_VehicleTimings;

            [ReadOnly]
            public ComponentLookup<PathInformation> m_PathInformations;

            [ReadOnly]
            public ComponentLookup<TransportLineData> m_TransportLineDatas;

            [ReadOnly]
            public ComponentLookup<PolicySliderData> m_PolicySliderDatas;

            [ReadOnly]
            public BufferLookup<RouteVehicle> m_RouteVehicles;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_RouteWaypoints;

            [ReadOnly]
            public BufferLookup<RouteSegment> m_RouteSegments;

            [ReadOnly]
            public BufferLookup<RouteModifier> m_RouteModifiers;

            [ReadOnly]
            public BufferLookup<RouteModifierData> m_RouteModifierDatas;

            public NativeArray<int> m_IntResults;

            public NativeList<float2> m_CountResults;

            public void Execute()
            {
                TransportLineData transportLineData = m_TransportLineDatas[m_SelectedPrefab];
                DynamicBuffer<RouteVehicle> activeVehicles = m_RouteVehicles[m_SelectedEntity];
                DynamicBuffer<RouteModifier> modifiers = m_RouteModifiers[m_SelectedEntity];
                DynamicBuffer<RouteModifierData> routeModifiers = m_RouteModifierDatas[m_VehicleCountPolicy];
                PolicySliderData sliderData = m_PolicySliderDatas[m_VehicleCountPolicy];

                float defaultVehicleInterval = transportLineData.m_DefaultVehicleInterval;
                float vehicleInterval = defaultVehicleInterval;

                RouteUtils.ApplyModifier(ref vehicleInterval, modifiers, RouteModifierType.VehicleInterval);

                float lineDuration = CalculateStableDuration(transportLineData);

                for (int i = 0; i < routeModifiers.Length; i++)
                {
                    RouteModifierData routeModifier = routeModifiers[i];
                    if (routeModifier.m_Type != RouteModifierType.VehicleInterval)
                    {
                        continue;
                    }

                    ref NativeList<float2> countResults = ref m_CountResults;
                    for (int desiredVehicles = 1; desiredVehicles <= 30; desiredVehicles++)
                    {
                        var delta = 100f / (lineDuration / (defaultVehicleInterval * desiredVehicles));
                        float2 value2 = new float2(desiredVehicles * sliderData.m_Step, delta);
                        countResults.Add(in value2);
                    }
                }

                m_IntResults[0] = CalculateVehicleCount(vehicleInterval, lineDuration);
                m_IntResults[1] = activeVehicles.Length;
            }

            private int CalculateVehicleCount(float vehicleInterval, float lineDuration)
            {
                return math.max(1, (int)math.round(lineDuration / math.max(1f, vehicleInterval)));
            }

            private float CalculateStableDuration(TransportLineData transportLineData)
            {
                DynamicBuffer<RouteWaypoint> waypoints = m_RouteWaypoints[m_SelectedEntity];
                DynamicBuffer<RouteSegment> segments = m_RouteSegments[m_SelectedEntity];
                int firstWaypoint = 0;
                for (int waypoint = 0; waypoint < waypoints.Length; waypoint++)
                {
                    if (m_VehicleTimings.HasComponent(waypoints[waypoint].m_Waypoint))
                    {
                        firstWaypoint = waypoint;
                        break;
                    }
                }

                float duration = 0f;
                for (int currentWaypoint = 0; currentWaypoint < waypoints.Length; currentWaypoint++)
                {
                    int2 @int = firstWaypoint + currentWaypoint;
                    @int.y++;
                    @int = math.select(@int, @int - waypoints.Length, @int >= waypoints.Length);
                    Entity waypoint = waypoints[@int.y].m_Waypoint;
                    Entity segment = segments[@int.x].m_Segment;
                    if (m_PathInformations.TryGetComponent(segment, out var componentData))
                    {
                        duration += componentData.m_Duration;
                    }

                    if (m_VehicleTimings.HasComponent(waypoint))
                    {
                        duration += transportLineData.m_StopDuration;
                    }
                }

                return duration;
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(Visible))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Visible(Game.UI.InGame.VehicleCountSection instance)
        {
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnSetVehicleCount))]
        [HarmonyPrefix]
        public static bool OnSetVehicleCount(float newVehicleCount, ref Game.UI.InGame.VehicleCountSection __instance)
        {
            var m_PoliciesUISystem = __instance.GetMemberValue<PoliciesUISystem>("m_PoliciesUISystem");
            var selectedEntity = __instance.GetMemberValue<Entity>("selectedEntity");
            var m_VehicleCountPolicy = __instance.GetMemberValue<Entity>("m_VehicleCountPolicy");
            var m_CountResult = __instance.GetMemberValue<NativeList<float2>>("m_CountResult");
            float2? delta = null;
            for (int i = 0; i < m_CountResult.Length; i++)
            {
                if (m_CountResult[i].x == newVehicleCount)
                {
                    delta = m_CountResult[i];
                    break;
                }
            }
            if (!delta.HasValue) {
                throw new System.Exception($"TransportPolicyAdjuster: m_CountResult does not contain index {newVehicleCount}");
            }
            m_PoliciesUISystem.SetPolicy(selectedEntity, m_VehicleCountPolicy, active: true, delta.Value.y);
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnWriteProperties))]
        [HarmonyPrefix]
        public static bool OnWriteProperties(ref IJsonWriter writer, ref Game.UI.InGame.VehicleCountSection __instance)
        {
            writer.PropertyName("vehicleCount");
            writer.Write(__instance.GetMemberValue<int>("vehicleCount"));
            writer.PropertyName("activeVehicles");
            writer.Write(__instance.GetMemberValue<int>("activeVehicles"));
            writer.PropertyName("vehicleCounts");

            var max_vehicles = 30;
            writer.ArrayBegin(max_vehicles);
            for (int i = 1; i <= max_vehicles; i++)
            {
                writer.Write(new float2(i * 10, i));
            }

            writer.ArrayEnd();
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnUpdate))]
        [HarmonyPrefix]
        public static bool OnUpdate(ref Game.UI.InGame.VehicleCountSection __instance)
        {
            __instance.SetMemberValue("visible", Visible(__instance));
            var typeHandle = __instance.GetMemberValue<object>("__TypeHandle");
            if (__instance.visible)
            {
                var __Game_Prefabs_RouteModifierData_RO_BufferLookup = typeHandle.GetMemberValue<BufferLookup<RouteModifierData>>("__Game_Prefabs_RouteModifierData_RO_BufferLookup");
                __Game_Prefabs_RouteModifierData_RO_BufferLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Routes_RouteModifier_RO_BufferLookup = typeHandle.GetMemberValue<BufferLookup<RouteModifier>>("__Game_Routes_RouteModifier_RO_BufferLookup");
                __Game_Routes_RouteModifier_RO_BufferLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Routes_RouteSegment_RO_BufferLookup = typeHandle.GetMemberValue<BufferLookup<RouteSegment>>("__Game_Routes_RouteSegment_RO_BufferLookup");
                __Game_Routes_RouteSegment_RO_BufferLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Routes_RouteWaypoint_RO_BufferLookup = typeHandle.GetMemberValue<BufferLookup<RouteWaypoint>>("__Game_Routes_RouteWaypoint_RO_BufferLookup");
                __Game_Routes_RouteWaypoint_RO_BufferLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Routes_RouteVehicle_RO_BufferLookup = typeHandle.GetMemberValue<BufferLookup<RouteVehicle>>("__Game_Routes_RouteVehicle_RO_BufferLookup");
                __Game_Routes_RouteVehicle_RO_BufferLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Pathfind_PathInformation_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<PathInformation>>("__Game_Pathfind_PathInformation_RO_ComponentLookup");
                __Game_Pathfind_PathInformation_RO_ComponentLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Routes_VehicleTiming_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<VehicleTiming>>("__Game_Routes_VehicleTiming_RO_ComponentLookup");
                __Game_Routes_VehicleTiming_RO_ComponentLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Prefabs_PolicySliderData_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<PolicySliderData>>("__Game_Prefabs_PolicySliderData_RO_ComponentLookup");
                __Game_Prefabs_PolicySliderData_RO_ComponentLookup.Update(ref __instance.CheckedStateRef);

                var __Game_Prefabs_TransportLineData_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<TransportLineData>>("__Game_Prefabs_TransportLineData_RO_ComponentLookup");
                __Game_Prefabs_TransportLineData_RO_ComponentLookup.Update(ref __instance.CheckedStateRef);

                CalculateVehicleCountJob jobData = new()
                {
                    m_SelectedEntity = __instance.GetMemberValue<Entity>("selectedEntity"),
                    m_SelectedPrefab = __instance.GetMemberValue<Entity>("selectedPrefab"),
                    m_VehicleCountPolicy = __instance.GetMemberValue<Entity>("m_VehicleCountPolicy"),
                    m_TransportLineDatas = __Game_Prefabs_TransportLineData_RO_ComponentLookup,
                    m_PolicySliderDatas = __Game_Prefabs_PolicySliderData_RO_ComponentLookup,
                    m_VehicleTimings = __Game_Routes_VehicleTiming_RO_ComponentLookup,
                    m_PathInformations = __Game_Pathfind_PathInformation_RO_ComponentLookup,
                    m_RouteVehicles = __Game_Routes_RouteVehicle_RO_BufferLookup,
                    m_RouteWaypoints = __Game_Routes_RouteWaypoint_RO_BufferLookup,
                    m_RouteSegments = __Game_Routes_RouteSegment_RO_BufferLookup,
                    m_RouteModifiers = __Game_Routes_RouteModifier_RO_BufferLookup,
                    m_RouteModifierDatas = __Game_Prefabs_RouteModifierData_RO_BufferLookup,
                    m_IntResults = __instance.GetMemberValue<NativeArray<int>>("m_IntResults"),
                    m_CountResults = __instance.GetMemberValue<NativeList<float2>>("m_CountResult")
                };
                IJobExtensions.Schedule(jobData, __instance.GetMemberValue<JobHandle>("Dependency")).Complete();
            }
            return false;
        }
    }
}