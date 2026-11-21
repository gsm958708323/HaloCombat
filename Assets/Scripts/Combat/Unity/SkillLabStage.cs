using System.Collections.Generic;
using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    // Geometry and framing are presentation only. All hit tests belong to the simulation.
    internal sealed class SkillLabStage
    {
        public static readonly Quaternion ViewRotation = Quaternion.Euler(58, 0, 0);
        readonly Camera _camera;
        readonly Transform _parent;
        readonly Material _floorMaterial, _gridMaterial;
        GameObject _ground;
        Mesh _grid;
        Bounds _bounds;
        float _zoom = 1f;
        int _screenWidth, _screenHeight;

        public SkillLabStage(Camera camera, Transform parent, Material floorMaterial, Material gridMaterial)
        {
            _camera = camera;
            _parent = parent;
            _floorMaterial = floorMaterial;
            _gridMaterial = gridMaterial;
        }

        public void Build(SkillSimulationSnapshot snapshot)
        {
            Clear();
            _bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Kind != "caster" && actor.Kind != "target") continue;
                Include(actor.X, actor.Z, actor.Radius, actor.Radius);
            }
            foreach (var obstacle in snapshot.Obstacles)
                Include(obstacle.X, obstacle.Z, obstacle.Width * .5f, obstacle.Depth * .5f);

            // Keep the number of grid lines bounded for authored layouts with large coordinates.
            float margin = Mathf.Max(4, Mathf.Max(_bounds.size.x, _bounds.size.z) * .16f);
            var min = _bounds.min - Vector3.one * margin;
            var max = _bounds.max + Vector3.one * margin;
            float spacing = Mathf.Max(1, Mathf.Ceil(Mathf.Max(max.x - min.x, max.z - min.z) / 70));
            _ground = new GameObject("SkillLab Stage");
            _ground.transform.SetParent(_parent, false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(_ground.transform, false);
            floor.transform.position = new Vector3(_bounds.center.x, -.065f, _bounds.center.z);
            floor.transform.localScale = new Vector3(max.x - min.x, .12f, max.z - min.z);
            floor.GetComponent<Renderer>().sharedMaterial = _floorMaterial;
            Object.Destroy(floor.GetComponent<Collider>());

            var vertices = new List<Vector3>();
            float firstX = Mathf.Ceil(min.x / spacing) * spacing;
            float firstZ = Mathf.Ceil(min.z / spacing) * spacing;
            for (int i = 0; i < 72; i++)
            {
                float x = firstX + spacing * i, z = firstZ + spacing * i;
                if (x <= max.x)
                {
                    vertices.Add(new Vector3(x, .002f, min.z));
                    vertices.Add(new Vector3(x, .002f, max.z));
                }
                if (z <= max.z)
                {
                    vertices.Add(new Vector3(min.x, .002f, z));
                    vertices.Add(new Vector3(max.x, .002f, z));
                }
            }
            _grid = new Mesh { name = "Ground grid" };
            _grid.SetVertices(vertices);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            _grid.SetIndices(indices, MeshTopology.Lines, 0);
            var grid = new GameObject("Grid");
            grid.transform.SetParent(_ground.transform, false);
            grid.AddComponent<MeshFilter>().sharedMesh = _grid;
            grid.AddComponent<MeshRenderer>().sharedMaterial = _gridMaterial;
            ResetView();
        }

        void Include(float x, float z, float halfWidth, float halfDepth)
        {
            _bounds.Encapsulate(new Vector3(x - halfWidth, 0, z - halfDepth));
            _bounds.Encapsulate(new Vector3(x + halfWidth, 2f, z + halfDepth));
        }

        public void ResetView()
        {
            _zoom = 1;
            Frame();
        }

        public void UpdateView(float scroll)
        {
            if (_ground == null) return;
            if (Mathf.Abs(scroll) > .001f)
            {
                _zoom = Mathf.Clamp(_zoom * Mathf.Pow(.82f, scroll), .35f, 3);
                Frame();
            }
            else if (_screenWidth != Screen.width || _screenHeight != Screen.height) Frame();
        }

        void Frame()
        {
            if (_camera == null || _ground == null) return;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            float reserved = Mathf.Min(.42f, SkillLabHud.Bounds.width / Mathf.Max(1, _screenWidth));
            float fittingAspect = Mathf.Max(.25f, _camera.aspect * (1 - reserved));
            float fit = Mathf.Max(_bounds.extents.x / fittingAspect, _bounds.extents.z * .85f + _bounds.extents.y * .53f) + 2;
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(4, fit) * _zoom;
            float distance = Mathf.Max(24, _bounds.size.magnitude + 8);
            _camera.farClipPlane = distance * 3;
            Vector3 center = _bounds.center - Vector3.right * (_camera.orthographicSize * _camera.aspect * reserved);
            _camera.transform.SetPositionAndRotation(center - ViewRotation * Vector3.forward * distance, ViewRotation);
        }

        public void Clear()
        {
            if (_ground != null) Object.Destroy(_ground);
            if (_grid != null) Object.Destroy(_grid);
            _ground = null;
            _grid = null;
        }
    }
}
