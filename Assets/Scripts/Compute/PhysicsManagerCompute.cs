using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class PhysicsManagerCompute : MonoBehaviour
{
    public enum Mode
    {
        Generate,
        Mode1,
        Mode2,
        Mode3
    };

    public Mode GenerateMode; 

    [SerializeField]
    private GameObject spherePrefab;
    [SerializeField]
    private GameObject cubePrefab;
    [SerializeField]
    private GameObject conePrefab;

    public ComputeShader computeShader;
    static readonly int fixedDeltaTimeId = Shader.PropertyToID("_fixedDeltaTime");

    static readonly int spherePosBufId = Shader.PropertyToID("tBufferSphere");
    static readonly int spherePhysBufId = Shader.PropertyToID("pBufferSphere");
    static readonly int sphereBufId = Shader.PropertyToID("sphereBuffer");

    static readonly int cubePosBufId = Shader.PropertyToID("tBufferCube");
    static readonly int cubePhysBufId = Shader.PropertyToID("pBufferCube");
    static readonly int cubeBufId = Shader.PropertyToID("cubeBuffer");

    static readonly int conePosBufId = Shader.PropertyToID("tBufferCone");
    static readonly int conePhysBufId = Shader.PropertyToID("pBufferCone");
    static readonly int coneBufId = Shader.PropertyToID("coneBuffer");

    static readonly int cubeVertexBufId = Shader.PropertyToID("cubeVertices");

    static int sphereKernel, sphereSphereCollisionKernel, sphereCubeCollisionKernel, sphereConeCollisionKernel;
    static int cubeKernel, cubeCubeCollisionKernel, cubeConeCollisionKernel;
    static int coneKernel, coneConeCollisionKernel;

    struct PhysicsData
    {
        public float invMass;
        public Vector3 force;
        public Vector3 velocity;
        public Vector3 torque;
        public Vector3 angularVelocity;
        public Matrix4x4 baseInverseInertiaTensor;
        public Matrix4x4 inverseInertiaTensor;
        public Vector3 minBound;
        public Vector3 maxBound;
    };

    struct TransformData
    {
        public Vector3 pos;
        public Quaternion rot;
        public float scale;
        public Matrix4x4 world;
        public Matrix4x4 local;
    }

    public List<GameObject> spheres;
    public List<GameObject> cubes;
    public List<GameObject> cones;

    private TransformData[] transformArraySpheres;
    private TransformData[] transformArrayCubes;
    private TransformData[] transformArrayCones;

    private ComputeBuffer cBufferRWSpheres;
    private ComputeBuffer cBufferRWCubes;
    private ComputeBuffer cBufferRWCones;

    private PhysicsData[] physicsArraySpheres;
    private PhysicsData[] physicsArrayCubes;
    private PhysicsData[] physicsArrayCones;

    private ComputeBuffer cBufferPhysicsSpheres;
    private ComputeBuffer cBufferPhysicsCubes;
    private ComputeBuffer cBufferPhysicsCones;

    private ComputeBuffer cBufferCubeVertices;

    struct SphereData
    {
        public float radius;
    }

    struct CubeData
    {
        public Vector3 extents;
    }
    struct ConeData
    {
        float boundingSphereRadius;
    };

    private SphereData[] sphereDataArray;
    private CubeData[] cubeDataArray;
    private ConeData[] coneDataArray;

    private ComputeBuffer cBufferDataSpheres;
    private ComputeBuffer cBufferDataCubes;
    private ComputeBuffer cBufferDataCones;

    private Vector3 minContainerBound;
    private Vector3 maxContainerBound;

    void GenerateGameObjects(GameObject prefab, int count, 
        ref TransformData[] transformArray, ref PhysicsData[] physicsArray, ref List<GameObject> objects)
    {
        physicsArray = new PhysicsData[count];
        transformArray = new TransformData[count];
        if (prefab == spherePrefab)
        {
            sphereDataArray = new SphereData[count];
        }
        if (prefab == cubePrefab)
        {
            cubeDataArray = new CubeData[count];
        }
        if (prefab == conePrefab)
        {
            coneDataArray = new ConeData[count];
        }

        PhysicsData pd = new PhysicsData();
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab);
            go.SetActive(false);
            go.transform.localScale = Vector3.one * Random.Range(1.0f, 1.7f);
            go.transform.position = new Vector3(
                Random.Range(minContainerBound.x + 1, maxContainerBound.x - 1),
                Random.Range(minContainerBound.y + 1, maxContainerBound.y - 1),
                Random.Range(minContainerBound.z + 1, maxContainerBound.z - 1));

            pd.invMass = 1.0f / Random.Range(1.0f, 2.0f);
            pd.velocity = new Vector3(
                Random.Range(-1.0f, 1.0f),
                Random.Range(-1.0f, 1.0f),
                Random.Range(-1.0f, 1.0f)) * 10;
            //pd.velocity.x = 5.0f;
            //pd.angularVelocity = Vector3.one * 10.0f;
            pd.force = Vector3.zero;
            pd.torque = Vector3.zero;

            Mesh mesh = go.GetComponentInChildren<MeshFilter>().mesh;
            //baseTriangles = new (Vector3, Vector3, Vector3)[mesh.triangles.Length / 3];
            // triangles = new (Vector3, Vector3, Vector3)[mesh.triangles.Length / 3];
            /*for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                baseTriangles[i / 3] = (mesh.vertices[mesh.triangles[i]], mesh.vertices[mesh.triangles[i + 1]], mesh.vertices[mesh.triangles[i + 2]]);
                triangles[i / 3] = baseTriangles[i / 3];
            }*/
            ComputeLocalBounds(mesh.vertices, out pd.minBound, out pd.maxBound);
            if (prefab == spherePrefab)
            {
                pd.minBound = go.transform.localToWorldMatrix.MultiplyPoint(pd.minBound);
                pd.maxBound = go.transform.localToWorldMatrix.MultiplyPoint(pd.maxBound);
                sphereDataArray[i].radius = (pd.maxBound.x - pd.minBound.x) * 0.5f;
                pd.inverseInertiaTensor.m00 = pd.inverseInertiaTensor.m11 = pd.inverseInertiaTensor.m22 =
                    1.0f / (0.4f / pd.invMass * sphereDataArray[i].radius * sphereDataArray[i].radius);
            }
            if (prefab == cubePrefab)
            {
                cubeDataArray[i].extents = (Vector3.Scale(pd.maxBound - pd.minBound, go.transform.localScale)) * 0.5f;
                pd.inverseInertiaTensor.m00 = pd.inverseInertiaTensor.m11 = pd.inverseInertiaTensor.m22 =
                    1.0f / ((4.0f / 6.0f) / pd.invMass * cubeDataArray[i].extents[0] * cubeDataArray[i].extents[0]);
            }
            //pd.inverseInertiaTensor = pd.inverseInertiaTensor.inverse;
            pd.baseInverseInertiaTensor = pd.inverseInertiaTensor;

            //go.transform.Rotate(Vector3.right, 30.0f);
            //go.transform.Rotate(Vector3.up, 0.0f);
            go.transform.rotation = UnityEngine.Random.rotation;

            transformArray[i].pos = go.transform.position;
            transformArray[i].rot = go.transform.rotation;
            transformArray[i].scale = go.transform.localScale.x;
            transformArray[i].world = go.transform.localToWorldMatrix;
            transformArray[i].local = go.transform.worldToLocalMatrix;

            physicsArray[i] = pd;

            go.SetActive(true);
            objects.Add(go);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Mesh mesh = GetComponentInChildren<MeshFilter>().mesh;
        ComputeLocalBounds(mesh.vertices, out minContainerBound, out maxContainerBound);
        minContainerBound = transform.localToWorldMatrix.MultiplyPoint(minContainerBound);
        maxContainerBound = transform.localToWorldMatrix.MultiplyPoint(maxContainerBound);

        computeShader.SetFloats("minContainerBound", new float[] { minContainerBound.x, minContainerBound.y, minContainerBound.z });
        computeShader.SetFloats("maxContainerBound", new float[] { maxContainerBound.x, maxContainerBound.y, maxContainerBound.z });

        if (GenerateMode == Mode.Mode1)
        {
            GenerateGameObjects(spherePrefab, 100, ref transformArraySpheres, ref physicsArraySpheres, ref spheres);
            GenerateGameObjects(cubePrefab, 250, ref transformArrayCubes, ref physicsArrayCubes, ref cubes);
            //GenerateGameObjects<PhysicsCone>(conePrefab, 500);
        }
        if (GenerateMode == Mode.Mode2)
        {
            GenerateGameObjects(spherePrefab, 250, ref transformArraySpheres, ref physicsArraySpheres, ref spheres);
            GenerateGameObjects(cubePrefab, 500, ref transformArrayCubes, ref physicsArrayCubes, ref cubes);
            //GenerateGameObjects<PhysicsCone>(conePrefab, 1000);
        }
        if (GenerateMode == Mode.Mode3)
        {
            GenerateGameObjects(spherePrefab, 500, ref transformArraySpheres, ref physicsArraySpheres, ref spheres);
            GenerateGameObjects(cubePrefab, 1000, ref transformArrayCubes, ref physicsArrayCubes, ref cubes);
            //GenerateGameObjects<PhysicsCone>(conePrefab, 2500);
        }

        sphereKernel = computeShader.FindKernel("CSGravitySphere");
        cubeKernel = computeShader.FindKernel("CSGravityCube");
        //coneKernel = computeShader.FindKernel("CSGravityCone");

        sphereSphereCollisionKernel = computeShader.FindKernel("CSCollisionSphereSphere");
        sphereCubeCollisionKernel = computeShader.FindKernel("CSCollisionSphereCube");
        //sphereConeCollisionKernel = computeShader.FindKernel("CSCollisionSphereCone");

        cubeCubeCollisionKernel = computeShader.FindKernel("CSCollisionCubeCube");
        //cubeConeCollisionKernel = computeShader.FindKernel("CSCollisionCubeCone");

        //coneConeCollisionKernel = computeShader.FindKernel("CSCollisionConeCone");

        if (spheres.Count > 0)
        {
            cBufferRWSpheres = new ComputeBuffer(spheres.Count, Marshal.SizeOf(transformArraySpheres[0]));
            cBufferRWSpheres.SetData(transformArraySpheres);

            Debug.Log(Marshal.SizeOf(physicsArraySpheres[0]));
            Debug.Log(Marshal.SizeOf<PhysicsData>());
            Debug.Log(Marshal.SizeOf<PhysicsData>(physicsArraySpheres[0]));
            Debug.Log(Marshal.SizeOf<TransformData>());
            cBufferPhysicsSpheres = new ComputeBuffer(spheres.Count, Marshal.SizeOf(physicsArraySpheres[0]));
            cBufferPhysicsSpheres.SetData(physicsArraySpheres);
            
            cBufferDataSpheres = new ComputeBuffer(spheres.Count, Marshal.SizeOf(sphereDataArray[0]));
            cBufferDataSpheres.SetData(sphereDataArray);







            computeShader.SetBuffer(sphereKernel, spherePosBufId, cBufferRWSpheres);
            computeShader.SetBuffer(sphereKernel, spherePhysBufId, cBufferPhysicsSpheres);
            computeShader.SetBuffer(sphereKernel, sphereBufId, cBufferDataSpheres);

            computeShader.SetBuffer(sphereSphereCollisionKernel, spherePosBufId, cBufferRWSpheres);
            computeShader.SetBuffer(sphereSphereCollisionKernel, spherePhysBufId, cBufferPhysicsSpheres);
            computeShader.SetBuffer(sphereSphereCollisionKernel, sphereBufId, cBufferDataSpheres);
        }
        
        if (cubes.Count > 0)
        {
            cBufferRWCubes = new ComputeBuffer(cubes.Count, Marshal.SizeOf(transformArrayCubes[0]));
            cBufferRWCubes.SetData(transformArrayCubes);

            cBufferPhysicsCubes = new ComputeBuffer(cubes.Count, Marshal.SizeOf(physicsArrayCubes[0]));
            cBufferPhysicsCubes.SetData(physicsArrayCubes);

            cBufferDataCubes = new ComputeBuffer(cubes.Count, Marshal.SizeOf(cubeDataArray[0]));
            cBufferDataCubes.SetData(cubeDataArray);

            Mesh cubeMesh = cubePrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            cBufferCubeVertices = new ComputeBuffer(cubeMesh.vertexCount, sizeof(float) * 3);
            cBufferCubeVertices.SetData(cubeMesh.vertices);






            computeShader.SetBuffer(cubeKernel, cubePosBufId, cBufferRWCubes);
            computeShader.SetBuffer(cubeKernel, cubePhysBufId, cBufferPhysicsCubes);
            computeShader.SetBuffer(cubeKernel, cubeBufId, cBufferDataCubes);
            computeShader.SetBuffer(cubeKernel, cubeVertexBufId, cBufferCubeVertices);

            computeShader.SetBuffer(cubeCubeCollisionKernel, cubePosBufId, cBufferRWCubes);
            computeShader.SetBuffer(cubeCubeCollisionKernel, cubePhysBufId, cBufferPhysicsCubes);
            computeShader.SetBuffer(cubeCubeCollisionKernel, cubeBufId, cBufferDataCubes);

            if (spheres.Count > 0)
            {
                computeShader.SetBuffer(sphereCubeCollisionKernel, cubePosBufId, cBufferRWCubes);
                computeShader.SetBuffer(sphereCubeCollisionKernel, cubePhysBufId, cBufferPhysicsCubes);

                computeShader.SetBuffer(sphereCubeCollisionKernel, spherePosBufId, cBufferRWSpheres);
                computeShader.SetBuffer(sphereCubeCollisionKernel, spherePhysBufId, cBufferPhysicsSpheres);
                computeShader.SetBuffer(sphereCubeCollisionKernel, sphereBufId, cBufferDataSpheres);
            }

        }

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        computeShader.SetFloat(fixedDeltaTimeId, Time.fixedDeltaTime);

        if (spheres.Count > 0)
        {
            int uniqueSpherePairs = (spheres.Count) * (spheres.Count - 1) / 2;
            if (spheres.Count > 1)
                computeShader.Dispatch(sphereSphereCollisionKernel, Mathf.CeilToInt(1.0f * uniqueSpherePairs / 32.0f), 1, 1);
        
            computeShader.Dispatch(sphereKernel, Mathf.CeilToInt(1.0f * spheres.Count / 32.0f), 1, 1);
            cBufferRWSpheres.GetData(transformArraySpheres);

            for (int i = 0; i < spheres.Count; i++)
            {
                spheres[i].transform.position = transformArraySpheres[i].pos;
                spheres[i].transform.rotation = transformArraySpheres[i].rot;
            }
        }

        if (cubes.Count > 0)
        {
            int uniqueCubePairs = (cubes.Count) * (cubes.Count - 1) / 2;
            if (cubes.Count > 1)
                computeShader.Dispatch(cubeCubeCollisionKernel, Mathf.CeilToInt(1.0f * uniqueCubePairs / 32.0f), 1, 1);

            //if (spheres.Count > 0)
            //{
            //    computeShader.Dispatch(sphereCubeCollisionKernel,
            //        Mathf.CeilToInt(1.0f * spheres.Count / 8.0f), Mathf.CeilToInt(1.0f * cubes.Count / 8.0f), 1);
            //}

            computeShader.Dispatch(cubeKernel, Mathf.CeilToInt(1.0f * cubes.Count / 32.0f), 1, 1);

            cBufferRWCubes.GetData(transformArrayCubes);

            for (int i = 0; i < cubes.Count; i++)
            {
                cubes[i].transform.position = transformArrayCubes[i].pos;
                cubes[i].transform.rotation = transformArrayCubes[i].rot;
                transformArrayCubes[i].world = cubes[i].transform.localToWorldMatrix;
                transformArrayCubes[i].local = cubes[i].transform.worldToLocalMatrix;
            }
            
            cBufferRWCubes.SetData(transformArrayCubes);
        }

    }

    void OnDestroy()
    {
        cBufferRWSpheres.Release();
        cBufferPhysicsSpheres.Release();
        cBufferDataSpheres.Release();

        cBufferRWCubes.Release();
        cBufferPhysicsCubes.Release();
        cBufferDataCubes.Release();

        cBufferRWCones.Release();
        cBufferPhysicsCones.Release();
        cBufferDataCones.Release();

        cBufferCubeVertices.Release();
    }

    public void ComputeLocalBounds(Vector3[] vertices, out Vector3 minBound, out Vector3 maxBound)
    {
        minBound = vertices[0];
        maxBound = vertices[0];
        foreach (Vector3 vertex in vertices)
        {
            minBound = Vector3.Min(minBound, vertex);
            maxBound = Vector3.Max(maxBound, vertex);
        }
    }

    public Vector3 GetMinContainerBound()
    {
        return minContainerBound;
    }

    public Vector3 GetMaxContainerBound()
    {
        return maxContainerBound;
    }
}
