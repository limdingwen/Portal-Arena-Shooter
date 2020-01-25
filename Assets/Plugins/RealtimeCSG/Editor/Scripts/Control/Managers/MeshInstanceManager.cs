﻿//#define SHOW_GENERATED_MESHES
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using RealtimeCSG;
using RealtimeCSG.Foundation;
using RealtimeCSG.Components;
using UnityEditor.SceneManagement;

namespace InternalRealtimeCSG
{
	internal sealed partial class MeshInstanceManager
	{
#if SHOW_GENERATED_MESHES
		public const HideFlags ComponentHideFlags = HideFlags.DontSaveInBuild;
#else
		public const HideFlags ComponentHideFlags = HideFlags.None
													//| HideFlags.NotEditable       // when this is put into a prefab (when making a prefab containing a model for instance) this will make it impossible to delete 
													//| HideFlags.HideInInspector   // apparently, when set, can cause issues with selection in sceneview 
													| HideFlags.HideInHierarchy
													| HideFlags.DontSaveInBuild
			;
#endif

		internal const string MeshContainerName			= "[generated-meshes]";
		private const string RenderMeshInstanceName		= "[generated-render-mesh]";
		private const string ColliderMeshInstanceName	= "[generated-collider-mesh]";
		private const string HelperMeshInstanceName		= "[generated-helper-mesh]";

		public static void Shutdown()
		{
		}

		public static void OnDestroyed(GeneratedMeshes container)
		{
		}

		public static void Destroy(GeneratedMeshes generatedMeshes)
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			if (!generatedMeshes)
				return;

			if (generatedMeshes.owner)
				generatedMeshes.owner.generatedMeshes = null;

			generatedMeshes.owner = null;
			Destroy(generatedMeshes.gameObject);

			// Undo.DestroyObjectImmediate // NOTE: why was this used before?
			// can't use Undo variant here because it'll mark scenes as dirty on load ..
		}

		public static void Destroy(GameObject gameObject)
		{
			if (!gameObject)
				return;

			// Cannot destroy gameObjects when certain hideflags are set
			if (!TryDestroy(gameObject))
			{
				// Work-around for nested prefab instance issues ..
				if (gameObject.activeSelf)
				{
					gameObject.hideFlags = HideFlags.DontSaveInBuild;
					gameObject.SetActive(false);
					SanitizeGameObject(gameObject);
					gameObject.name = "<unable to delete>";
				}
			}
		}

		// Sometimes we're not allowed to destroy objects, so we try to destroy it and return false if we failed
		static bool TryDestroy(GameObject gameObject)
		{
			if (!gameObject)
				return false;

			var prevHideFlags = gameObject.hideFlags;

			// Cannot destroy gameObjects when certain hideflags are set
			gameObject.hideFlags = HideFlags.None;
			try
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
				return true;
			}
			catch
			{
				gameObject.hideFlags = prevHideFlags;
				return false;
			}
		}

		static void SanitizeGameObject(GameObject gameObject)
		{
			{
				var childComponents = gameObject.GetComponentsInChildren<MonoBehaviour>();
				foreach (var component in childComponents)
				{
					try { UnityEngine.Object.DestroyImmediate(component); } catch { }
				}
			}
			{
				var childComponents = gameObject.GetComponentsInChildren<Component>();
				foreach (var component in childComponents)
				{
					if (!(component is Transform))
					{
						try { UnityEngine.Object.DestroyImmediate(component); } catch { }
					}
				}
			}
			{
				var transform = gameObject.transform;
				foreach (Transform childTransform in transform)
				{
					Destroy(childTransform.gameObject);
				}
			}
		}

		public static void OnCreated(GeneratedMeshes container)
		{
			ValidateGeneratedMeshesDelayed(container);
		}

		public static void OnEnable(GeneratedMeshes container)
		{
			if (container)
				container.gameObject.SetActive(true);
		}

		public static void OnDisable(GeneratedMeshes container)
		{
			if (container)
				container.gameObject.SetActive(false);
		}
		
		public static void OnCreated(GeneratedMeshInstance meshInstance)
		{
			var parent = meshInstance.transform.parent;
			GeneratedMeshes container = null;
			if (parent)
				container = parent.GetComponent<GeneratedMeshes>();
			if (!container)
			{
				Destroy(meshInstance.gameObject);
				return;
			}

			//EnsureValidHelperMaterials(container.owner, meshInstance);

			Initialize(container, meshInstance);

			var key = meshInstance.GenerateKey();
			if (container.meshInstanceLookup[key] != meshInstance)
			{
				if (meshInstance && meshInstance.gameObject)
				{
					Destroy(meshInstance.gameObject);
				}
			}
		}


		static void Initialize(GeneratedMeshes container, GeneratedMeshInstance meshInstance)
		{
			var key = meshInstance.GenerateKey();
			container.meshInstanceLookup[key] = meshInstance;
		}

		static void Initialize(GeneratedMeshes container, HelperSurfaceDescription helperSurface)
		{
			var key = helperSurface.GenerateKey();
			container.helperSurfaceLookup[key] = helperSurface;
		}



		private readonly static HashSet<GeneratedMeshes> validateGeneratedMeshes = new HashSet<GeneratedMeshes>();
		private readonly static HashSet<CSGModel> validateModelWithChildren = new HashSet<CSGModel>();
		private readonly static HashSet<CSGModel> validateModelWithoutChildren = new HashSet<CSGModel>();

		public static void Update()
		{
			var models = CSGModelManager.GetAllModel();
			for (var i = 0; i < models.Length; i++)
			{
				var model = models[i];
				if (model)
					MeshInstanceManager.ValidateModelDelayed(model);
			}

			ignoreValidateModelDelayed = true;
			try
			{
				if (validateModelWithChildren.Count > 0)
				{
					foreach (var model in validateModelWithChildren)
						ValidateModelNow(model, checkChildren: true);
					validateModelWithChildren.Clear();
				}
				if (validateModelWithoutChildren.Count > 0)
				{
					foreach (var model in validateModelWithoutChildren)
						ValidateModelNow(model, checkChildren: false);
					validateModelWithoutChildren.Clear();
				}
			}
			finally
			{
				ignoreValidateModelDelayed = false;
			}

			for (var i = 0; i < models.Length; i++)
			{
				var model = models[i];
				if (!model)
					continue;

				var meshContainer = model.generatedMeshes;
				if (!meshContainer)
					continue;

				if (meshContainer.meshInstanceLookup == null ||
					meshContainer.meshInstanceLookup.Count == 0)
				{
					MeshInstanceManager.ValidateGeneratedMeshesDelayed(meshContainer);
					continue;
				}
			}

			if (validateGeneratedMeshes.Count > 0)
			{
				foreach (var generatedMeshes in validateGeneratedMeshes)
					ValidateGeneratedMeshesNow(generatedMeshes);
				validateGeneratedMeshes.Clear();
			}

			
			MeshInstanceManager.UpdateTransforms();
		}

		public static void ValidateGeneratedMeshesDelayed(GeneratedMeshes meshContainer)
		{
			validateGeneratedMeshes.Add(meshContainer);
		}


		static bool ignoreValidateModelDelayed = false;
		internal static void ValidateModelDelayed(CSGModel model, bool checkChildren = false)
		{
			if (ignoreValidateModelDelayed)
				return;
			if (checkChildren)
			{
				validateModelWithChildren.Add(model);
				validateModelWithoutChildren.Remove(model);
			} else
			{
				if (!validateModelWithChildren.Contains(model))
					validateModelWithoutChildren.Add(model);
			}
		}

		static void UpdateGeneratedMeshesFlags(CSGModel model, GeneratedMeshes generatedMeshes)
		{
			if (!generatedMeshes)
				return;
			
			if (generatedMeshes.enabled == false)
				generatedMeshes.enabled = true;
			var generatedMeshesGameObject = generatedMeshes.gameObject;
			var activated = (model.enabled && model.gameObject.activeSelf);
			if (generatedMeshesGameObject.activeSelf != activated)
				generatedMeshesGameObject.SetActive(activated);
		}

		static GeneratedMeshes EnsureOneValidGeneratedMeshesComponent(CSGModel model)
		{
			// Find all the GeneratedMeshes inside this model
			var foundGeneratedMeshes = model.GetComponentsInChildren<GeneratedMeshes>(true);
			// If we have CSGModel components inside this CSGModel component, we ignore all the GeneratedMeshes inside those ..
			for (var i = foundGeneratedMeshes.Length - 1; i >= 0; i--)
			{
				var parentModel = foundGeneratedMeshes[i].GetComponentInParent<CSGModel>();
				if (parentModel != model)
				{
					// TODO: should just swap with last element + keep track of our own count in array and use that below
					ArrayUtility.RemoveAt(ref foundGeneratedMeshes, i);
				}
			}


			if (foundGeneratedMeshes.Length > 1)
			{
				var prevGeneratedMeshes = model.generatedMeshes;
				GeneratedMeshes newGeneratedMeshes = null;

				// Check if we have more than one GeneratedMeshes component, this can happen, for instance, 
				//	due to prefab merging issues or if the user duplicates the gameObject

				for (var i = foundGeneratedMeshes.Length - 1; i >= 0; i--)
				{
					var generatedMeshesComponent = foundGeneratedMeshes[i];
					if (!generatedMeshesComponent)
						continue;

					var generatedMeshesGameObject = generatedMeshesComponent.gameObject;
					if (!newGeneratedMeshes ||
						// Prefer to keep the GeneratedMesh that is currently used by the Model
						generatedMeshesComponent == prevGeneratedMeshes)
					{
						// if the we already found a GeneratedMesh, and it's valid, try to see if we can destroy it
						if (newGeneratedMeshes)
						{
							var newGeneratedMeshesGameObject = newGeneratedMeshes.gameObject;
							if (TryDestroy(newGeneratedMeshesGameObject))
							{
								newGeneratedMeshes = generatedMeshesComponent;
								continue;
							}
							// Fall through, we need to destroy this component after all
						} else
						{
							newGeneratedMeshes = generatedMeshesComponent;
							continue;
						}
					}

					// Try to destroy the GeneratedMeshes gameObject
					if (!TryDestroy(generatedMeshesGameObject))
					{
						// If the we already found a GeneratedMesh, and it's valid, try to see if we can destroy that instead
						var newGeneratedMeshesGameObject = newGeneratedMeshes.gameObject;
						if (!TryDestroy(newGeneratedMeshesGameObject))
						{
							// Fall back to disabling the component instead
							Destroy(newGeneratedMeshesGameObject);
						} 
						prevGeneratedMeshes = null;
						newGeneratedMeshes = generatedMeshesComponent;
					}
				}

				model.generatedMeshes = newGeneratedMeshes;
				if (model.generatedMeshes)
				{
					model.generatedMeshes.owner = model;
					UpdateGeneratedMeshesFlags(model, model.generatedMeshes);
					ValidateGeneratedMeshesNow(model.generatedMeshes, skipSiblingCheck: true);
					return model.generatedMeshes;
				}

				// Fall through, create a new GeneratedMeshes component
			} else
			if (foundGeneratedMeshes.Length == 1)
			{
				model.generatedMeshes = foundGeneratedMeshes[0];
				model.generatedMeshes.owner = model;
				UpdateGeneratedMeshesFlags(model, model.generatedMeshes);
				ValidateGeneratedMeshesNow(model.generatedMeshes, skipSiblingCheck: true);
				return model.generatedMeshes;
			}


			// Create it if it doesn't exist
			var generatedMeshesObject = new GameObject(MeshContainerName);
			generatedMeshesObject.SetActive(false);

			var generatedMeshes = generatedMeshesObject.AddComponent<GeneratedMeshes>();
			generatedMeshes.owner = model;

			var generatedMeshesTransform = generatedMeshesObject.transform;
			generatedMeshesTransform.SetParent(model.transform, false);

			generatedMeshesObject.SetActive(true);

			UpdateGeneratedMeshesVisibility(generatedMeshes, model.ShowGeneratedMeshes);
			UpdateGeneratedMeshesFlags(model, generatedMeshes);
			model.generatedMeshes = generatedMeshes;
			model.generatedMeshes.owner = model;
			return generatedMeshes;
		}

		internal static bool ValidateModelNow(CSGModel model, bool checkChildren = false)
		{
			if (!model)
				return false;

			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return true;

			if (!checkChildren && 
				model.generatedMeshes && 
				model.generatedMeshes.owner == model)
				return true;

			EnsureOneValidGeneratedMeshesComponent(model);

			model.forceUpdate = true;
			return true;
		}

		private static bool ShouldRenderHelperSurface(RenderSurfaceType renderSurfaceType)
		{
			switch (renderSurfaceType)
			{
				case RenderSurfaceType.Hidden:			return CSGSettings.ShowHiddenSurfaces;
				case RenderSurfaceType.Culled:			return CSGSettings.ShowCulledSurfaces;
				case RenderSurfaceType.Collider:		return CSGSettings.ShowColliderSurfaces;
				case RenderSurfaceType.Trigger:			return CSGSettings.ShowTriggerSurfaces;
				case RenderSurfaceType.ShadowOnly:		
				case RenderSurfaceType.CastShadows:		return CSGSettings.ShowCastShadowsSurfaces;
				case RenderSurfaceType.ReceiveShadows:	return CSGSettings.ShowReceiveShadowsSurfaces;
			}
			return false;
		}

		/*
		private static RenderSurfaceType EnsureValidHelperMaterials(CSGModel model, GeneratedMeshInstance meshInstance)
		{
			var surfaceType = !meshInstance.RenderMaterial ? meshInstance.RenderSurfaceType : 
								GetRenderSurfaceType(model, meshInstance);
			if (surfaceType != RenderSurfaceType.Normal)
				meshInstance.RenderMaterial = MaterialUtility.GetSurfaceMaterial(surfaceType);
			return surfaceType;
		}
		*/

		private static bool ValidMeshInstance(GeneratedMeshInstance meshInstance)
		{
			return meshInstance.IsValid();
		}

		public static bool HasVisibleMeshRenderer(GeneratedMeshInstance meshInstance)
		{
			if (!meshInstance)
				return false;
			return meshInstance.RenderSurfaceType == RenderSurfaceType.Normal;
		}

		public static bool HasRuntimeMesh(GeneratedMeshInstance meshInstance)
		{
			if (!meshInstance)
				return false;
			return	meshInstance.RenderSurfaceType != RenderSurfaceType.Culled &&
					meshInstance.RenderSurfaceType != RenderSurfaceType.Hidden;
		}

		public static void RenderHelperSurfaces(SceneView sceneView)
		{
			var allHelperSurfaces = (CSGSettings.VisibleHelperSurfaces & ~HelperSurfaceFlags.ShowVisibleSurfaces);
			if (allHelperSurfaces == (HelperSurfaceFlags)0)
			{
				CSGSettings.VisibleHelperSurfaces = CSGSettings.DefaultHelperSurfaceFlags;
				return;
			}

			var camera			= sceneView.camera;			
			var showWireframe	= RealtimeCSG.CSGSettings.IsWireframeShown(sceneView);
				
			var visibleLayers	= Tools.visibleLayers;
			var models			= InternalCSGModelManager.Models;
			for (var i = 0; i < models.Length; i++)
			{
				var model = models[i];
				if (!model)
					continue;

				if (((1 << model.gameObject.layer) & visibleLayers) == 0)
					continue;

				if (!model.generatedMeshes)
					continue;

				var container = model.generatedMeshes;
				if (!container.owner ||
					!container.owner.isActiveAndEnabled)
				{
					continue;
				}

				var meshInstances	= container.meshInstanceLookup;
				var helperSurfaces	= container.helperSurfaceLookup;
				if ((meshInstances  == null || meshInstances.Count  == 0) &&
					(helperSurfaces == null || helperSurfaces.Count == 0))
				{
					ValidateGeneratedMeshesDelayed(container);
					continue;
				}

				if (helperSurfaces == null ||
					helperSurfaces.Count == 0)
					continue;

				foreach (var meshInstance in meshInstances.Values)
				{
					if (!meshInstance)
					{
						TryDestroy(meshInstance.gameObject);
						return;
					}
				}
				

				//var modelDoesNotRender = !model.IsRenderable;
				
				var matrix = container.transform.localToWorldMatrix;
				//foreach (var meshInstance in meshInstances.Values) { if (!meshInstance) continue;
				foreach (var meshInstance in helperSurfaces.Values)
				{
					var renderSurfaceType = meshInstance.RenderSurfaceType;

					if (!ShouldRenderHelperSurface(renderSurfaceType))
						continue;

					var material = MaterialUtility.GetSurfaceMaterial(renderSurfaceType);
					if (!material)
						continue;

					if (!meshInstance.SharedMesh)
					{
						InternalCSGModelManager.UpdateMesh(meshInstance.GeneratedMeshContents,
														   meshInstance.MeshDescription,
														   meshInstance.RenderSurfaceType,
														   ref meshInstance.HasGeneratedNormals,
														   ref meshInstance.SharedMesh);
						if (!meshInstance.SharedMesh)
							continue;
					}

					if (!meshInstance.HasGeneratedNormals)
					{
						meshInstance.SharedMesh.RecalculateNormals();
						meshInstance.HasGeneratedNormals = true;
					}

					if (!showWireframe)
					{
						// "DrawMeshNow" so that it renders properly in all shading modes
						if (material.SetPass(0))
							Graphics.DrawMeshNow(meshInstance.SharedMesh,
													matrix);
					} else
					{
						Graphics.DrawMesh(meshInstance.SharedMesh,
										  matrix,
										  material,
										  layer: 0,
										  camera: camera,
										  submeshIndex: 0,
										  properties: null,
										  castShadows: false,
										  receiveShadows: false);
					}
				}
			}
		}

		public static UnityEngine.Object[] FindRenderers(CSGModel[] models)
		{
			var renderers = new List<UnityEngine.Object>();
			foreach (var model in models)
			{
				if (!model.generatedMeshes)
					continue;

				foreach (var renderer in model.generatedMeshes.GetComponentsInChildren<MeshRenderer>())
				{
					if (!renderer)
						continue;

					var type = MaterialUtility.GetMaterialSurfaceType(renderer.sharedMaterial);
					if (type == RenderSurfaceType.Normal)
						continue;

					renderers.Add(renderer);
				}
			}
			return renderers.ToArray();
		}

		internal static void Reset()
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
			{
				var scene = SceneManager.GetSceneAt(sceneIndex);
				if (!scene.isLoaded)
					continue;

				var meshInstances = SceneQueryUtility.GetAllComponentsInScene<GeneratedMeshInstance>(scene);
				for (int m = 0; m < meshInstances.Count; m++)
				{
					meshInstances[m].hideFlags = HideFlags.None;
					var gameObject = meshInstances[m].gameObject;
					SanitizeGameObject(gameObject);
					TryDestroy(gameObject);
				}
			}
		}

		public static RenderSurfaceType GetSurfaceType(GeneratedMeshDescription meshDescription, ModelSettingsFlags modelSettings)
		{
			if (meshDescription.meshQuery.LayerQuery == LayerUsageFlags.Culled) return RenderSurfaceType.Culled;
			switch (meshDescription.meshQuery.LayerParameterIndex)
			{
				case LayerParameterIndex.LayerParameter1:
				{
					switch (meshDescription.meshQuery.LayerQuery)
					{
						case LayerUsageFlags.RenderReceiveCastShadows:
						case LayerUsageFlags.RenderReceiveShadows:
						case LayerUsageFlags.RenderCastShadows:
						case LayerUsageFlags.Renderable:										return RenderSurfaceType.Normal;
						case LayerUsageFlags.ReceiveShadows:									return RenderSurfaceType.Hidden;
						case (LayerUsageFlags.CastShadows | LayerUsageFlags.ReceiveShadows):
						case LayerUsageFlags.CastShadows:										return RenderSurfaceType.ShadowOnly; 
						default:
						return RenderSurfaceType.Culled;
					}
				}
				case LayerParameterIndex.LayerParameter2:
				{
					if ((modelSettings & ModelSettingsFlags.IsTrigger) != 0)
					{
						return RenderSurfaceType.Trigger;
					} else
					{
						return RenderSurfaceType.Collider;
					}
				}
				case LayerParameterIndex.None:
				{
					switch (meshDescription.meshQuery.LayerQuery)
					{
						case LayerUsageFlags.None:				return RenderSurfaceType.Hidden;
						case LayerUsageFlags.CastShadows:		return RenderSurfaceType.CastShadows;
						case LayerUsageFlags.ReceiveShadows:	return RenderSurfaceType.ReceiveShadows;
						case LayerUsageFlags.Collidable:
						{
							if ((modelSettings & ModelSettingsFlags.IsTrigger) != 0)
							{
								return RenderSurfaceType.Trigger;
							} else
								return RenderSurfaceType.Collider;
						}
						default:
						case LayerUsageFlags.Culled:
						return RenderSurfaceType.Culled;
					}
				}
			}
			return RenderSurfaceType.Normal;
		}

		public static GeneratedMeshInstance CreateMeshInstance(GeneratedMeshes generatedMeshes, GeneratedMeshDescription meshDescription, ModelSettingsFlags modelSettings, RenderSurfaceType renderSurfaceType)
		{
			if (!generatedMeshes || !generatedMeshes.owner)
				return null;


			var generatedMeshesTransform = generatedMeshes.transform;
			GameObject meshInstanceGameObject = null;
			GeneratedMeshInstance meshInstance = null;

			// prefab bug prevents us to reuse removed gameObjects
			/*
			for (int i = 0; i < generatedMeshesTransform.childCount; i++)
			{
				var childTransform = generatedMeshesTransform.GetChild(i);
				var childGameObject = childTransform.gameObject;
				if (!childGameObject.activeSelf)
					continue;

				childGameObject.hideFlags = HideFlags.None;
				try
				{
					meshInstance = childGameObject.AddComponent<GeneratedMeshInstance>();
					meshInstanceGameObject = childGameObject;
					break;
				}
				catch
				{
				}
			}

			if (!meshInstanceGameObject)
			*/
			{
				meshInstanceGameObject = new GameObject();
				meshInstanceGameObject.SetActive(false);
				meshInstanceGameObject.transform.SetParent(generatedMeshesTransform, false);
				meshInstanceGameObject.transform.localPosition	= MathConstants.zeroVector3;
				meshInstanceGameObject.transform.localRotation	= MathConstants.identityQuaternion;
				meshInstanceGameObject.transform.localScale		= MathConstants.oneVector3;

				meshInstance = meshInstanceGameObject.AddComponent<GeneratedMeshInstance>();
			}



			var containerStaticFlags = GameObjectUtility.GetStaticEditorFlags(generatedMeshes.owner.gameObject);
			GameObjectUtility.SetStaticEditorFlags(meshInstanceGameObject, containerStaticFlags);


			Material renderMaterial = null;
			PhysicMaterial physicsMaterial = null;
			if (meshDescription.surfaceParameter != 0)
			{
				var obj = EditorUtility.InstanceIDToObject(meshDescription.surfaceParameter);
				if (obj)
				{
					switch (meshDescription.meshQuery.LayerParameterIndex)
					{
						case LayerParameterIndex.LayerParameter1:
						{ renderMaterial = obj as Material; break; }
						case LayerParameterIndex.LayerParameter2:
						{ physicsMaterial = obj as PhysicMaterial; break; }
					}
				}
			}

			meshInstance.MeshDescription = meshDescription;

			// Our mesh has not been initialized yet, so make sure we reflect that fact
			meshInstance.MeshDescription.geometryHashValue = 0;
			meshInstance.MeshDescription.surfaceHashValue = 0;
			meshInstance.MeshDescription.vertexCount = 0;
			meshInstance.MeshDescription.indexCount = 0;

			meshInstance.RenderMaterial = renderMaterial;
			meshInstance.PhysicsMaterial = physicsMaterial;
			meshInstance.RenderSurfaceType = renderSurfaceType;

			meshInstanceGameObject.SetActive(true);

			Initialize(generatedMeshes, meshInstance);
			return meshInstance;
		}

		public static HelperSurfaceDescription CreateHelperSurfaceDescription(GeneratedMeshes container, GeneratedMeshDescription meshDescription, ModelSettingsFlags modelSettings, RenderSurfaceType renderSurfaceType)
		{
			var instance = new HelperSurfaceDescription
			{
				RenderSurfaceType = renderSurfaceType,
				MeshDescription   = meshDescription
			};

			// Our mesh has not been initialized yet, so make sure we reflect that fact
			instance.MeshDescription.geometryHashValue	= 0;
			instance.MeshDescription.surfaceHashValue	= 0;
			instance.MeshDescription.vertexCount		= 0;
			instance.MeshDescription.indexCount			= 0;

			Initialize(container, instance);
			return instance;
		}

		internal static void ClearMesh(ref bool hasGeneratedNormals, ref Mesh sharedMesh)
		{
			hasGeneratedNormals = false;
			if (sharedMesh)
			{
				sharedMesh.Clear(keepVertexLayout: true);
				return;
			}
			
			sharedMesh = new Mesh();
			sharedMesh.name = string.Format("<generated {0}>", sharedMesh.GetInstanceID());
			sharedMesh.MarkDynamic();
        }

        public static bool UsesLightmapUVs(CSGModel model)
        {
            var staticFlags = GameObjectUtility.GetStaticEditorFlags(model.gameObject);
#if UNITY_2019_2_OR_NEWER 
            if ((staticFlags & StaticEditorFlags.ContributeGI) != StaticEditorFlags.ContributeGI)
                return false;
#else
            if ((staticFlags & StaticEditorFlags.LightmapStatic) != StaticEditorFlags.LightmapStatic)
                return false;            
#endif
            return true;
        }

        public static bool NeedToGenerateLightmapUVsForModel(CSGModel model)
		{
			if (!model)
				return false;

			if (!model.generatedMeshes)
				return false;

			var container = model.generatedMeshes;
			if (!container || container.owner != model)
				return false;

			if (container.meshInstanceLookup == null)
				return false;

            if (!UsesLightmapUVs(model))
                return false;

            foreach (var pair in container.meshInstanceLookup)
			{
				var instance = pair.Value;
				if (!instance)
					continue;

				if (NeedToGenerateLightmapUVsForInstance(instance))
					return true;
			}
			return false;
		}

		public static void GenerateLightmapUVsForModel(CSGModel model)
		{
			if (!model)
				return;

			if (!model.generatedMeshes)
				return;

			var container = model.generatedMeshes;
			if (!container || !container.owner)
				return;

			if (container.meshInstanceLookup == null)
				return;

			var uvGenerationSettings = new UnityEditor.UnwrapParam
			{
				angleError = Mathf.Clamp(model.angleError, CSGModel.MinAngleError, CSGModel.MaxAngleError),
				areaError  = Mathf.Clamp(model.areaError, CSGModel.MinAreaError, CSGModel.MaxAreaError),
				hardAngle  = model.hardAngle,
				packMargin = model.packMargin
			};

			foreach (var pair in container.meshInstanceLookup)
			{
				var instance = pair.Value;
				if (!instance)
					continue;

				GenerateLightmapUVsForInstance(instance, model, uvGenerationSettings);
			}
		}

		private static void GenerateLightmapUVsForInstance(GeneratedMeshInstance instance, CSGModel model, UnwrapParam param)
		{
			var meshRendererComponent = instance.CachedMeshRenderer;
			if (!meshRendererComponent)
			{
				var gameObject = instance.gameObject;
				meshRendererComponent = gameObject.GetComponent<MeshRenderer>();
				instance.CachedMeshRendererSO = null;
			}

			if (!meshRendererComponent)
				return;

			meshRendererComponent.realtimeLightmapIndex = -1;
			meshRendererComponent.lightmapIndex = -1;
			
			var oldVertices		= instance.SharedMesh.vertices;
			if (oldVertices.Length == 0)
				return;

			var oldUV			= instance.SharedMesh.uv;
			var oldNormals		= instance.SharedMesh.normals;
			var oldTangents		= instance.SharedMesh.tangents;
			var oldColors		= instance.SharedMesh.colors;
			var oldTriangles	= instance.SharedMesh.triangles;
			var oldName			= instance.SharedMesh.name;
			
			var tempMesh = new Mesh
			{
				vertices	= oldVertices,
				normals		= oldNormals,
				uv			= oldUV,
				tangents	= oldTangents,
				colors		= oldColors,
				triangles	= oldTriangles,
				name		= oldName
			};
			tempMesh.bounds = instance.SharedMesh.bounds;
			instance.SharedMesh = tempMesh;
			/*
#if UNITY_2018_3_OR_NEWER
			var modelGameObject = model.gameObject;
			var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(modelGameObject);
			if (prefabStage != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tempMesh)))
			{
				var prefabAssetPath = prefabStage.prefabAssetPath;
				if (!string.IsNullOrEmpty(prefabAssetPath))
					AssetDatabase.AddObjectToAsset(tempMesh, prefabAssetPath);
				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(modelGameObject.scene);
			}
#endif
			*/

			Debug.Log("Generating lightmap UVs (by Unity) for the mesh " + instance.name + " of the Model named \"" + model.name +"\"\n", model);
			//var optimizeTime = EditorApplication.timeSinceStartup;
			//MeshUtility.Optimize(instance.SharedMesh);
			//optimizeTime = EditorApplication.timeSinceStartup - optimizeTime;

			var lightmapGenerationTime = EditorApplication.timeSinceStartup;
            MeshUtility.Optimize(instance.SharedMesh);
            Unwrapping.GenerateSecondaryUVSet(instance.SharedMesh, param);
			lightmapGenerationTime = EditorApplication.timeSinceStartup - lightmapGenerationTime;
			
			Debug.Log(//"\tMesh optimizing in " + (optimizeTime * 1000) + " ms\n"+
					  "\tUV generation in " + (lightmapGenerationTime* 1000) + " ms\n", model);

			EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
			instance.HasUV2 = true;
			instance.LightingHashValue = instance.MeshDescription.geometryHashValue;
		}
		
		private static bool NeedToGenerateLightmapUVsForInstance(GeneratedMeshInstance instance)
		{
			return !instance.HasUV2 && instance.RenderSurfaceType == RenderSurfaceType.Normal;
		}

		private static bool NeedCollider(CSGModel model, GeneratedMeshInstance instance)
		{
			return //((model.HaveCollider || model.IsTrigger) &&

					(//instance.RenderSurfaceType == RenderSurfaceType.Normal ||
						instance.RenderSurfaceType == RenderSurfaceType.Collider ||
						instance.RenderSurfaceType == RenderSurfaceType.Trigger
						) &&

					// Make sure the bounds of the mesh are not empty ...
					(Mathf.Abs(instance.SharedMesh.bounds.size.x) > MathConstants.EqualityEpsilon ||
					 Mathf.Abs(instance.SharedMesh.bounds.size.y) > MathConstants.EqualityEpsilon ||
					 Mathf.Abs(instance.SharedMesh.bounds.size.z) > MathConstants.EqualityEpsilon)
					//)
			;
		}

		static StaticEditorFlags FilterStaticEditorFlags(StaticEditorFlags oldStaticFlags, RenderSurfaceType renderSurfaceType)
		{
			var newStaticFlags = oldStaticFlags;
			var walkable =	renderSurfaceType != RenderSurfaceType.Hidden &&
							renderSurfaceType != RenderSurfaceType.ShadowOnly &&
							renderSurfaceType != RenderSurfaceType.Culled &&
							renderSurfaceType != RenderSurfaceType.Trigger;
			if (walkable)	newStaticFlags = newStaticFlags | StaticEditorFlags.NavigationStatic;
			else			newStaticFlags = newStaticFlags & ~StaticEditorFlags.NavigationStatic;
			
			if (renderSurfaceType != RenderSurfaceType.Normal &&
				renderSurfaceType != RenderSurfaceType.ShadowOnly)
				newStaticFlags = (StaticEditorFlags)0;

            // This fixes a bug in 2018.3 where it tries to generate lightmaps for ShadowOnly surfaces ..
            // .. but then rage quits because it doesn't have any normals
#if UNITY_2019_2_OR_NEWER 
			if (renderSurfaceType == RenderSurfaceType.ShadowOnly)
				newStaticFlags = newStaticFlags & ~(StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
#else
            if (renderSurfaceType == RenderSurfaceType.ShadowOnly)
                newStaticFlags = newStaticFlags & ~(StaticEditorFlags.LightmapStatic | StaticEditorFlags.ReflectionProbeStatic);            
#endif

            return newStaticFlags;
		}

		static string MaterialToString(Material mat)	
		{
			if (ReferenceEquals(mat, null))
				return "null";
			if (!mat)
				return "invalid";
			return mat.name + " " + mat.GetInstanceID().ToString();
		}

		public static void ClearUVs(CSGModel model)
		{
			if (!model.generatedMeshes)
				return;

			var container = model.generatedMeshes;
			if (!container || !container.owner)
				return;

			foreach (var pair in container.meshInstanceLookup)
			{
				var instance = pair.Value;
				if (!instance)
					continue;

				Refresh(instance, model, onlyFastRefreshes: false);
				ClearUVs(instance);
			}
		}

		public static void ClearUVs(GeneratedMeshInstance instance)
		{
			var meshRendererComponent	= instance.CachedMeshRenderer;
			if (meshRendererComponent)
			{
				meshRendererComponent.realtimeLightmapIndex = -1;
				meshRendererComponent.lightmapIndex = -1;
			}
			instance.LightingHashValue = 0;
			instance.HasUV2 = false;
		}

		public static void Refresh(CSGModel model, bool postProcessScene = false, bool onlyFastRefreshes = true)
		{
			if (!model)
				return;

			var generatedMeshes = model.generatedMeshes;
			if (!generatedMeshes || generatedMeshes.owner != model)
				return;

			foreach (var pair in generatedMeshes.meshInstanceLookup)
			{
				var instance = pair.Value;
				if (!instance)
					continue;

				Refresh(instance, model, postProcessScene, onlyFastRefreshes);
			}
		}

		//		internal static double updateMeshColliderMeshTime = 0.0;
		public static void Refresh(GeneratedMeshInstance instance, CSGModel owner, bool postProcessScene = false, bool onlyFastRefreshes = true)
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			if (!instance)
				return;

			if (postProcessScene)
				onlyFastRefreshes = false;

			if (!instance.SharedMesh ||
				instance.SharedMesh.vertexCount == 0)
			{
				instance.Dirty = true;
				if (instance.GeneratedMeshContents != null)
				{
					InternalCSGModelManager.UpdateMesh(instance.GeneratedMeshContents,
													   instance.MeshDescription,
													   instance.RenderSurfaceType,
													   ref instance.HasGeneratedNormals,
													   ref instance.SharedMesh);
					if (!instance.SharedMesh)
						return;
				} else
					return;
			}


			// Update the flags
			var oldRenderSurfaceType	= instance.RenderSurfaceType;
			if (!instance.RenderMaterial)
				instance.RenderMaterial = MaterialUtility.GetSurfaceMaterial(oldRenderSurfaceType);
			instance.RenderSurfaceType	= GetSurfaceType(instance.MeshDescription, owner.Settings);
			instance.Dirty				= instance.Dirty || (oldRenderSurfaceType != instance.RenderSurfaceType);


			// Update the transform, if incorrect
			var gameObject = instance.gameObject;
			if (gameObject.transform.localPosition	!= MathConstants.zeroVector3)			gameObject.transform.localPosition	= MathConstants.zeroVector3;
			if (gameObject.transform.localRotation	!= MathConstants.identityQuaternion)	gameObject.transform.localRotation	= MathConstants.identityQuaternion;
			if (gameObject.transform.localScale		!= MathConstants.oneVector3)			gameObject.transform.localScale		= MathConstants.oneVector3;


#if SHOW_GENERATED_MESHES
			var meshInstanceFlags   = HideFlags.None;
			var transformFlags      = HideFlags.None;
			var gameObjectFlags     = HideFlags.None;
#else
			var meshInstanceFlags   = HideFlags.DontSaveInBuild;// | HideFlags.NotEditable;
			var transformFlags      = HideFlags.HideInInspector;// | HideFlags.NotEditable;
			var gameObjectFlags     = HideFlags.None;
#endif

			if (gameObject.transform.hideFlags	!= transformFlags)		{ gameObject.transform.hideFlags	= transformFlags; }
			if (gameObject.hideFlags			!= gameObjectFlags)		{ gameObject.hideFlags				= gameObjectFlags; }
			if (instance.hideFlags				!= meshInstanceFlags)	{ instance.hideFlags				= meshInstanceFlags; }

			
			var showVisibleSurfaces	=	instance.RenderSurfaceType != RenderSurfaceType.Normal ||
										(RealtimeCSG.CSGSettings.VisibleHelperSurfaces & HelperSurfaceFlags.ShowVisibleSurfaces) != 0;

			if (gameObject.activeSelf != showVisibleSurfaces) gameObject.SetActive(showVisibleSurfaces);
			if (!instance.enabled) instance.enabled = true;
			
			
			// Update navigation on mesh
			var oldStaticFlags = GameObjectUtility.GetStaticEditorFlags(gameObject);
			var newStaticFlags = FilterStaticEditorFlags(GameObjectUtility.GetStaticEditorFlags(owner.gameObject), instance.RenderSurfaceType);
			if (newStaticFlags != oldStaticFlags)
			{
				GameObjectUtility.SetStaticEditorFlags(gameObject, newStaticFlags);
			}

			var meshFilterComponent		= instance.CachedMeshFilter;
			var meshRendererComponent	= instance.CachedMeshRenderer;
			var needMeshCollider		= NeedCollider(owner, instance);
			
			

			var needMeshRenderer		= (instance.RenderSurfaceType == RenderSurfaceType.Normal ||
										   instance.RenderSurfaceType == RenderSurfaceType.ShadowOnly);
			if (needMeshRenderer)
			{
				if (!meshRendererComponent)
				{
					meshRendererComponent = gameObject.GetComponent<MeshRenderer>();
					instance.CachedMeshRendererSO = null;
				}
				if (!meshFilterComponent)
				{
					meshFilterComponent = gameObject.GetComponent<MeshFilter>();
					if (!meshFilterComponent)
					{
						meshFilterComponent = gameObject.AddComponent<MeshFilter>();
						instance.CachedMeshRendererSO = null;
						instance.Dirty = true;
					}
				}

//				var ownerReceiveShadows = owner.ReceiveShadows;
//				var shadowCastingMode	= owner.ShadowCastingModeFlags;
				var ownerReceiveShadows = true;
				var shadowCastingMode	= owner.IsTwoSidedShadows ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.On;
				if (instance.RenderSurfaceType == RenderSurfaceType.ShadowOnly)
				{
					shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
				}

				
				switch (instance.MeshDescription.meshQuery.LayerQuery)
				{
					case LayerUsageFlags.RenderReceiveCastShadows:
					{
						break;
					}
					case LayerUsageFlags.RenderReceiveShadows:
					{
						shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						break;
					} 
					case LayerUsageFlags.RenderCastShadows:
					{
						ownerReceiveShadows = false;
						break;
					}
					case LayerUsageFlags.Renderable:
					{
						shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						ownerReceiveShadows = false;
						break;
					}
					case LayerUsageFlags.CastShadows:
					{
						shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
						ownerReceiveShadows = false;
						break;
					}
				}


				var requiredMaterial = instance.RenderMaterial;
				if (shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
					// Note: need non-transparent material here
					requiredMaterial = MaterialUtility.DefaultMaterial;

				if (!requiredMaterial)
					requiredMaterial = MaterialUtility.MissingMaterial;

				if (!meshRendererComponent)
				{
					meshRendererComponent = gameObject.AddComponent<MeshRenderer>();
					meshRendererComponent.sharedMaterial = requiredMaterial;
					meshRendererComponent.gameObject.name = RenderMeshInstanceName;
					instance.CachedMeshRendererSO = null;
					instance.Dirty = true;
				}

				if ((meshFilterComponent.hideFlags & HideFlags.HideInHierarchy) == 0)
				{
					meshFilterComponent.hideFlags |= HideFlags.HideInHierarchy;
				}

				if ((meshRendererComponent.hideFlags & HideFlags.HideInHierarchy) == 0)
				{
					meshRendererComponent.hideFlags |= HideFlags.HideInHierarchy;
				}
				
				if (instance.RenderSurfaceType != RenderSurfaceType.ShadowOnly)
				{ 
					if (instance.HasUV2 && 
						(instance.LightingHashValue != instance.MeshDescription.geometryHashValue) && meshRendererComponent)
					{
						instance.ResetUVTime = Time.realtimeSinceStartup;
						if (instance.HasUV2)
							ClearUVs(instance);
					}

					if ((owner.AutoRebuildUVs || postProcessScene))
					{
						if ((float.IsPositiveInfinity(instance.ResetUVTime) || ((Time.realtimeSinceStartup - instance.ResetUVTime) > 2.0f)) &&
							NeedToGenerateLightmapUVsForModel(owner))
						{
							GenerateLightmapUVsForModel(owner);
						}
					}
				}

				if (!postProcessScene &&
					meshFilterComponent.sharedMesh != instance.SharedMesh)
					meshFilterComponent.sharedMesh = instance.SharedMesh;

				if (meshRendererComponent &&
					meshRendererComponent.shadowCastingMode != shadowCastingMode)
				{
					meshRendererComponent.shadowCastingMode = shadowCastingMode;
					instance.Dirty = true;
				}

				if (meshRendererComponent &&
					meshRendererComponent.receiveShadows != ownerReceiveShadows)
				{
					meshRendererComponent.receiveShadows = ownerReceiveShadows;
					instance.Dirty = true;
				}


				//*
				if (!onlyFastRefreshes)
				{
					var meshRendererComponentSO	= instance.CachedMeshRendererSO as UnityEditor.SerializedObject;
					if (meshRendererComponentSO == null)
					{
						if (meshRendererComponent)
						{
							instance.CachedMeshRendererSO =
							meshRendererComponentSO = new SerializedObject(meshRendererComponent);
						}
					} else
					if (!meshRendererComponent)
					{
						instance.CachedMeshRendererSO =
						meshRendererComponentSO = null; 
					}
					if (meshRendererComponentSO != null)
					{
						bool SOModified = false;
						meshRendererComponentSO.Update(); 
						var scaleInLightmapProperty = meshRendererComponentSO.FindProperty("m_ScaleInLightmap");
						var scaleInLightmap			= owner.scaleInLightmap;
						if (scaleInLightmapProperty != null &&
							scaleInLightmapProperty.floatValue != scaleInLightmap)
						{
							scaleInLightmapProperty.floatValue = scaleInLightmap;
							SOModified = true;
						}

						var autoUVMaxDistanceProperty		= meshRendererComponentSO.FindProperty("m_AutoUVMaxDistance");
						var autoUVMaxDistance				= owner.autoUVMaxDistance;
						if (autoUVMaxDistanceProperty != null &&
							autoUVMaxDistanceProperty.floatValue != autoUVMaxDistance)
						{
							autoUVMaxDistanceProperty.floatValue = autoUVMaxDistance;
							SOModified = true;
						}

						var autoUVMaxAngleProperty			= meshRendererComponentSO.FindProperty("m_AutoUVMaxAngle");
						var autoUVMaxAngle					= owner.autoUVMaxAngle;
						if (autoUVMaxAngleProperty != null &&
							autoUVMaxAngleProperty.floatValue != autoUVMaxAngle)
						{
							autoUVMaxAngleProperty.floatValue = autoUVMaxAngle;
							SOModified = true;
						}

						var ignoreNormalsProperty			= meshRendererComponentSO.FindProperty("m_IgnoreNormalsForChartDetection");
						var ignoreNormals					= owner.IgnoreNormals;
						if (ignoreNormalsProperty != null &&
							ignoreNormalsProperty.boolValue != ignoreNormals)
						{
							ignoreNormalsProperty.boolValue = ignoreNormals;
							SOModified = true;
						}
					
						var minimumChartSizeProperty		= meshRendererComponentSO.FindProperty("m_MinimumChartSize");
						var minimumChartSize				= owner.minimumChartSize;
						if (minimumChartSizeProperty != null &&
							minimumChartSizeProperty.intValue != minimumChartSize)
						{
							minimumChartSizeProperty.intValue = minimumChartSize;
							SOModified = true;
						}

						var preserveUVsProperty		= meshRendererComponentSO.FindProperty("m_PreserveUVs");
						var preserveUVs				= owner.PreserveUVs;
						if (preserveUVsProperty != null &&
							preserveUVsProperty.boolValue != preserveUVs)
						{
							preserveUVsProperty.boolValue = preserveUVs;
							SOModified = true;
						}

#if UNITY_2017_2_OR_NEWER
                        var stitchLightmapSeamsProperty = meshRendererComponentSO.FindProperty("m_StitchLightmapSeams");
						var stitchLightmapSeams			= owner.StitchLightmapSeams;
						if (stitchLightmapSeamsProperty != null && // Note that some alpha/beta versions of 2017.2 had a different name
							stitchLightmapSeamsProperty.boolValue != stitchLightmapSeams)
						{
							stitchLightmapSeamsProperty.boolValue = stitchLightmapSeams;
							SOModified = true;
						}
#endif

						if (SOModified)
							meshRendererComponentSO.ApplyModifiedProperties();
					}
				}
                //*/

#if UNITY_2019_2_OR_NEWER
                var receiveGI = owner.ReceiveGI;
                if (meshRendererComponent &&
                    meshRendererComponent.receiveGI != receiveGI)
                {
                    meshRendererComponent.receiveGI = receiveGI;
                    instance.Dirty = true;
                }
#endif

                if (meshRendererComponent &&
					meshRendererComponent.sharedMaterial != requiredMaterial)
				{
					meshRendererComponent.sharedMaterial = requiredMaterial;
					instance.Dirty = true;
				}

				// we don't actually want the unity style of rendering a wireframe 
				// for our meshes, so we turn it off
				//*
				EditorUtility.SetSelectedRenderState(meshRendererComponent, EditorSelectedRenderState.Hidden);
				//*/
			} else
			{
				if (meshFilterComponent)	{ meshFilterComponent.hideFlags = HideFlags.None; UnityEngine.Object.DestroyImmediate(meshFilterComponent); instance.Dirty = true; }
				if (meshRendererComponent)	{ meshRendererComponent.hideFlags = HideFlags.None; UnityEngine.Object.DestroyImmediate(meshRendererComponent); instance.Dirty = true; }
				instance.LightingHashValue = instance.MeshDescription.geometryHashValue;
				meshFilterComponent = null;
				meshRendererComponent = null;
				instance.CachedMeshRendererSO = null;
			}
						
			instance.CachedMeshFilter   = meshFilterComponent;
			instance.CachedMeshRenderer = meshRendererComponent;

			// TODO:	navmesh specific mesh
			// TODO:	occludee/reflection probe static
			
			var meshColliderComponent	= instance.CachedMeshCollider;
			if (needMeshCollider)
			{
				if (!meshColliderComponent)
					meshColliderComponent = gameObject.GetComponent<MeshCollider>();
				if (meshColliderComponent && !meshColliderComponent.enabled)
					meshColliderComponent.enabled = true;

				if (!meshColliderComponent)
				{
					meshColliderComponent = gameObject.AddComponent<MeshCollider>();
					meshColliderComponent.gameObject.name = ColliderMeshInstanceName;
					instance.Dirty = true;
				}

				// stops it from rendering wireframe in scene
				if ((meshColliderComponent.hideFlags & HideFlags.HideInHierarchy) == 0)
				{
					meshColliderComponent.hideFlags |= HideFlags.HideInHierarchy;
				}

				var currentPhyicsMaterial = instance.PhysicsMaterial ?? owner.DefaultPhysicsMaterial;
				if (meshColliderComponent.sharedMaterial != currentPhyicsMaterial)
				{
					meshColliderComponent.sharedMaterial = currentPhyicsMaterial;
					instance.Dirty = true;
				}

				var setToConvex = owner.SetColliderConvex;
				if (meshColliderComponent.convex != setToConvex)
				{
					meshColliderComponent.convex = setToConvex;
					instance.Dirty = true;
				}

#if UNITY_2017_3_OR_NEWER
				var cookingOptions = owner.MeshColliderCookingOptions;
				if (meshColliderComponent.cookingOptions != cookingOptions)
				{
					meshColliderComponent.cookingOptions = cookingOptions;
					instance.Dirty = true;
				}
#endif

				if (instance.RenderSurfaceType == RenderSurfaceType.Trigger ||
					owner.IsTrigger)
				{
					if (!meshColliderComponent.isTrigger)
					{
						meshColliderComponent.isTrigger = true;
						instance.Dirty = true;
					}
				} else
				{
					if (meshColliderComponent.isTrigger)
					{
						meshColliderComponent.isTrigger = false;
						instance.Dirty = true;
					}
				}

				if (meshColliderComponent.sharedMesh != instance.SharedMesh)
					meshColliderComponent.sharedMesh = instance.SharedMesh;

				// .. for some reason this fixes mesh-colliders not being found with ray-casts in the editor?
#if UNITY_EDITOR
				if (instance.Dirty)
				{
					meshColliderComponent.enabled = false;
					meshColliderComponent.enabled = true;
				}
#endif
			} else
			{
				if (meshColliderComponent) { meshColliderComponent.hideFlags = HideFlags.None; UnityEngine.Object.DestroyImmediate(meshColliderComponent); instance.Dirty = true; }
				meshColliderComponent = null;
			}
			instance.CachedMeshCollider = meshColliderComponent;
			
			if (!postProcessScene)
			{
#if SHOW_GENERATED_MESHES
				if (instance.Dirty)
					UpdateName(instance);
#else
				/*
				if (needMeshRenderer)
				{
					if (instance.name != RenderMeshInstanceName)
						instance.name = RenderMeshInstanceName;
				} else
				if (needMeshCollider)
				{
					if (instance.name != ColliderMeshInstanceName)
						instance.name = ColliderMeshInstanceName;
				}
				*/
#endif
				instance.Dirty = false;
			}
		}

#if SHOW_GENERATED_MESHES
		private static void UpdateName(GeneratedMeshInstance instance)
		{
			var renderMaterial			= instance.RenderMaterial;
			var parentObject			= instance.gameObject;

			var builder = new System.Text.StringBuilder();
			builder.Append(instance.RenderSurfaceType);
			builder.Append(' ');
			builder.Append(instance.GetInstanceID());

			if (instance.PhysicsMaterial)
			{
				var physicmaterialName = ((!instance.PhysicsMaterial) ? "default" : instance.PhysicsMaterial.name);
				if (builder.Length > 0) builder.Append(' ');
				builder.AppendFormat(" Physics [{0}]", physicmaterialName);
			}
			if (renderMaterial)
			{
				builder.AppendFormat(" Material [{0} {1}]", renderMaterial.name, renderMaterial.GetInstanceID());
			}

			builder.AppendFormat(" Key {0}", instance.GenerateKey().GetHashCode());

			var objectName = builder.ToString();
			if (parentObject.name != objectName) parentObject.name = objectName;
			if (instance.SharedMesh &&
				instance.SharedMesh.name != objectName)
				instance.SharedMesh.name = objectName;
		}
#endif

		static int RefreshModelCounter = 0;
		
		public static void UpdateHelperSurfaceVisibility(bool force = false)
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;
			
//			updateMeshColliderMeshTime = 0.0;
			var models = InternalCSGModelManager.Models;
			var currentRefreshModelCount = 0;
			for (var i = 0; i < models.Length; i++)
			{
				var model = models[i];
				if (!model)
					continue;
				
				var generatedMeshes = model.generatedMeshes;
				if (!generatedMeshes || generatedMeshes.owner != model)
					continue;

				if (force ||
					RefreshModelCounter == currentRefreshModelCount)
				{
					UpdateContainerFlags(generatedMeshes);
					foreach (var pair in generatedMeshes.meshInstanceLookup)
					{
						var instance = pair.Value;
						if (!instance)
						{
							ValidateGeneratedMeshesDelayed(generatedMeshes);
							continue;
						}

						Refresh(instance, generatedMeshes.owner, onlyFastRefreshes: !force);
					}
				}
				currentRefreshModelCount++;
			}
			
			if (RefreshModelCounter < currentRefreshModelCount)
				RefreshModelCounter++;
			else
				RefreshModelCounter = 0;
		}

		private static void AssignLayerToChildren(GameObject gameObject)
		{
			if (!gameObject)
				return;
			var layer = gameObject.layer;
            foreach (var transform in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (transform.GetComponent<CSGNode>())
                    transform.gameObject.layer = layer;
            }
		}

		public static void UpdateGeneratedMeshesVisibility(CSGModel model)
		{
			if (!model.generatedMeshes)
				return;

			UpdateGeneratedMeshesVisibility(model.generatedMeshes, model.ShowGeneratedMeshes);
		}

		public static void UpdateGeneratedMeshesVisibility(GeneratedMeshes container, bool visible)
		{
			if (!container.owner.isActiveAndEnabled ||
				(container.owner.hideFlags & (HideFlags.HideInInspector | HideFlags.HideInHierarchy)) == (HideFlags.HideInInspector | HideFlags.HideInHierarchy))
				return;
			
			var containerGameObject = container.gameObject; 
			
			HideFlags gameObjectFlags;
			HideFlags transformFlags;
#if SHOW_GENERATED_MESHES
			gameObjectFlags = HideFlags.None;
#else
			if (visible)
			{
				gameObjectFlags = HideFlags.HideInInspector;
			} else
			{
				gameObjectFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
			}
#endif
			transformFlags = gameObjectFlags | HideFlags.NotEditable;

			if (containerGameObject.hideFlags != gameObjectFlags)
			{
				containerGameObject.hideFlags = gameObjectFlags;
			}

			if (container.transform.hideFlags != transformFlags)
			{
				container.transform.hideFlags   = transformFlags;
				container.hideFlags             = transformFlags | ComponentHideFlags;
			}
		}

		static void AutoUpdateRigidBody(GeneratedMeshes container)
		{
			var model		= container.owner;
			var gameObject	= model.gameObject;
			if (ModelTraits.NeedsRigidBody(model))
			{
				var rigidBody = container.CachedRigidBody;
				if (!rigidBody)
					rigidBody = model.GetComponent<Rigidbody>();
				if (!rigidBody)
					rigidBody = gameObject.AddComponent<Rigidbody>();

				if (rigidBody.hideFlags != HideFlags.None)
				{
					rigidBody.hideFlags = HideFlags.None;
				}

				RigidbodyConstraints constraints;
				bool isKinematic;
				bool useGravity;
				if (ModelTraits.NeedsStaticRigidBody(model))
				{
					isKinematic = true;
					useGravity = false;
					constraints = RigidbodyConstraints.FreezeAll;
				} else
				{
					isKinematic = false;
					useGravity = true;
					constraints = RigidbodyConstraints.None;
				}
				
				if (rigidBody.isKinematic != isKinematic) rigidBody.isKinematic = isKinematic;
				if (rigidBody.useGravity  != useGravity) rigidBody.useGravity = useGravity;
				if (rigidBody.constraints != constraints) rigidBody.constraints = constraints;
				container.CachedRigidBody = rigidBody;
			} else
			{
				var rigidBody = container.CachedRigidBody;
				if (!rigidBody)
					rigidBody = model.GetComponent<Rigidbody>();
				if (rigidBody)
				{
					rigidBody.hideFlags = HideFlags.None;
					UnityEngine.Object.DestroyImmediate(rigidBody);
				}
				container.CachedRigidBody = null;
			}
		}

		public static void RemoveIfEmpty(GameObject gameObject)
		{
			var allComponents = gameObject.GetComponents<Component>();
			for (var i = 0; i < allComponents.Length; i++)
			{
				if (allComponents[i] is Transform)
					continue;
				if (allComponents[i] is GeneratedMeshInstance)
					continue;

				return;
			}
			Destroy(gameObject);
		}

		public static void ValidateGeneratedMeshesNow(GeneratedMeshes generatedMeshes, bool skipSiblingCheck = false)
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			if (!generatedMeshes)
				return;

			if (generatedMeshes.owner)
			{
				if (!skipSiblingCheck)
				{
					ValidateModelNow(generatedMeshes.owner, true);
					if (!generatedMeshes)
						return;
				}
			} else
			{
				Destroy(generatedMeshes.gameObject);
				return;
			}

			var generatedMeshesGameObject = generatedMeshes.gameObject;
			var generatedMeshesTransform = generatedMeshesGameObject.transform;

			generatedMeshes.meshInstanceLookup.Clear();
			for (var i = 0; i < generatedMeshesTransform.childCount; i++)
			{
				var meshInstanceTransform	= generatedMeshesTransform.GetChild(i);
				var meshInstance			= meshInstanceTransform.GetComponent<GeneratedMeshInstance>();
				if (!meshInstance)
				{
					Destroy(meshInstanceTransform.gameObject);
					continue;
				}
				var key = meshInstance.GenerateKey();
				if (generatedMeshes.meshInstanceLookup.ContainsKey(key))
				{
					Destroy(meshInstanceTransform.gameObject);
					continue;
				}

				/*
				if (meshInstance.RenderSurfaceType == RenderSurfaceType.Normal && !meshInstance.RenderMaterial)
				{
					Destroy(meshInstanceTransform.gameObject);
					continue;
				}
				*/
				if (!ValidMeshInstance(meshInstance))
				{
					Destroy(meshInstanceTransform.gameObject);
					continue;
				}

				generatedMeshes.meshInstanceLookup[key] = meshInstance;
			}

			if (string.IsNullOrEmpty(generatedMeshesGameObject.name))
			{
				var flags = generatedMeshesGameObject.hideFlags;

				if (generatedMeshesGameObject.hideFlags != HideFlags.None)
				{
					generatedMeshesGameObject.hideFlags = HideFlags.None;
				}

				generatedMeshesGameObject.name  = MeshContainerName;

				if (generatedMeshesGameObject.hideFlags != flags)
				{
					generatedMeshesGameObject.hideFlags = flags;
				}
			}

			if (generatedMeshes.owner)
				UpdateGeneratedMeshesVisibility(generatedMeshes, generatedMeshes.owner.ShowGeneratedMeshes);
			
			if (generatedMeshes.owner)
			{
				var modelTransform = generatedMeshes.owner.transform;
				if (generatedMeshesTransform.parent != modelTransform)
					generatedMeshesTransform.parent.SetParent(modelTransform, true);
			}
		}
		
		public static GeneratedMeshInstance[] GetAllModelMeshInstances(GeneratedMeshes container)
		{
			if (container.meshInstanceLookup == null ||
				container.meshInstanceLookup.Count == 0)
				return null;

			return container.meshInstanceLookup.Values.ToArray();
		}
		
		public static GeneratedMeshInstance GetMeshInstance(GeneratedMeshes container, GeneratedMeshDescription meshDescription, ModelSettingsFlags modelSettings, RenderSurfaceType renderSurfaceType)
		{
			var key	= MeshInstanceKey.GenerateKey(meshDescription);
			GeneratedMeshInstance instance;
			if (container.meshInstanceLookup.TryGetValue(key, out instance))
			{
				if (instance)
					return instance;
			}

			instance = CreateMeshInstance(container, meshDescription, modelSettings, renderSurfaceType);
			if (!instance)
				return null;
			return instance;
		}

		public static HelperSurfaceDescription GetHelperSurfaceDescription(GeneratedMeshes container, ModelSettingsFlags modelSettings, GeneratedMeshDescription meshDescription, RenderSurfaceType renderSurfaceType)
		{
			var key = MeshInstanceKey.GenerateKey(meshDescription);
			HelperSurfaceDescription instance;
			if (container.helperSurfaceLookup.TryGetValue(key, out instance))
				return instance;

			return CreateHelperSurfaceDescription(container, meshDescription, modelSettings, renderSurfaceType);
		}

#region UpdateTransform
		public static void UpdateTransforms()
		{
			var models = InternalCSGModelManager.Models;
			for (var i = 0; i < models.Length; i++)
			{
				var model = models[i];
				if (!model)
					continue;
			
				UpdateTransform(model.generatedMeshes);
			}
		}

		static void UpdateTransform(GeneratedMeshes container)
		{
			if (!container || !container.owner)
			{
				return;
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			// TODO: make sure outlines are updated when models move
			
			var containerTransform = container.transform;
			if (containerTransform.localPosition	!= MathConstants.zeroVector3 ||
				containerTransform.localRotation	!= MathConstants.identityQuaternion ||
				containerTransform.localScale		!= MathConstants.oneVector3)
			{
				containerTransform.localPosition	= MathConstants.zeroVector3;
				containerTransform.localRotation	= MathConstants.identityQuaternion;
				containerTransform.localScale		= MathConstants.oneVector3;
				SceneToolRenderer.SetOutlineDirty();
			}
		}
#endregion

#region UpdateContainerComponents
		static readonly List<GeneratedMeshInstance> __notfoundInstances		= new List<GeneratedMeshInstance>();
		static MeshInstanceKey[]					__removeMeshInstances	= new MeshInstanceKey[0];
		public static void UpdateContainerComponents(GeneratedMeshes container, 
													 HashSet<GeneratedMeshInstance> foundInstances,
													 HashSet<HelperSurfaceDescription> foundHelperSurfaces)
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			if (!container || !container.owner)
				return;

			if (container.meshInstanceLookup == null)
				ValidateGeneratedMeshesNow(container);


			container.SetHelperSurfaces(foundHelperSurfaces.ToArray());
			container.SetMeshInstances(foundInstances.ToArray());

			__notfoundInstances.Clear();
			var instances = container.GetComponentsInChildren<GeneratedMeshInstance>(true);
			if (foundInstances == null)
			{
				__notfoundInstances.AddRange(instances);
			} else
			{
				for (int i = 0; i < instances.Length; i++)
				{
					var instance = instances[i];
					if (!foundInstances.Contains(instance))
					{
						__notfoundInstances.Add(instance);
						continue;
					}

					var key = instance.GenerateKey();
					container.meshInstanceLookup[key] = instance;
				}
			}
			
			for (int i = 0; i < __notfoundInstances.Count; i++)
			{
				var meshInstance = __notfoundInstances[i];
				if (meshInstance && meshInstance.gameObject)
				{
					Destroy(meshInstance.gameObject);
				}

				if (__removeMeshInstances.Length < container.meshInstanceLookup.Count)
				{
					__removeMeshInstances = new MeshInstanceKey[container.meshInstanceLookup.Count];
				}

				int removeMeshInstancesCount = 0;
				foreach(var item in container.meshInstanceLookup)
				{
					if (!item.Value ||
						item.Value == meshInstance)
					{
						__removeMeshInstances[removeMeshInstancesCount] = item.Key;
						removeMeshInstancesCount++;
					}
				}
				if (removeMeshInstancesCount > 0)
				{
					if (removeMeshInstancesCount == container.meshInstanceLookup.Count)
					{
						container.meshInstanceLookup.Clear();
					} else
					{
						for (int j = 0; j < removeMeshInstancesCount; j++)
						{
							container.meshInstanceLookup.Remove(__removeMeshInstances[j]);
						}
					}
				}
			}
			 
			if (!container.owner)
				return;

			UpdateTransform(container);
		}
#endregion
		
#region UpdateContainerFlags
		private static void UpdateContainerFlags(GeneratedMeshes container)
		{
			if (!container)
				return;
			if (container.owner)
			{
				var ownerTransform = container.owner.transform;
				if (container.transform.parent != ownerTransform) 
				{
					container.transform.SetParent(ownerTransform, false);
				}

				if (!container)
					return;
			}

			//var isTrigger			= container.owner.IsTrigger;
			//var collidable		= container.owner.HaveCollider || isTrigger;
			var ownerStaticFlags	= GameObjectUtility.GetStaticEditorFlags(container.owner.gameObject);
			var previousStaticFlags	= GameObjectUtility.GetStaticEditorFlags(container.gameObject);
			var containerTag		= container.owner.gameObject.tag;
			var containerLayer		= container.owner.gameObject.layer;
			
			var showVisibleSurfaces	= (RealtimeCSG.CSGSettings.VisibleHelperSurfaces & HelperSurfaceFlags.ShowVisibleSurfaces) != 0;


			if (ownerStaticFlags != previousStaticFlags ||
				containerTag   != container.gameObject.tag ||
				containerLayer != container.gameObject.layer)
			{
				foreach (var meshInstance in container.meshInstanceLookup.Values)
				{
					if (!meshInstance)
						continue;

					if (meshInstance.RenderSurfaceType == RenderSurfaceType.Normal)
					{
						var gameObject = meshInstance.gameObject;
						if (gameObject.activeSelf != showVisibleSurfaces)
							gameObject.SetActive(showVisibleSurfaces);
					}

					var oldStaticFlags = GameObjectUtility.GetStaticEditorFlags(meshInstance.gameObject);
					var newStaticFlags = FilterStaticEditorFlags(oldStaticFlags, meshInstance.RenderSurfaceType);

					foreach (var transform in meshInstance.GetComponentsInChildren<Transform>(true))
					{
						var gameObject = transform.gameObject;
						if (oldStaticFlags != newStaticFlags)
							GameObjectUtility.SetStaticEditorFlags(gameObject, newStaticFlags);
						if (gameObject.tag != containerTag)
							gameObject.tag = containerTag;
						if (gameObject.layer != containerLayer)
							gameObject.layer = containerLayer;
					}
				}
			}

			if (container.owner.NeedAutoUpdateRigidBody)
				AutoUpdateRigidBody(container);
		}
#endregion
	}
}
