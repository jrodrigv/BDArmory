//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18449
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BahaTurret
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class BDATargetManager : MonoBehaviour
	{
		public static Dictionary<BDArmorySettings.BDATeams, List<TargetInfo>> TargetDatabase;

		public static Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>> GPSTargets;

		public static List<ModuleTargetingCamera> ActiveLasers;

		public static List<MissileLauncher> FiredMissiles;



		string debugString = string.Empty;

		public static float heatScore = 0;
		public static float flareScore = 0;

		public static bool hasAddedButton = false;

		void Start()
		{
			//legacy targetDatabase
			TargetDatabase = new Dictionary<BDArmorySettings.BDATeams, List<TargetInfo>>();
			TargetDatabase.Add(BDArmorySettings.BDATeams.A, new List<TargetInfo>());
			TargetDatabase.Add(BDArmorySettings.BDATeams.B, new List<TargetInfo>());
			StartCoroutine(CleanDatabaseRoutine());

			GPSTargets = new Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>>();
			GPSTargets.Add(BDArmorySettings.BDATeams.A, new List<GPSTargetInfo>());
			GPSTargets.Add(BDArmorySettings.BDATeams.B, new List<GPSTargetInfo>());

			//Laser points
			ActiveLasers = new List<ModuleTargetingCamera>();

			FiredMissiles = new List<MissileLauncher>();

			AddToolbarButton();
		}

		void AddToolbarButton()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(!hasAddedButton)
				{
					Texture buttonTexture = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "icon", false);
					ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
					hasAddedButton = true;
				}
			}
		}
		public void ShowToolbarGUI()
		{
			BDArmorySettings.toolbarGuiEnabled = true;	
		}

		public void HideToolbarGUI()
		{
			BDArmorySettings.toolbarGuiEnabled = false;	
		}
		void Dummy()
		{}

		void Update()
		{
			if(BDArmorySettings.DRAW_DEBUG_LABELS)
			{
				UpdateDebugLabels();
			}
		}


		//Laser point stuff
		public static void RegisterLaserPoint(ModuleTargetingCamera cam)
		{
			if(ActiveLasers.Contains(cam))
			{
				return;
			}
			else
			{
				ActiveLasers.Add(cam);
			}
		}

		/// <summary>
		/// Gets the laser target painter with the least angle off boresight. Set the missile as the reference transform.
		/// </summary>
		/// <returns>The laser target painter.</returns>
		/// <param name="referenceTransform">Reference transform.</param>
		/// <param name="maxBoreSight">Max bore sight.</param>
		public static ModuleTargetingCamera GetLaserTarget(MissileLauncher ml)
		{
			Transform referenceTransform = ml.transform;
			float maxOffBoresight = ml.maxOffBoresight;
			ModuleTargetingCamera finalCam = null;
			float smallestAngle = 360;
			foreach(var cam in ActiveLasers)
			{
				if(!cam)
				{
					continue;
				}

				if(cam.cameraEnabled && cam.groundStabilized && cam.surfaceDetected && !cam.gimbalLimitReached)
				{
					float angle = Vector3.Angle(referenceTransform.forward, cam.groundTargetPosition-referenceTransform.position);
					if(angle < maxOffBoresight && angle < smallestAngle && ml.CanSeePosition(cam.groundTargetPosition))
					{
						smallestAngle = angle;
						finalCam = cam;
					}
				}
			}
			return finalCam;
		}

		public static TargetSignatureData GetHeatTarget(Ray ray, float scanRadius, float highpassThreshold, bool allAspect)
		{
			float minScore = highpassThreshold;
			float minMass = 0.5f;
			TargetSignatureData finalData = TargetSignatureData.noTarget;
			float finalScore = 0;
			foreach(var vessel in FlightGlobals.Vessels)
			{
				if(!vessel || !vessel.loaded)
				{
					continue;
				}
				if(vessel.GetTotalMass() < minMass)
				{
					continue;
				}

				if(RadarUtils.TerrainCheck(ray.origin, vessel.transform.position))
				{
					continue;
				}

				float angle = Vector3.Angle(vessel.CoM-ray.origin, ray.direction);
				if(angle < scanRadius)
				{
					float score = 0;
					foreach(var part in vessel.Parts)
					{
						if(!part) continue;
						if(!allAspect)
						{
							if(!Misc.CheckSightLine(ray.origin, part.transform.position, 10000, 5, 5)) continue;
						}

						float thisScore = (float)(part.thermalInternalFluxPrevious+part.skinTemperature) * Mathf.Clamp01(15/angle);
						thisScore *= Mathf.Pow(1400,2)/Mathf.Clamp((vessel.CoM-ray.origin).sqrMagnitude, 90000, 36000000);
						score = Mathf.Max (score, thisScore);
					}

					if(vessel.LandedOrSplashed)
					{
						score /= 4;
					}

					score *= Mathf.Clamp(Vector3.Angle(vessel.transform.position-ray.origin, -VectorUtils.GetUpDirection(ray.origin))/90, 0.5f, 1.5f);

					if(score > finalScore)
					{
						finalScore = score;
						finalData = new TargetSignatureData(vessel, score);
					}
				}
			}

			heatScore = finalScore;//DEBUG
			flareScore = 0; //DEBUG
			foreach(var flare in BDArmorySettings.Flares)
			{
				if(!flare) continue;

				float angle = Vector3.Angle(flare.transform.position-ray.origin, ray.direction);
				if(angle < scanRadius)
				{
					float score = flare.thermal * Mathf.Clamp01(15/angle);
					score *= Mathf.Pow(1400,2)/Mathf.Clamp((flare.transform.position-ray.origin).sqrMagnitude, 90000, 36000000);

					score *= Mathf.Clamp(Vector3.Angle(flare.transform.position-ray.origin, -VectorUtils.GetUpDirection(ray.origin))/90, 0.5f, 1.5f);

					if(score > finalScore)
					{
						flareScore = score;//DEBUG
						finalScore = score;
						finalData = new TargetSignatureData(flare, score);
					}
				}
			}



			if(finalScore < minScore)
			{
				finalData = TargetSignatureData.noTarget;
			}

			return finalData;
		}








		void UpdateDebugLabels()
		{
			debugString = string.Empty;
			debugString+= ("Team A's targets:");
			foreach(var targetInfo in TargetDatabase[BDArmorySettings.BDATeams.A])
			{
				if(targetInfo)
				{
					if(!targetInfo.Vessel)
					{
						debugString+= ("\n - A target with no vessel reference.");
					}
					else
					{
						debugString+= ("\n - "+targetInfo.Vessel.vesselName+", Engaged by "+targetInfo.numFriendliesEngaging);
					}
				}
				else
				{
					debugString+= ("\n - A null target info.");
				}
			}
			debugString+= ("\nTeam B's targets:");
			foreach(var targetInfo in TargetDatabase[BDArmorySettings.BDATeams.B])
			{
				if(targetInfo)
				{
					if(!targetInfo.Vessel)
					{
						debugString+= ("\n - A target with no vessel reference.");
					}
					else
					{
						debugString+= ("\n - "+targetInfo.Vessel.vesselName+", Engaged by "+targetInfo.numFriendliesEngaging);
					}
				}
				else
				{
					debugString+= ("\n - A null target info.");
				}
			}

			debugString += "\n\nHeat score: "+heatScore;
			debugString += "\nFlare score: "+flareScore;
		}




		//gps stuff
		void SaveGPSTargets()
		{
			
		}

		void LoadGPSTargets()
		{

		}






		//Legacy target managing stuff

		public static BDArmorySettings.BDATeams BoolToTeam(bool team)
		{
			return team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
		}

		public static BDArmorySettings.BDATeams OtherTeam(BDArmorySettings.BDATeams team)
		{
			return team == BDArmorySettings.BDATeams.A ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
		}

		IEnumerator CleanDatabaseRoutine()
		{
			while(enabled)
			{
				yield return new WaitForSeconds(5);
			
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => target == null);
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => target.team == BDArmorySettings.BDATeams.A);
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => !target.isThreat);

				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => target == null);
				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => target.team == BDArmorySettings.BDATeams.B);
				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => !target.isThreat);
			}
		}

		void RemoveTarget(TargetInfo target, BDArmorySettings.BDATeams team)
		{
			TargetDatabase[team].Remove(target);
		}

		public static void ReportVessel(Vessel v, MissileFire reporter)
		{
			if(!v) return;
			if(!reporter) return;


			TargetInfo info = v.gameObject.GetComponent<TargetInfo>();
			if(!info)
			{
				foreach(var mf in v.FindPartModulesImplementing<MissileFire>())
				{
					if(mf.team != reporter.team)
					{
						info = v.gameObject.AddComponent<TargetInfo>();
					}
					return;
				}

				foreach(var ml in v.FindPartModulesImplementing<MissileLauncher>())
				{
					if(ml.hasFired)
					{
						if(ml.team != reporter.team)
						{
							info = v.gameObject.AddComponent<TargetInfo>();
						}
					}

					return;
				}
			}
		}

		public static TargetInfo GetAirToAirTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && !target.isLanded && !target.isMissile)
				{
					if(finalTarget == null || target.numFriendliesEngaging < finalTarget.numFriendliesEngaging)
					{
						finalTarget = target;
					}
				}
			}

			return finalTarget;
		}
		 

		public static TargetInfo GetClosestTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !target.isMissile)
				{
					if(finalTarget == null || (target.IsCloser(finalTarget, mf)))
					{
						finalTarget = target;
					}
				}
			}

			return finalTarget;
		}

		public static List<TargetInfo> GetAllTargetsExcluding(List<TargetInfo> excluding, MissileFire mf)
		{
			List<TargetInfo> finalTargets = new List<TargetInfo>();
			BDArmorySettings.BDATeams team = BoolToTeam(mf.team);

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !excluding.Contains(target))
				{
					finalTargets.Add(target);
				}
			}

			return finalTargets;
		}

		public static TargetInfo GetLeastEngagedTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;
			
			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !target.isMissile)
				{
					if(finalTarget == null || target.numFriendliesEngaging < finalTarget.numFriendliesEngaging)
					{
						finalTarget = target;
					}
				}
			}
		
			return finalTarget;
		}

		public static TargetInfo GetMissileTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && target.isMissile)
				{
					bool isHostile = false;
					if(target.missileModule && target.missileModule.targetMf && target.missileModule.targetMf.team == mf.team)
					{
						isHostile = true;
					}

					if(isHostile && ((finalTarget == null && target.numFriendliesEngaging < 2) || target.numFriendliesEngaging < finalTarget.numFriendliesEngaging))
					{
						finalTarget = target;
					}
				}
			}
			
			return finalTarget;
		}

		public static TargetInfo GetUnengagedMissileTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && target.isMissile)
				{
					bool isHostile = false;
					if(target.missileModule && target.missileModule.targetMf && target.missileModule.targetMf.team == mf.team)
					{
						isHostile = true;
					}
					
					if(isHostile && target.numFriendliesEngaging == 0)
					{
						return target;
					}
				}
			}
			
			return null;
		}

		public static TargetInfo GetClosestMissileTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = BoolToTeam(mf.team);
			TargetInfo finalTarget = null;
			
			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && target.isMissile)
				{
					bool isHostile = false;
					if(target.missileModule && target.missileModule.targetMf && target.missileModule.targetMf.team == mf.team)
					{
						isHostile = true;
					}

					if(isHostile && (finalTarget == null || target.IsCloser(finalTarget, mf)))
					{
						finalTarget = target;
					}
				}
			}
			
			return finalTarget;
		}



		void OnGUI()
		{
			if(BDArmorySettings.DRAW_DEBUG_LABELS)	
			{
				GUI.Label(new Rect(600,100,600,600), debugString);	
			}
		}
	}
}
