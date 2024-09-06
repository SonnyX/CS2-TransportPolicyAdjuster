using Colossal.Logging;
using Colossal.UI.Binding;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.UI.InGame;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TransportPolicyAdjuster
{
    [HarmonyPatch]
    public class VehicleCountSection
    {
        public struct CalculateVehicleCountJob : IJob
        {
            [ReadOnly]
            public Entity m_SelectedEntity;

            [ReadOnly]
            public Entity m_SelectedPrefab;

            [ReadOnly]
            public Entity m_Policy;

            [ReadOnly]
            public int m_MaxVehicleCount;

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

            public NativeReference<float> m_Duration;

            public void Execute()
            {
                TransportLineData transportLineData = m_TransportLineDatas[m_SelectedPrefab];
                DynamicBuffer<RouteVehicle> activeVehicles = m_RouteVehicles[m_SelectedEntity];
                DynamicBuffer<RouteModifier> modifiers = m_RouteModifiers[m_SelectedEntity];
                DynamicBuffer<RouteModifierData> routeModifiers = m_RouteModifierDatas[m_Policy];
                PolicySliderData sliderData = m_PolicySliderDatas[m_Policy];

                float defaultVehicleInterval = transportLineData.m_DefaultVehicleInterval;
                float vehicleInterval = defaultVehicleInterval;

                RouteUtils.ApplyModifier(ref vehicleInterval, modifiers, RouteModifierType.VehicleInterval);

                float lineDuration = CalculateStableDuration(transportLineData);
                m_Duration.Value = lineDuration;

                // Default vehicle count
                m_IntResults[0] = TransportLineSystem.CalculateVehicleCount(vehicleInterval, lineDuration);
                // active vehicles
                m_IntResults[1] = activeVehicles.Length;
                // vehicleCountMin
                m_IntResults[2] = 1;
                // vehicleCountMax
                m_IntResults[3] = m_MaxVehicleCount;
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
            var logger = LogManager.GetLogger(nameof(TransportPolicyAdjuster)).SetShowsErrorsInUI(true);
            logger.Info("Reversed patch: Visible in VehicleCountSection");
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnSetVehicleCount))]
        [HarmonyPrefix]
        public static bool OnSetVehicleCount(float newVehicleCount, ref Game.UI.InGame.VehicleCountSection __instance)
        {
            try
            {
                var m_PoliciesUISystem = __instance.GetMemberValue<PoliciesUISystem>("m_PoliciesUISystem");
                var selectedEntity = __instance.GetMemberValue<Entity>("selectedEntity");
                var m_VehicleCountPolicy = __instance.GetMemberValue<Entity>("m_VehicleCountPolicy");

                var stableDuration = __instance.GetMemberValue<float>("stableDuration");

                var typeHandle = __instance.GetMemberValue<object>("__TypeHandle");
                var __Game_Prefabs_TransportLineData_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<TransportLineData>>("__Game_Prefabs_TransportLineData_RO_ComponentLookup");
                var defaultVehicleInterval = __Game_Prefabs_TransportLineData_RO_ComponentLookup[__instance.GetMemberValue<Entity>("selectedPrefab")].m_DefaultVehicleInterval;

                Mod.log.Info($"newVehicleCount: {newVehicleCount}, stableDuration: {stableDuration}, defaultVehicleInterval: {defaultVehicleInterval}");

                float vehicleInterval = 100f / (stableDuration / (defaultVehicleInterval * newVehicleCount));
                m_PoliciesUISystem.SetPolicy(selectedEntity, m_VehicleCountPolicy, active: true, vehicleInterval);
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetLogger(nameof(TransportPolicyAdjuster)).SetShowsErrorsInUI(true);
                logger.Critical(ex, $"Something went wrong in the OnSetVehicleCount of VehicleCountSection");
            }
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnWriteProperties))]
        [HarmonyPrefix]
        public static bool OnWriteProperties(ref IJsonWriter writer, ref Game.UI.InGame.VehicleCountSection __instance)
        {
            try
            {
                var typeHandle = __instance.GetMemberValue<object>("__TypeHandle");
                var __Game_Prefabs_TransportLineData_RO_ComponentLookup = typeHandle.GetMemberValue<ComponentLookup<TransportLineData>>("__Game_Prefabs_TransportLineData_RO_ComponentLookup");
                __Game_Prefabs_TransportLineData_RO_ComponentLookup.Update(ref __instance.CheckedStateRef);
                var maxVehicleCount = Mod.m_Setting.GetMaximumCount(__Game_Prefabs_TransportLineData_RO_ComponentLookup[__instance.GetMemberValue<Entity>("selectedPrefab")].m_TransportType);

                writer.PropertyName("vehicleCountMin");
                writer.Write(1);
                writer.PropertyName("vehicleCountMax");
                writer.Write(maxVehicleCount);
                writer.PropertyName("vehicleCount");
                writer.Write(__instance.GetMemberValue<int>("vehicleCount"));
                writer.PropertyName("activeVehicles");
                writer.Write(__instance.GetMemberValue<int>("activeVehicles"));
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetLogger(nameof(TransportPolicyAdjuster)).SetShowsErrorsInUI(true);
                logger.Critical(ex, $"Something went wrong in the OnWriteProperties of VehicleCountSection");
            }
            return false;
        }

        [HarmonyPatch(typeof(Game.UI.InGame.VehicleCountSection), nameof(OnUpdate))]
        [HarmonyPrefix]
        public static bool OnUpdate(ref Game.UI.InGame.VehicleCountSection __instance)
        {
            try
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
                        m_Policy = __instance.GetMemberValue<Entity>("m_VehicleCountPolicy"),
                        m_MaxVehicleCount = Mod.m_Setting.GetMaximumCount(__Game_Prefabs_TransportLineData_RO_ComponentLookup[__instance.GetMemberValue<Entity>("selectedPrefab")].m_TransportType),
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
                        m_Duration = __instance.GetMemberValue<NativeReference<float>>("m_DurationResult"),
                    };
                    IJobExtensions.Schedule(jobData, __instance.GetMemberValue<JobHandle>("Dependency")).Complete();
                }
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetLogger(nameof(TransportPolicyAdjuster)).SetShowsErrorsInUI(true);
                logger.Critical(ex, $"Something went wrong in the OnUpdate of VehicleCountSection");
            }
            return false;
        }
    }
}