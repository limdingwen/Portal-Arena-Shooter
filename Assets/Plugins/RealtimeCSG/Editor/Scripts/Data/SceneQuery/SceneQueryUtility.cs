﻿using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;
using RealtimeCSG;
using RealtimeCSG.Legacy;
using RealtimeCSG.Components;
using Object = UnityEngine.Object;

namespace InternalRealtimeCSG
{
	internal sealed class PointSelection
	{
		public PointSelection(int brushNodeID, int pointIndex) { BrushNodeID = brushNodeID; PointIndex = pointIndex; }
		public readonly int BrushNodeID;
		public readonly int PointIndex;
	}

	internal static class SceneQueryUtility
	{
		#region GetAllComponentsInScene
		public static List<T> GetAllComponentsInScene<T>(Scene scene)
			where T : Component
		{
			var items = new List<T>();
			var rootItems = GetRootGameObjectsInScene(scene);
			for (int i = 0; i < rootItems.Length; i++)
			{
				var root = rootItems[i];
				if (!root)
					continue;
				items.AddRange(root.GetComponentsInChildren<T>(true));
			}
			return items;
		}

		public static GameObject[] GetRootGameObjectsInScene(Scene scene)
		{
			if (scene.isLoaded)
				return scene.GetRootGameObjects();
			
			var rootLookup = new HashSet<Transform>();
			var transforms = Object.FindObjectsOfType<Transform>();
			for (int i = 0; i < transforms.Length;i++)
				rootLookup.Add(transforms[i].root);

			var rootArray = rootLookup.ToArray();
			var gameObjectArray = new GameObject[rootArray.Length];
			for (int i = 0; i < rootArray.Length; i++)
				gameObjectArray[i] = rootArray[i].gameObject;

			return gameObjectArray;
		}
		#endregion

		#region GetFirstGameObjectInSceneWithName
		public static GameObject GetFirstGameObjectInSceneWithName(Scene scene, string name)
		{
			foreach (var root in scene.GetRootGameObjects())
			{
				if (!root)
					continue;
				if (root.name == name)
					return root;
				foreach (var transform in root.GetComponentsInChildren<Transform>(true))
				{
					if (transform.name == name)
						return transform.gameObject;
				}
			}
			return null;
		}
		#endregion

		#region GetUniqueHiddenGameObjectInSceneWithName
		internal static GameObject GetUniqueHiddenGameObjectInSceneWithName(Scene scene, string name)
		{
			if (!scene.IsValid() || !scene.isLoaded)
				return null;

			var rootGameObjects = scene.GetRootGameObjects();
			GameObject foundRoot = null;
			for (int i = 0; i < rootGameObjects.Length; i++)
			{
				var root = rootGameObjects[i];
				if (!root)
					continue;

				if (root.hideFlags != HideFlags.None &&
					root.name == name)
				{
					if (foundRoot)
					{
						Object.DestroyImmediate(root);
						continue;
					}
					foundRoot = root;
				}

				var rootChildren = root.GetComponentsInChildren<Transform>(true);
				for (int j = 0; j < rootChildren.Length; j++)
				{
					var child = rootChildren[j];
					if (child == root)
						continue;
					if (!child)
						continue;

					if (child.hideFlags == HideFlags.None ||
						child.name != name)
						continue;
					
					if (foundRoot)
					{
						Object.DestroyImmediate(child.gameObject);
						continue;
					}
					foundRoot = child.gameObject;
				}
			}
			return foundRoot;
		}
		#endregion


		#region GetGroupObjectIfObjectIsPartOfGroup
		public static GameObject GetGroupGameObjectIfObjectIsPartOfGroup(GameObject gameObject)
		{
			if (gameObject == null)
				return null;

			var node = gameObject.GetComponentInChildren<CSGNode>();
			if (!node)
				return gameObject;

			var operation = GetGroupOperationForNode(node);
			return operation == null ? gameObject : operation.gameObject;
		}
        #endregion

        internal static bool GameObjectContainsAttribute<T>(GameObject go) where T : Attribute
        {
            var behaviours = go.GetComponents(typeof(Component));
            for (var index = 0; index < behaviours.Length; index++)
            {
                var behaviour = behaviours[index];
                if (behaviour == null)
                    continue;

                var behaviourType = behaviour.GetType();
                if (behaviourType.GetCustomAttributes(typeof(T), true).Length > 0)
                    return true;
            }
            return false;
        }

        internal static GameObject FindSelectionBase(GameObject go)
        {
            if (go == null)
                return null;

#if UNITY_2018_3_OR_NEWER
            Transform prefabBase = null;
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(go))
            {
                prefabBase = PrefabUtility.GetOutermostPrefabInstanceRoot(go).transform;
            }
#endif

            GameObject group = null;
            Transform groupTransform = null;
            var node = go.GetComponentInChildren<CSGNode>();
            if (node)
            {
                var operation = GetGroupOperationForNode(node);
                group = (operation == null) ? null : operation.gameObject;
                groupTransform = (operation == null) ? null : operation.transform;
            }


            Transform tr = go.transform;
            while (tr != null)
            {
#if UNITY_2018_3_OR_NEWER
                if (tr == prefabBase)
                    return tr.gameObject;
#endif
                if (tr == groupTransform)
                    return group;

                if (GameObjectContainsAttribute<SelectionBaseAttribute>(tr.gameObject))
                    return tr.gameObject;

                tr = tr.parent;
            }

            return go;
        }

        #region GetGroupOperationForNode (private)
        private static CSGOperation GetGroupOperationForNode(CSGNode node)
		{
			if (!node)
				return null;

			var parent = node.transform.parent;
			while (parent)
			{
				var model = parent.GetComponent<CSGModel>();
				if (model)
					return null;

				var parentOp = parent.GetComponent<CSGOperation>();
				if (parentOp &&
					//!parentOp.PassThrough && 
					parentOp.HandleAsOne)
					return parentOp;

				parent = parent.transform.parent;
			}
			return null;
		}
        #endregion

        #region GetTopMostGroupForNode
		public static CSGNode GetTopMostGroupForNode(CSGNode node)
		{
			if (!node)
				return null;

			var topSelected = node;
			var parent = node.transform.parent;
			while (parent)
			{
				var model = parent.GetComponent<CSGModel>();
				if (model)
					break;

				var parentOp = parent.GetComponent<CSGOperation>();
				if (parentOp &&
					parentOp.HandleAsOne &&
					!parentOp.PassThrough)
					topSelected = parentOp;

				parent = parent.transform.parent;
			}
			return topSelected;
		}
        #endregion


        #region DeselectAllChildBrushes (private)
		private static void DeselectAllChildBrushes(Transform transform, HashSet<GameObject> objectsInFrustum)
		{
			var visibleLayers = Tools.visibleLayers;
			 
			for (int i = 0, childCount = transform.childCount; i < childCount; i++)
			{
				var childTransform = transform.GetChild(i);
				var childNode = childTransform.GetComponent<CSGNode>();
				if (!childNode || (childNode is CSGModel) || ((1 << childNode.gameObject.layer) & visibleLayers) == 0)
					continue;

				var childGameObject = childTransform.gameObject;
				objectsInFrustum.Remove(childGameObject);
				DeselectAllChildBrushes(childTransform.transform, objectsInFrustum);
			}
		}
        #endregion

        #region AreAllBrushesSelected (private)
		private static bool AreAllBrushesSelected(Transform transform, HashSet<GameObject> objectsInFrustum)
		{			
			var visibleLayers = Tools.visibleLayers;
			

			var allChildrenSelected = true;
			var i = 0;
			var childCount = transform.childCount;
			for (; i < childCount; i++)
			{
				var childTransform = transform.GetChild(i);
				var childNode = childTransform.GetComponent<CSGNode>();
				if (!childNode || (childNode is CSGModel) || ((1 << childNode.gameObject.layer) & visibleLayers) == 0)
				{
					continue;
				}

				var childGameObject = childTransform.gameObject;
				if (!childTransform.gameObject.activeInHierarchy)
				{
					objectsInFrustum.Remove(childGameObject);
					continue;
				}

				if (objectsInFrustum.Contains(childGameObject))
				{
					objectsInFrustum.Remove(childGameObject);
					continue;
				}

				var childOperation = childNode as CSGOperation;
				if (childOperation == null ||
					!childOperation.PassThrough)
				{
					objectsInFrustum.Remove(childGameObject);
					allChildrenSelected = false;
					break;
				}

				var result = AreAllBrushesSelected(childTransform, objectsInFrustum);
				objectsInFrustum.Remove(childGameObject);

				if (result)
					continue;

				objectsInFrustum.Remove(childGameObject);
				allChildrenSelected = false;
				break;
			}
			if (allChildrenSelected)
				return true;

			for (; i < childCount; i++)
			{
				var childTransform = transform.GetChild(i);
				var childNode = childTransform.GetComponent<CSGNode>();
				if (!childNode || (childNode is CSGModel))
					continue;

				var childGameObject = childTransform.gameObject;
				objectsInFrustum.Remove(childGameObject);
				DeselectAllChildBrushes(childTransform.transform, objectsInFrustum);
			}
			return false;
		}
        #endregion


        #region GetItemsInFrustum
		public static bool GetItemsInFrustum(Plane[] planes,
											 HashSet<GameObject> objectsInFrustum)
		{
			if (objectsInFrustum == null)
				return false;

			objectsInFrustum.Clear();
			var found = false;
			foreach (var model in InternalCSGModelManager.Models)
			{
				if (!ModelTraits.WillModelRender(model))
					continue;
				found = InternalCSGModelManager.External.GetItemsInFrustum(model, planes, objectsInFrustum) || found;
			}

			var visibleLayers = Tools.visibleLayers;

			var items = objectsInFrustum.ToArray();
			for (var i = items.Length - 1; i >= 0; i--)
			{
				var child = items[i];
				var node = child.GetComponent<CSGNode>();
				if (!node || ((1 << node.gameObject.layer) & visibleLayers) == 0)
					continue;

				if (!objectsInFrustum.Contains(child))
					continue;

				while (true)
				{
					var parent = GetGroupOperationForNode(node);
					if (!parent ||
						!AreAllBrushesSelected(parent.transform, objectsInFrustum))
						break;

					objectsInFrustum.Add(parent.gameObject);
					node = parent;
				}
			}
			return found;
		}
        #endregion

        #region GetPointsInFrustum
		internal static PointSelection[] GetPointsInFrustum(SceneView sceneView,
															Plane[] planes,
														    CSGBrush[] brushes,
															ControlMeshState[] controlMeshStates,
															bool ignoreHiddenPoints)
		{
			var pointSelection = new List<PointSelection>();
			for (var t = 0; t < brushes.Length; t++)
			{
				var targetMeshState = controlMeshStates[t];
				if (targetMeshState == null)
					continue;

				var sceneViewState = targetMeshState.GetSceneViewState(sceneView, false);

				for (var p = 0; p < targetMeshState.WorldPoints.Length; p++)
				{
					if (ignoreHiddenPoints && sceneViewState.WorldPointBackfaced[p])
						continue;
					var point = targetMeshState.WorldPoints[p];
					var found = true;
					for (var i = 0; i < 6; i++)
					{
						if (!(planes[i].GetDistanceToPoint(point) > MathConstants.DistanceEpsilon))
							continue;

						found = false;
						break;
					}

					if (found)
					{
						pointSelection.Add(new PointSelection(t, p));
					}
				}
			}
			return pointSelection.ToArray();
		}
        #endregion

        #region DeepSelection (private)

		private static LegacyBrushIntersection[] _deepClickIntersections;
		private static Vector2 _prevSceenPos = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
		private static SceneView _prevSceneView;
		private static int _deepIndex;
		private static void ResetDeepClick()
		{
			_deepClickIntersections = null;
			_prevSceneView = null;
			_prevSceenPos = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
			_deepIndex = 0;
		}

        #endregion

        #region Find..xx..Intersection

        #region FindClickWorldIntersection
		public static bool FindClickWorldIntersection(Vector2 screenPos, out GameObject foundObject)
		{
            var sceneView = SceneView.currentDrawingSceneView;// ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
			var camera = sceneView ? sceneView.camera : Camera.current;

			foundObject = null;
			if (!camera)
				return false;

			var worldRay		= HandleUtility.GUIPointToWorldRay(screenPos);
			var worldRayStart	= worldRay.origin;
			var worldRayVector	= (worldRay.direction * (camera.farClipPlane - camera.nearClipPlane));
			var worldRayEnd		= worldRayStart + worldRayVector;

			CSGModel intersectionModel = null;
			if (_prevSceenPos == screenPos && _prevSceneView == sceneView && _deepClickIntersections != null)
			{
				var prevIntersection = (_deepIndex > 0 && _deepIndex < _deepClickIntersections.Length) ? _deepClickIntersections[_deepIndex] : null;
				if (_deepClickIntersections.Length > 1)
				{
					var visibleLayers = Tools.visibleLayers;
					for (var i = _deepClickIntersections.Length - 1; i >= 0; i--)
					{
						if (((1 << _deepClickIntersections[i].gameObject.layer) & visibleLayers) == 0)
							continue;

						if (_deepClickIntersections[i].brush)
							continue;
						ArrayUtility.RemoveAt(ref _deepClickIntersections, i);
						if (i <= _deepIndex)
							_deepIndex--;
					}
				}

				if (_deepClickIntersections.Length <= 1)
				{
					ResetDeepClick();
				}
				else
				{
					_deepIndex = (_deepIndex + 1) % _deepClickIntersections.Length;
					var currentIntersection = (_deepIndex > 0 && _deepIndex < _deepClickIntersections.Length) ? _deepClickIntersections[_deepIndex] : null;
					if (currentIntersection != prevIntersection &&
						currentIntersection != null)
					{
						foundObject = currentIntersection.gameObject;
						_prevSceenPos = screenPos;
						_prevSceneView = sceneView;
						intersectionModel = currentIntersection.model;
					}
					else
					{
						ResetDeepClick();
					}
				}
			}

			if (_prevSceenPos != screenPos)
			{
				var wireframeShown = CSGSettings.IsWireframeShown(sceneView);
				if (FindMultiWorldIntersection(worldRayStart, worldRayEnd, out _deepClickIntersections, ignoreInvisibleSurfaces: !wireframeShown, ignoreUnrenderables: !wireframeShown))
				{					
					var visibleLayers = Tools.visibleLayers;
					for (int i = 0; i < _deepClickIntersections.Length; i++)
					{
						if (((1 << _deepClickIntersections[i].gameObject.layer) & visibleLayers) == 0)
							continue;

						_deepIndex = 0;
						var intersection = _deepClickIntersections[i];
						foundObject = intersection.gameObject;
						_prevSceenPos = screenPos;
						_prevSceneView = sceneView;
						intersectionModel = intersection.model;
						break;
					}
				}
				else
					ResetDeepClick();
			}

			GameObject[] modelMeshes = null;
			if (intersectionModel != null)
			{
				modelMeshes = CSGModelManager.GetModelMeshes(intersectionModel);
			}

			HideFlags[] hideFlags = null;
			if (modelMeshes != null)
			{
				hideFlags = new HideFlags[modelMeshes.Length];
				for (var i = 0; i < modelMeshes.Length; i++)
				{
					hideFlags[i] = modelMeshes[i].hideFlags;
                    if (modelMeshes[i].hideFlags != HideFlags.None)
					    modelMeshes[i].hideFlags = HideFlags.None;
				}
			}

            var gameObject = HandleUtility.PickGameObject(screenPos, true);

            if (modelMeshes != null)
			{
				for (var i = 0; i < modelMeshes.Length; i++)
				{
					var modelMesh = modelMeshes[i];
					if (!modelMesh)
						continue;

					if (gameObject == modelMesh)
						gameObject = null;

                    if (modelMesh.hideFlags != hideFlags[i])
                        modelMesh.hideFlags = hideFlags[i];
				}
			}

			if (!gameObject ||
				gameObject.GetComponent<CSGModel>() ||
				gameObject.GetComponent<CSGBrush>() ||
				gameObject.GetComponent<CSGOperation>() ||
				gameObject.GetComponent<GeneratedMeshInstance>() ||
				gameObject.GetComponent<GeneratedMeshes>())
				return (foundObject != null);

			foundObject = gameObject;
			return true;
		}
        #endregion

        #region FindMeshIntersection
		public static LegacyBrushIntersection FindMeshIntersection(Vector2 screenPos, CSGBrush[] ignoreBrushes = null, HashSet<Transform> ignoreTransforms = null)
		{
			var worldRay = HandleUtility.GUIPointToWorldRay(screenPos);
			var hit = HandleUtility.RaySnap(worldRay);
			while (hit != null)
			{
				var rh = (RaycastHit)hit;
				if (ignoreTransforms != null && ignoreTransforms.Contains(rh.transform))
				{
					worldRay.origin = rh.point + (worldRay.direction * 0.00001f);
					hit = HandleUtility.RaySnap(worldRay);
					continue;
				}

				// Check if it's a mesh ...
					if (rh.transform.GetComponent<MeshRenderer>() &&
						// .. but not one we generated
						!rh.transform.GetComponent<CSGNode>() &&
						!rh.transform.GetComponent<GeneratedMeshInstance>())
				{
					return new LegacyBrushIntersection
					{
						brushNodeID = CSGNode.InvalidNodeID,
						surfaceIndex = -1,
						worldIntersection = rh.point,
						worldPlane = new CSGPlane(-rh.normal, rh.point)
					};
				}
				break;
			}

			LegacyBrushIntersection intersection;
			if (FindWorldIntersection(worldRay, out intersection, ignoreBrushes: ignoreBrushes))
				return intersection;

			var gridPlane = RealtimeCSG.CSGGrid.CurrentGridPlane;
			var intersectionPoint = gridPlane.RayIntersection(worldRay);
			if (float.IsNaN(intersectionPoint.x) ||
				float.IsNaN(intersectionPoint.y) ||
				float.IsNaN(intersectionPoint.z) ||
				float.IsInfinity(intersectionPoint.x) ||
				float.IsInfinity(intersectionPoint.y) ||
				float.IsInfinity(intersectionPoint.z))
			{
				intersectionPoint = worldRay.GetPoint(10);
				return new LegacyBrushIntersection
				{
					brushNodeID = CSGNode.InvalidNodeID,
					surfaceIndex = -1,
					worldIntersection = MathConstants.zeroVector3,
					worldPlane = new CSGPlane(gridPlane.normal, intersectionPoint)
				};
			}

			return new LegacyBrushIntersection
			{
				brushNodeID = CSGNode.InvalidNodeID,
				surfaceIndex = -1,
				worldIntersection = intersectionPoint,
				worldPlane = gridPlane
			};
		}
        #endregion

        #region FindUnityWorldIntersection
		public static bool FindUnityWorldIntersection(Vector2 screenPos, out GameObject foundObject)
		{
			var sceneView = SceneView.currentDrawingSceneView;// ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
			var camera = sceneView ? sceneView.camera : Camera.current;

			foundObject = null;
			if (!camera)
				return false;

			var wireframeShown	= CSGSettings.IsWireframeShown(sceneView);
			var worldRay		= HandleUtility.GUIPointToWorldRay(screenPos);
			var worldRayStart	= worldRay.origin;
			var worldRayVector	= (worldRay.direction * (camera.farClipPlane - camera.nearClipPlane));
			var worldRayEnd		= worldRayStart + worldRayVector;

			CSGModel intersectionModel = null;

			LegacyBrushIntersection[] intersections;
			if (FindMultiWorldIntersection(worldRayStart, worldRayEnd, out intersections, ignoreInvisibleSurfaces: !wireframeShown))
			{
				var visibleLayers = Tools.visibleLayers;
				for (int i = 0; i < intersections.Length; i++)
				{
					if (((1 << intersections[i].gameObject.layer) & visibleLayers) == 0)
						continue;
					intersectionModel = intersections[i].model;
					break;
				}
			}

			GameObject[] modelMeshes = null;
			HideFlags[] hideFlags = null;
			if (intersectionModel != null)
			{
				modelMeshes = CSGModelManager.GetModelMeshes(intersectionModel);
				if (modelMeshes != null)
				{
					hideFlags = new HideFlags[modelMeshes.Length];
					for (var i = 0; i < modelMeshes.Length; i++)
					{
						hideFlags[i] = modelMeshes[i].hideFlags;
						modelMeshes[i].hideFlags = HideFlags.None;
					}
				}
			}

			var gameObject = HandleUtility.PickGameObject(screenPos, false);

			if (modelMeshes != null)
			{
				for (var i = 0; i < modelMeshes.Length; i++)
				{
					var modelMesh = modelMeshes[i];
					if (!modelMesh)
						continue;

					if (gameObject == modelMesh)
						gameObject = null;

					modelMesh.hideFlags = hideFlags[i];
				}
			}

			if (!gameObject ||
				gameObject.GetComponent<Canvas>() ||
				gameObject.GetComponent<CSGModel>() ||
				gameObject.GetComponent<CSGBrush>() ||
				gameObject.GetComponent<CSGOperation>() ||
				gameObject.GetComponent<GeneratedMeshInstance>() ||
				gameObject.GetComponent<GeneratedMeshes>())
				return false;

			foundObject = gameObject;
			return true;
		}
        #endregion

        #region FindWorldIntersection
		public static bool FindWorldIntersection(Vector2 screenPos, out LegacyBrushIntersection intersection, bool ignoreInvisibleSurfaces = true, bool ignoreUnrenderables = true, CSGBrush[] ignoreBrushes = null)
		{
			var worldRay = HandleUtility.GUIPointToWorldRay(screenPos);
			return FindWorldIntersection(worldRay, out intersection, ignoreInvisibleSurfaces, ignoreUnrenderables, ignoreBrushes);
		}

		public static bool FindWorldIntersection(Ray worldRay, out LegacyBrushIntersection intersection, bool ignoreInvisibleSurfaces = true, bool ignoreUnrenderables = true, CSGBrush[] ignoreBrushes = null)
		{
			var rayStart = worldRay.origin;
			var rayVector = (worldRay.direction * (Camera.current.farClipPlane - Camera.current.nearClipPlane));
			var rayEnd = rayStart + rayVector;

			return FindWorldIntersection(rayStart, rayEnd, out intersection, ignoreInvisibleSurfaces, ignoreUnrenderables, ignoreBrushes);
		}
		
		static CSGModel[] __foundModels = new CSGModel[0];
		public static bool FindWorldIntersection(Vector3 rayStart, Vector3 rayEnd, out LegacyBrushIntersection intersection, bool ignoreInvisibleSurfaces = true, bool ignoreUnrenderables = true, CSGBrush[] ignoreBrushes = null)
		{
			intersection = null;
			if (InternalCSGModelManager.External == null ||
				InternalCSGModelManager.External.RayCastMulti == null)
				return false;

			ignoreInvisibleSurfaces = ignoreInvisibleSurfaces && !CSGSettings.ShowCulledSurfaces;

			var visibleLayers = Tools.visibleLayers;
			int foundModelCount = 0;

			if (__foundModels.Length < InternalCSGModelManager.Models.Length)
				__foundModels = new CSGModel[InternalCSGModelManager.Models.Length];

			for (var g = 0; g < InternalCSGModelManager.Models.Length; g++)
			{
				var model = InternalCSGModelManager.Models[g];
				if (!model || !model.isActiveAndEnabled ||
					((1 << model.gameObject.layer) & visibleLayers) == 0)
					continue;

				if (ignoreUnrenderables && !ModelTraits.WillModelRender(model) &&
					!Selection.Contains(model.gameObject.GetInstanceID()))
					continue;

				__foundModels[foundModelCount] = model;
				foundModelCount++;
			}

			if (foundModelCount == 0)
				return false;

			LegacyBrushIntersection[] modelIntersections;
			if (!InternalCSGModelManager.External.RayCastMulti(	foundModelCount,
																__foundModels,
																rayStart,
																rayEnd,
																ignoreInvisibleSurfaces,
																out modelIntersections,
																ignoreBrushes: ignoreBrushes))
				return false;

			for (var i = 0; i < modelIntersections.Length; i++)
			{
				var modelIntersection	= modelIntersections[i];
					
				if (intersection != null &&
					modelIntersection.distance > intersection.distance)
					continue;

				var brush = modelIntersection.gameObject.GetComponent<CSGBrush>();
				if (BrushTraits.IsSurfaceSelectable(brush, modelIntersection.surfaceIndex))
					continue;

				modelIntersection.brush = brush;

				intersection = modelIntersection;
			}

			if (intersection == null)
				return false;
			
			return true;
		}
        #endregion

        #region FindMultiWorldIntersection
		public static bool FindMultiWorldIntersection(Vector3 worldRayStart, Vector3 worldRayEnd, out LegacyBrushIntersection[] intersections, bool ignoreInvisibleSurfaces = true, bool ignoreUnrenderables = true, CSGBrush[] ignoreBrushes = null)
		{
			intersections = null;
			if (InternalCSGModelManager.External == null ||
				InternalCSGModelManager.External.RayCastIntoModelMulti == null)
				return false;

			var foundIntersections = new Dictionary<CSGNode, LegacyBrushIntersection>();
			
			var visibleLayers = Tools.visibleLayers;
			ignoreInvisibleSurfaces = ignoreInvisibleSurfaces && !CSGSettings.ShowCulledSurfaces;
			for (var g = 0; g < InternalCSGModelManager.Models.Length; g++)
			{
				var model = InternalCSGModelManager.Models[g];
				if (!model ||
					!model.isActiveAndEnabled)
				{
					continue;
				}

				if (((1 << model.gameObject.layer) & visibleLayers) == 0)
					continue;
					
				if (ignoreUnrenderables && !ModelTraits.WillModelRender(model) &&
					!Selection.Contains(model.gameObject.GetInstanceID()))
					continue;

				LegacyBrushIntersection[] modelIntersections;
				if (!InternalCSGModelManager.External.RayCastIntoModelMulti(model,
																			worldRayStart,
																			worldRayEnd,
																			ignoreInvisibleSurfaces,
																			out modelIntersections,
																			ignoreBrushes: ignoreBrushes))
					continue;
				
				for (var i = 0; i < modelIntersections.Length; i++)
				{
					var intersection	= modelIntersections[i];
					var brush			= intersection.gameObject.GetComponent<CSGBrush>();
					if (BrushTraits.IsSurfaceSelectable(brush, intersection.surfaceIndex))
						continue;
					
					var currentNode = GetTopMostGroupForNode(brush);
					LegacyBrushIntersection other;
					if (foundIntersections.TryGetValue(currentNode, out other)
						&& other.distance <= intersection.distance)
						continue;

					intersection.brush = brush;
					intersection.model = model;

					foundIntersections[currentNode] = modelIntersections[i];
				}
			}

			if (foundIntersections.Count == 0)
				return false;

			var sortedIntersections = foundIntersections.Values.ToArray();
			Array.Sort(sortedIntersections, (x, y) => (int)Mathf.Sign(x.distance - y.distance));
			
			intersections = sortedIntersections;
			return true;
		}
        #endregion

        #region FindBrushIntersection
		public static bool FindBrushIntersection(CSGBrush brush, Matrix4x4 modelTransformation, Vector3 rayStart, Vector3 rayEnd, out LegacyBrushIntersection intersection)
		{
			intersection = null;
			if (!brush || InternalCSGModelManager.External.RayCastIntoBrush == null)
				return false;
			
			if (!InternalCSGModelManager.External.RayCastIntoBrush(brush.brushNodeID, 
																   rayStart,
																   rayEnd,
																   modelTransformation,
																   out intersection,
																   false))
				return false;

			if (BrushTraits.IsSurfaceSelectable(brush, intersection.surfaceIndex))
				return false;
			return true;
		}
        #endregion

        #region FindSurfaceIntersection
		public static bool FindSurfaceIntersection(CSGBrush brush, Matrix4x4 modelTransformation, Int32 surfaceIndex, Vector2 screenPos, out LegacySurfaceIntersection intersection)
		{
			var worldRay = HandleUtility.GUIPointToWorldRay(screenPos);
			var rayStart = worldRay.origin;
			var rayVector = (worldRay.direction * (Camera.current.farClipPlane - Camera.current.nearClipPlane));
			var rayEnd = rayStart + rayVector;

			return FindSurfaceIntersection(brush, modelTransformation, surfaceIndex, rayStart, rayEnd, out intersection);
		}

		public static bool FindSurfaceIntersection(CSGBrush brush, Matrix4x4 modelTransformation, Int32 surfaceIndex, Vector3 rayStart, Vector3 rayEnd, out LegacySurfaceIntersection intersection)
		{
			intersection = null;
			if (!brush ||
				InternalCSGModelManager.External.RayCastIntoBrushSurface == null)
				return false;
			
			if (!InternalCSGModelManager.External.RayCastIntoBrushSurface(brush.brushNodeID,
																		  surfaceIndex,
																		  rayStart,
																		  rayEnd,
																		  modelTransformation,
																		  out intersection))
			{
				return false;
			}

			if (BrushTraits.IsSurfaceSelectable(brush, surfaceIndex))
			{
				return false;
			}
			return true;
		}
        #endregion

        #endregion	
	}
}