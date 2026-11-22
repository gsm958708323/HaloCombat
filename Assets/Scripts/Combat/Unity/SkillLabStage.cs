using System.Collections.Generic;
using Combat.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace Combat.Unity
{
    // Geometry and framing are presentation only. All hit tests belong to the simulation.
    internal sealed class SkillLabStage
    {
        static readonly Quaternion ViewRotation = Quaternion.Euler(55, 0, 0);
        readonly Camera _camera;
        readonly Transform _parent;
        readonly Material _floorMaterial, _gridMaterial, _majorGridMaterial;
        GameObject _ground;
        Mesh _grid;
        Bounds _bounds;
        bool _haveBounds;
        float _zoom = 1f;
        int _screenWidth, _screenHeight;

        public SkillLabStage(Camera camera, Transform parent, Material floor, Material grid, Material majorGrid)
        {
            _camera = camera;
            _parent = parent;
            _floorMaterial = floor;
            _gridMaterial = grid;
            _majorGridMaterial = majorGrid;
        }

        public void Build(SkillSimulationSnapshot snapshot)
        {
            Clear();
            _haveBounds = false;
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Kind == "caster" || actor.Kind == "target")
                    Include(actor.X, actor.Z, actor.Radius, actor.Radius, 2.1f);
            }
            foreach (var obstacle in snapshot.Obstacles)
                Include(obstacle.X, obstacle.Z, obstacle.Width * .5f, obstacle.Depth * .5f, 1f);

            if (!_haveBounds) _bounds = new Bounds(Vector3.zero, new Vector3(2, .1f, 2));

            _ground = new GameObject("SkillLab Stage");
            _ground.transform.SetParent(_parent, false);
            float span = Mathf.Max(_bounds.size.x, _bounds.size.z);
            float margin = Mathf.Max(6, span * .35f);
            Vector3 min = _bounds.min - Vector3.one * margin;
            Vector3 max = _bounds.max + Vector3.one * margin;
            float deckSize = Mathf.Max(120, span * 9);
            var floor = Primitive(PrimitiveType.Cube, _ground.transform, "Matte training floor", _floorMaterial,
                new Vector3(_bounds.center.x, -.061f, _bounds.center.z), new Vector3(deckSize, .12f, deckSize));
            floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            BuildGrid(min, max);
            ResetView();
        }

        void BuildGrid(Vector3 min, Vector3 max)
        {
            var vertices = new List<Vector3>();
            var meters = new List<int>();
            var heavy = new List<int>();
            // Normal layouts use 1 m lines. Enormous layouts retain a bounded sampled grid.
            float spacing = Mathf.Max(1, Mathf.Ceil(Mathf.Max(max.x - min.x, max.z - min.z) / 100));
            int firstX = Mathf.CeilToInt(min.x / spacing), firstZ = Mathf.CeilToInt(min.z / spacing);
            int lastX = Mathf.FloorToInt(max.x / spacing), lastZ = Mathf.FloorToInt(max.z / spacing);
            for (int i = firstX; i <= lastX && i - firstX < 102; i++)
            {
                float x = i * spacing;
                AddLine(new Vector3(x, .007f, min.z), new Vector3(x, .007f, max.z), i % 5 == 0 ? heavy : meters);
            }
            for (int i = firstZ; i <= lastZ && i - firstZ < 102; i++)
            {
                float z = i * spacing;
                AddLine(new Vector3(min.x, .007f, z), new Vector3(max.x, .007f, z), i % 5 == 0 ? heavy : meters);
            }
            AddLine(new Vector3(min.x, .009f, min.z), new Vector3(max.x, .009f, min.z), heavy);
            AddLine(new Vector3(max.x, .009f, min.z), new Vector3(max.x, .009f, max.z), heavy);
            AddLine(new Vector3(max.x, .009f, max.z), new Vector3(min.x, .009f, max.z), heavy);
            AddLine(new Vector3(min.x, .009f, max.z), new Vector3(min.x, .009f, min.z), heavy);
            _grid = new Mesh { name = "SkillLab metric grid", subMeshCount = 2 };
            _grid.SetVertices(vertices);
            _grid.SetIndices(meters, MeshTopology.Lines, 0);
            _grid.SetIndices(heavy, MeshTopology.Lines, 1);
            var grid = new GameObject("Metric markings");
            grid.transform.SetParent(_ground.transform, false);
            grid.AddComponent<MeshFilter>().sharedMesh = _grid;
            var renderer = grid.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _gridMaterial, _majorGridMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            void AddLine(Vector3 a, Vector3 b, List<int> indices)
            {
                indices.Add(vertices.Count);
                vertices.Add(a);
                indices.Add(vertices.Count);
                vertices.Add(b);
            }
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Material material,
            Vector3 position, Vector3 scale)
        {
            var body = GameObject.CreatePrimitive(type);
            body.name = name;
            body.transform.SetParent(parent, false);
            body.transform.localPosition = position;
            body.transform.localScale = scale;
            body.GetComponent<Renderer>().sharedMaterial = material;
            var collider = body.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            return body;
        }

        void Include(float x, float z, float halfWidth, float halfDepth, float height)
        {
            var min = new Vector3(x - halfWidth, 0, z - halfDepth);
            var max = new Vector3(x + halfWidth, height, z + halfDepth);
            if (!_haveBounds)
            {
                _bounds.SetMinMax(min, max);
                _haveBounds = true;
                return;
            }
            _bounds.Encapsulate(min);
            _bounds.Encapsulate(max);
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
            _camera.orthographic = true;
            var up = ViewRotation * Vector3.up;
            var extent = _bounds.extents;
            float height = Mathf.Abs(up.y) * extent.y + Mathf.Abs(up.z) * extent.z;
            float verticalFit = (height + .8f) / .63f;
            float horizontalFit = (extent.x + .7f) / Mathf.Max(.2f, _camera.aspect * .63f);
            _camera.orthographicSize = Mathf.Max(4.6f, verticalFit, horizontalFit) * _zoom;
            // Make room for the skill dock below and the stats panel to the right.
            Vector3 center = _bounds.center - up * (_camera.orthographicSize * .11f)
                + Vector3.right * (_camera.orthographicSize * _camera.aspect * .04f);
            float distance = Mathf.Max(16, _bounds.size.magnitude + 6);
            _camera.farClipPlane = distance * 4;
            _camera.transform.SetPositionAndRotation(center - ViewRotation * Vector3.forward * distance, ViewRotation);
        }

        public void Clear()
        {
            if (_ground != null) { _ground.SetActive(false); Object.Destroy(_ground); }
            if (_grid != null) Object.Destroy(_grid);
            _ground = null;
            _grid = null;
        }
    }
}
