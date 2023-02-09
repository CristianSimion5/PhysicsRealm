using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class PhysicsManager : MonoBehaviour
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

    public static readonly Vector3 GRAVITY = new Vector3(0, -9.81f, 0);
    public static readonly float RESTITUTION = 0.9f;

    public List<PhysicsObject> objects;
    private Vector3 minContainerBound;
    private Vector3 maxContainerBound;

    void GenerateGameObjects<T>(GameObject prefab, int count) where T : PhysicsObject
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab);
            go.SetActive(false);
            go.transform.localScale = Vector3.one * Random.Range(1.0f, 1.7f);
            go.transform.position = new Vector3(
                Random.Range(minContainerBound.x + 1, maxContainerBound.x - 1),
                Random.Range(minContainerBound.y + 1, maxContainerBound.y - 1),
                Random.Range(minContainerBound.z + 1, maxContainerBound.z - 1));
            PhysicsObject pObj = go.AddComponent<T>();
            pObj.Init(Random.Range(1.0f, 2.0f) / 10, this);
            go.transform.rotation = Random.rotation;
            pObj.velocity = new Vector3(
                Random.Range(-1.0f, 1.0f),
                Random.Range(-1.0f, 1.0f),
                Random.Range(-1.0f, 1.0f)) * 10;
            go.SetActive(true);
            objects.Add(pObj);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Mesh mesh = GetComponentInChildren<MeshFilter>().mesh;
        ComputeLocalBounds(mesh.vertices, out minContainerBound, out maxContainerBound);
        minContainerBound = transform.localToWorldMatrix.MultiplyPoint(minContainerBound);
        maxContainerBound = transform.localToWorldMatrix.MultiplyPoint(maxContainerBound);

        int numSpheres = 0, numCubes = 0, numCones = 0;
        if (GenerateMode == Mode.Mode1)
        {
            numSpheres = 100;
            numCubes = 250;
            numCones = 500;
        }
        if (GenerateMode == Mode.Mode2)
        {
            numSpheres = 250;
            numCubes = 500;
            numCones = 1000;
        }
        if (GenerateMode == Mode.Mode3)
        {
            numSpheres = 500;
            numCubes = 1000;
            numCones = 2500;
        }

        if (GenerateMode != Mode.Generate)
        {
            GenerateGameObjects<PhysicsSphere>(spherePrefab, numSpheres);
            GenerateGameObjects<PhysicsCube>(cubePrefab, numCones);
            GenerateGameObjects<PhysicsCone>(conePrefab, numCubes);
        }

        /*
        mesh1 = triangle1.GetComponentInChildren<MeshFilter>().mesh;
        mesh2 = triangle2.GetComponentInChildren<MeshFilter>().mesh;

        s1 = Instantiate(spherePrefab);
        s2 = Instantiate(spherePrefab);
        s1.transform.localScale = Vector3.one * 0.2f;
        s2.transform.localScale = Vector3.one * 0.2f;
        Instantiate(spherePrefab, triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[0]]), transform.rotation);
        Instantiate(spherePrefab, triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[1]]), transform.rotation);
        Instantiate(spherePrefab, triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[2]]), transform.rotation);
        Instantiate(spherePrefab, triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[0]]), transform.rotation);
        Instantiate(spherePrefab, triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[1]]), transform.rotation);
        Instantiate(spherePrefab, triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[2]]), transform.rotation);*/
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            foreach (var physicsObject in objects)
            {
                if (physicsObject.CheckRaycast(ray, out hit))
                {
                    physicsObject.AddForceAtPosition(ray.direction * 100.0f, hit.point);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (GenerateMode == Mode.Generate)
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                GenerateGameObjects<PhysicsSphere>(spherePrefab, 5);
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                GenerateGameObjects<PhysicsCube>(cubePrefab, 5);
            }
            if (Input.GetKeyDown(KeyCode.P))
            {
                GenerateGameObjects<PhysicsCone>(conePrefab, 5);
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        for (int i = 0; i < objects.Count; i++)
        {
            for (int j = i + 1; j < objects.Count; j++)
            {
                objects[i].HandleCollision(objects[j]);
            }
        }

        /* Debugging code 
        (Vector3 first, Vector3 second) pos = (new Vector3(), new Vector3());
        if (TriangleIntersection(
            triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[0]]),
            triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[1]]),
            triangle1.transform.TransformPoint(mesh1.vertices[mesh1.triangles[2]]),
            triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[0]]),
            triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[1]]),
            triangle2.transform.TransformPoint(mesh2.vertices[mesh2.triangles[2]]),
            ref pos))
        {
            s1.transform.position = pos.first;
            s2.transform.position = pos.second;
            //Debug.Log("intersectiune");
        }*/
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

    public bool SphereSphereIntersect(Vector3 pos1, float radius1, Vector3 pos2, float radius2)
    {
        float r = radius1 + radius2;
        return Vector3.SqrMagnitude(pos1 - pos2) < r * r;
    }

    public bool SphereOBBIntersect(Vector3 posSphere, float radius, PhysicsCube obb)
    {
        Vector3 localCenter = obb.transform.InverseTransformPoint(posSphere);
        float localRadius = radius / obb.transform.localScale.x;
        Vector3 localExtents = (obb.maxBound - obb.minBound) * 0.5f;

        Vector3 dir = localCenter;
        Vector3 closestPoint = new Vector3();
        for (int i = 0; i < 3; i++)
        {
            closestPoint[i] = Mathf.Clamp(dir[i], -localExtents[i], localExtents[i]);
        }

        Vector3 dirToContact = closestPoint - localCenter;
        return dirToContact.sqrMagnitude < localRadius * localRadius;
    }

    public void SphereOBBCollision(PhysicsSphere sphere, PhysicsCube obb)
    {
        Vector3 localCenter = obb.transform.InverseTransformPoint(sphere.transform.position);
        float localRadius = sphere.radius / obb.transform.localScale.x;
        Vector3 localExtents = (obb.maxBound - obb.minBound) * 0.5f;

        Vector3 dir = localCenter;
        Vector3 closestPoint = new Vector3();
        for (int i = 0; i < 3; i++)
        {
            closestPoint[i] = Mathf.Clamp(dir[i], -localExtents[i], localExtents[i]);
        }

        Vector3 dirToContact = closestPoint - localCenter;
        if (dirToContact.sqrMagnitude < localRadius * localRadius)
        {
            // Collision detected
            PhysicsObject.Contact contactInfo;
            contactInfo.norm = dirToContact.normalized;
            contactInfo.contactA = contactInfo.norm * localRadius;
            contactInfo.contactB = closestPoint;
            //contactInfo.overlapDist = localRadius - dirToContact.magnitude;

            contactInfo.norm = obb.transform.TransformDirection(contactInfo.norm);
            contactInfo.contactA = obb.transform.TransformPoint(contactInfo.contactA);
            contactInfo.contactB = obb.transform.TransformPoint(contactInfo.contactB);
            contactInfo.overlapDist = sphere.radius - 
                Vector3.Distance(obb.transform.TransformPoint(closestPoint), sphere.transform.position);

            sphere.ResolveCollision(obb, contactInfo);
        }
    }

    public bool TriangleIntersection2D(Vector3 a1, Vector3 b1, Vector3 c1,
        Vector3 a2, Vector3 b2, Vector3 c2)
    {
        Debug.Log("Coplanar detected");
        return false;
    }

    public enum PlaneIntersection
    {
        TRUE,
        FALSE,
        COPLANAR
    }

    public PlaneIntersection TestPlanes(Vector3 a1, Vector3 b1, Vector3 c1,
        Vector3 a2, Vector3 b2, Vector3 c2, ref Vector3 distances, ref Vector3 normal)
    {
        Plane plane = new Plane(a1, b1, c1);
        float d1 = plane.GetDistanceToPoint(a2);
        float d2 = plane.GetDistanceToPoint(b2);
        float d3 = plane.GetDistanceToPoint(c2);
        float eps = 1e-3f;

        bool t1 = Mathf.Abs(d1) < eps + 0.0f;
        bool t2 = Mathf.Abs(d2) < eps + 0.0f;
        bool t3 = Mathf.Abs(d3) < eps + 0.0f;
        if ((t1 && t2 && t3))
        {
            // Coplanar
            return PlaneIntersection.COPLANAR;
        }

        distances[0] = d1;
        distances[1] = d2;
        distances[2] = d3;
        
        float sumSgn = Mathf.Sign(distances[0]) + Mathf.Sign(distances[1]) + Mathf.Sign(distances[2]);
        if (Mathf.Abs(sumSgn) == 3f)
            return PlaneIntersection.FALSE;

        normal = plane.normal;
        return PlaneIntersection.TRUE;
    }

    public Vector2 CheckLineSegment(Vector3 a1, Vector3 b1, Vector3 c1, int Did, Vector3 d, out (Vector3, Vector3) T)
    {
        Vector3 p = new Vector3();
        /* Unoptimized calc, projection on axis is faster
        p[0] = Vector3.Dot(D, a1);
        p[1] = Vector3.Dot(D, b1);
        p[2] = Vector3.Dot(D, c1);*/
        p[0] = a1[Did];
        p[1] = b1[Did];
        p[2] = c1[Did];

        float interp1 = d[0] / (d[0] - d[1]);
        float interp2 = d[2] / (d[2] - d[1]);
        float t1 = p[0] + (p[1] - p[0]) * interp1;
        float t2 = p[2] + (p[1] - p[2]) * interp2;
        T.Item1 = a1 + (b1 - a1) * interp1;
        T.Item2 = c1 + (b1 - c1) * interp2;

        return new Vector2(t1, t2);
    }

    public bool TriangleIntersection(Vector3 a1, Vector3 b1, Vector3 c1,
        Vector3 a2, Vector3 b2, Vector3 c2, ref (Vector3, Vector3) L)
    {
        Vector3[] d = new Vector3[2];
        Vector3[] normals = new Vector3[2];

        PlaneIntersection result = TestPlanes(a1, b1, c1, a2, b2, c2, ref d[1], ref normals[0]);
        if (result == PlaneIntersection.FALSE)
        {
            return false;
        }
        if (result == PlaneIntersection.COPLANAR)
        {
            return TriangleIntersection2D(a1, b1, c1, a2, b2, c2);
        }
        result = TestPlanes(a2, b2, c2, a1, b1, c1, ref d[0], ref normals[1]);
        if (result == PlaneIntersection.FALSE)
        {
            return false;
        }

        Vector3 D = Vector3.Cross(normals[0], normals[1]);
        float Dmaxabs = Mathf.Abs(D.x);
        int Did = 0;
        for (int i = 1; i < 3; i++)
        {
            float Dval = Mathf.Abs(D[i]);
            if (Dmaxabs < Dval)
            { 
                Dmaxabs = Dval;
                Did = i;
            }
        }

        Vector2[] segments = new Vector2[2];
        (Vector3, Vector3)[] segmPoints = new (Vector3, Vector3)[2];
        // Make sure the point that is not on the same side of the plane is the middle argument
        if (Mathf.Sign(d[0][1]) == Mathf.Sign(d[0][2]))
            segments[0] = CheckLineSegment(c1, a1, b1, Did, new Vector3(d[0].z, d[0].x, d[0].y), 
                out segmPoints[0]);
        else if (Mathf.Sign(d[0][0]) == Mathf.Sign(d[0][1]))
            segments[0] = CheckLineSegment(b1, c1, a1, Did, new Vector3(d[0].y, d[0].z, d[0].x),
                out segmPoints[0]);
        else
            segments[0] = CheckLineSegment(a1, b1, c1, Did, d[0],
                out segmPoints[0]);

        if (segments[0].x > segments[0].y)
        {
            float temp = segments[0].x;
            segments[0].x = segments[0].y;
            segments[0].y = temp;
            Vector3 tempv = segmPoints[0].Item1;
            segmPoints[0].Item1 = segmPoints[0].Item2;
            segmPoints[0].Item2 = tempv;
        }

        if (Mathf.Sign(d[1][1]) == Mathf.Sign(d[1][2]))
            segments[1] = CheckLineSegment(c2, a2, b2, Did, new Vector3(d[1].z, d[1].x, d[1].y),
                out segmPoints[1]);
        else if (Mathf.Sign(d[1][0]) == Mathf.Sign(d[1][1]))
            segments[1] = CheckLineSegment(b2, c2, a2, Did, new Vector3(d[1].y, d[1].z, d[1].x),
                out segmPoints[1]);
        else
            segments[1] = CheckLineSegment(a2, b2, c2, Did, d[1],
                out segmPoints[1]);

        if (segments[1].x > segments[1].y)
        {
            float temp = segments[1].x;
            segments[1].x = segments[1].y;
            segments[1].y = temp;
            Vector3 tempv = segmPoints[1].Item1;
            segmPoints[1].Item1 = segmPoints[1].Item2;
            segmPoints[1].Item2 = tempv;
        }

        if (!(
            (segments[0].x <= segments[1].y) &&
            (segments[1].x <= segments[0].y))
            )
            return false;

        if (segments[0].x < segments[1].x)
            L.Item1 = segmPoints[1].Item1;
        else
            L.Item1 = segmPoints[0].Item1;

        if (segments[0].y < segments[1].y)
            L.Item2 = segmPoints[0].Item2;
        else
            L.Item2 = segmPoints[1].Item2;

        return true;
    }

    public void SphereConeCollision(PhysicsSphere sphere, PhysicsCone cone)
    {
        if (!SphereSphereIntersect(sphere.transform.position, sphere.radius,
            cone.transform.position, cone.boundingSphereRadius))
            return;

        MeshCollision(sphere, cone);
    }

    public void CubeConeCollision(PhysicsCube cube, PhysicsCone cone)
    {
        if (!SphereOBBIntersect(cone.transform.position, cone.boundingSphereRadius, cube))
            return;

        MeshCollision(cube, cone);
    }

    public void MeshCollision(PhysicsObject obj1, PhysicsObject obj2)
    {
        (Vector3 A, Vector3 B) points = (new Vector3(), new Vector3());
        PhysicsObject.Contact contactInfo = new PhysicsObject.Contact();
        foreach (var tri1 in obj1.triangles)
        {
            foreach (var tri2 in obj2.triangles)
            {
                if (TriangleIntersection(tri1.A, tri1.B, tri1.C, tri2.A, tri2.B, tri2.C, ref points))
                {
                    Vector3 midpoint = (points.A + points.B) * 0.5f;
                    contactInfo.contactA = midpoint;
                    contactInfo.contactB = midpoint;
                    contactInfo.norm = Vector3.Normalize(obj2.transform.position - obj1.transform.position);//Vector3.Normalize((tri2.A + tri2.B + tri2.C) * 0.333f - midpoint);
                    contactInfo.overlapDist = 0.005f;
                    obj1.ResolveCollision(obj2, contactInfo);
                    break;
                }
            }
        }
    }
}
