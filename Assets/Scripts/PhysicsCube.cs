using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PhysicsCube : PhysicsObject
{
    private Vector3 extents;
    private Mesh mesh;

    // Start is called before the first frame update
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    // Update is called once per frame
    void Update()
    {

    }

    protected override void FixedUpdate()
    {
        Contact contactInfo = new Contact();
        if (!InsideContainer(ref contactInfo))
        {
            ResolveCollisionContainer(contactInfo);
        }
        base.FixedUpdate();
    }

    protected override void InitProperties()
    {
        extents = (Vector3.Scale(maxBound - minBound, transform.localScale)) * 0.5f;
    }

    public override void UpdateInertiaTensor()
    {
        inertiaTensor.m00 = inertiaTensor.m11 = inertiaTensor.m22 =  
            (4.0f / 6.0f) * mass * extents[0] * extents[0];
        baseInertiaTensor = inertiaTensor;
    }

    public override bool CheckRaycast(Ray ray, out RaycastHit hit)
    {
        hit = new RaycastHit();
        return false;
    }

    public override void HandleCollision(PhysicsObject other)
    {
        other.HandleCollision(this);
    }

    public override void HandleCollision(PhysicsSphere otherSphere)
    {
        phys.SphereOBBCollision(otherSphere, this);
    }

    private bool InsideContainer(ref Contact contactInfo)
    {
        Vector3 minCont = phys.GetMinContainerBound();
        Vector3 maxCont = phys.GetMaxContainerBound();
        List<Vector3> points = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        float minOverlap = float.MaxValue;

        int vertices = 0;
        foreach (Vector3 vertex in mesh.vertices)
        {
            if (vertices >= 8)
                break;
            Vector3 vertexWorld = transform.localToWorldMatrix.MultiplyPoint3x4(vertex);
            for (int i = 0; i < 3; i++)
            {
                if (vertexWorld[i] < minCont[i])
                {
                    points.Add(vertexWorld);
                    Vector3 normal = Vector3.zero;
                    normal[i] = -1;
                    normals.Add(normal);
                    minOverlap = Mathf.Min(minOverlap, minCont[i] - vertexWorld[i]);
                }
                else if (vertexWorld[i] > maxCont[i])
                {
                    points.Add(vertexWorld);
                    Vector3 normal = Vector3.zero;
                    normal[i] = 1;
                    normals.Add(normal);
                    minOverlap = Mathf.Min(minOverlap, vertexWorld[i] - maxCont[i]);
                }
            }
            vertices++;
        }
        if (points.Count > 0)
        {
            Vector3 sumPoints = Vector3.zero;
            Vector3 sumNormals = Vector3.zero;
            foreach (Vector3 point in points)
                sumPoints += point;
            foreach (Vector3 normal in normals)
                sumNormals += normal;

            contactInfo.contactA = sumPoints / points.Count;
            contactInfo.norm = sumNormals.normalized;
            contactInfo.overlapDist = minOverlap;
            return false;
        }
        
        return true;
    }

    private bool CheckNonOverlap(float R, float R01, 
        Vector3 axis, ref Contact contactInfo)
    {
        float dif = R - R01;
        if (dif > 0.0f)
            return true;

        if (axis == Vector3.zero)
            return false;

        if (contactInfo.overlapDist > -dif)
        {
            contactInfo.overlapDist = -dif;
            contactInfo.norm = axis;
        }

        return false;
    }

   

    public override void HandleCollision(PhysicsCube otherCube)
    {
        Vector3[] A = { transform.right, transform.up, transform.forward };
        Vector3[] B = { otherCube.transform.right, otherCube.transform.up, otherCube.transform.forward };
        Vector3 a = extents;
        Vector3 b = otherCube.extents;
        Vector3 D = otherCube.transform.position - transform.position;
        Matrix4x4 C = Matrix4x4.identity;
        Matrix4x4 Ca = Matrix4x4.identity;
        for (int i = 0; i < A.Length; i++)
            for (int j = 0; j < B.Length; j++)
            {
                C[i, j] = Vector3.Dot(A[i], B[j]);
                Ca[i, j] = Mathf.Abs(C[i, j]);
            }

        Contact contactInfo = new Contact();
        contactInfo.overlapDist = float.MaxValue;

        float A0D = Vector3.Dot(A[0], D);
        float R = Mathf.Abs(A0D);
        float R0 = a[0];
        float R1 = b[0] * Ca[0, 0] + b[1] * Ca[0, 1] + b[2] * Ca[0, 2];
        if (CheckNonOverlap(R, R0 + R1, A[0], ref contactInfo))
            return;

        float A1D = Vector3.Dot(A[1], D);
        R = Mathf.Abs(A1D);
        R0 = a[1];
        R1 = b[0] * Ca[1, 0] + b[1] * Ca[1, 1] + b[2] * Ca[1, 2];
        if (CheckNonOverlap(R, R0 + R1, A[1], ref contactInfo))
            return;

        float A2D = Vector3.Dot(A[2], D);
        R = Mathf.Abs(A2D);
        R0 = a[2];
        R1 = b[0] * Ca[2, 0] + b[1] * Ca[2, 1] + b[2] * Ca[2, 2];
        if (CheckNonOverlap(R, R0 + R1, A[2], ref contactInfo))
            return;

        R = Mathf.Abs(Vector3.Dot(B[0], D));
        R0 = a[0] * Ca[0, 0] + a[1] * Ca[1, 0] + a[2] * Ca[2, 0];
        R1 = b[0];
        if (CheckNonOverlap(R, R0 + R1, B[0], ref contactInfo))
            return;

        R = Mathf.Abs(Vector3.Dot(B[1], D));
        R0 = a[0] * Ca[0, 1] + a[1] * Ca[1, 1] + a[2] * Ca[2, 1];
        R1 = b[1];
        if (CheckNonOverlap(R, R0 + R1, B[1], ref contactInfo))
            return;

        R = Mathf.Abs(Vector3.Dot(B[2], D));
        R0 = a[0] * Ca[0, 2] + a[1] * Ca[1, 2] + a[2] * Ca[2, 2];
        R1 = b[2];
        if (CheckNonOverlap(R, R0 + R1, B[2], ref contactInfo))
            return;

        R = Mathf.Abs(C[1, 0] * A2D - C[2, 0] * A1D);
        R0 = a[1] * Ca[2, 0] + a[2] * Ca[1, 0];
        R1 = b[1] * Ca[0, 2] + b[2] * Ca[0, 1];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[0], B[0])), ref contactInfo))
            return;

        R = Mathf.Abs(C[1, 1] * A2D - C[2, 1] * A1D);
        R0 = a[1] * Ca[2, 1] + a[2] * Ca[1, 1];
        R1 = b[0] * Ca[0, 2] + b[2] * Ca[0, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[0], B[1])), ref contactInfo))
            return;

        R = Mathf.Abs(C[1, 2] * A2D - C[2, 2] * A1D);
        R0 = a[1] * Ca[2, 2] + a[2] * Ca[1, 2];
        R1 = b[0] * Ca[0, 1] + b[1] * Ca[0, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[0], B[2])), ref contactInfo))
            return;

        R = Mathf.Abs(C[2, 0] * A0D - C[0, 0] * A2D);
        R0 = a[0] * Ca[2, 0] + a[2] * Ca[0, 0];
        R1 = b[1] * Ca[1, 2] + b[2] * Ca[1, 1];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[1], B[0])), ref contactInfo))
            return;

        R = Mathf.Abs(C[2, 1] * A0D - C[0, 1] * A2D);
        R0 = a[0] * Ca[2, 1] + a[2] * Ca[0, 1];
        R1 = b[0] * Ca[1, 2] + b[2] * Ca[1, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[1], B[1])), ref contactInfo))
            return;

        R = Mathf.Abs(C[2, 2] * A0D - C[0, 2] * A2D);
        R0 = a[0] * Ca[2, 2] + a[2] * Ca[0, 2];
        R1 = b[0] * Ca[1, 1] + b[1] * Ca[1, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[1], B[2])), ref contactInfo))
            return;

        R = Mathf.Abs(C[0, 0] * A1D - C[1, 0] * A0D);
        R0 = a[0] * Ca[1, 0] + a[1] * Ca[0, 0];
        R1 = b[1] * Ca[2, 2] + b[2] * Ca[2, 1];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[2], B[0])), ref contactInfo))
            return;

        R = Mathf.Abs(C[0, 1] * A1D - C[1, 1] * A0D);
        R0 = a[0] * Ca[1, 1] + a[1] * Ca[0, 1];
        R1 = b[0] * Ca[2, 2] + b[2] * Ca[2, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[2], B[1])), ref contactInfo))
            return;

        R = Mathf.Abs(C[0, 2] * A1D - C[1, 2] * A0D);
        R0 = a[0] * Ca[1, 2] + a[1] * Ca[0, 2];
        R1 = b[0] * Ca[2, 1] + b[1] * Ca[2, 0];
        if (CheckNonOverlap(R, R0 + R1, Vector3.Normalize(Vector3.Cross(A[2], B[2])), ref contactInfo))
            return;

        // Collision Detected (finally)
        // Debug.Log(contactInfo.norm);
        // Debug.Log(contactInfo.overlapDist);
        float extentSum = extents[0] + otherCube.extents[0]; 
        Vector3 dir = otherCube.transform.position - transform.position;
        contactInfo.contactA = transform.position + dir * (extentSum - extents[0]) / extentSum;
        contactInfo.contactB = otherCube.transform.position - dir * (extentSum - otherCube.extents[0]) / extentSum;
        contactInfo.norm *= Mathf.Sign(Vector3.Dot(dir, contactInfo.norm));

        ResolveCollision(otherCube, contactInfo);
    }

    public override void HandleCollision(PhysicsCone otherCone)
    {
        phys.CubeConeCollision(this, otherCone);
    }
}
