using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PhysicsCone : PhysicsObject
{
    public float boundingSphereRadius;
    private float baseRadius;
    private float height;
    private Mesh mesh;

    protected override void InitProperties()
    {
        mesh = GetComponentInChildren<MeshFilter>().mesh;
        baseRadius = transform.localScale.x * (maxBound.x - minBound.x) * 0.5f;
        height = transform.localScale.y * (maxBound.y - minBound.y) * 0.5f;
        minBound = transform.localToWorldMatrix.MultiplyPoint(minBound);
        maxBound = transform.localToWorldMatrix.MultiplyPoint(maxBound);
        boundingSphereRadius = Vector3.Distance(maxBound, minBound) * 0.5f;
    }

    public override void UpdateInertiaTensor()
    {
        float r2 = baseRadius * baseRadius;
        float h2 = height * height;
        inertiaTensor.m00 = inertiaTensor.m11 = mass * (3.0f / 20.0f * r2 + 1.0f / 10.0f * h2);
        inertiaTensor.m22 = 3.0f / 20.0f * mass * r2;
        baseInertiaTensor = inertiaTensor;
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
        phys.SphereConeCollision(otherSphere, this);
    }

    public override void HandleCollision(PhysicsCube otherCube)
    {
        phys.CubeConeCollision(otherCube, this);
    }

    public override void HandleCollision(PhysicsCone otherCone)
    {
        if (!phys.SphereSphereIntersect(transform.position, boundingSphereRadius,
            otherCone.transform.position, otherCone.boundingSphereRadius))
            return;

        phys.MeshCollision(this, otherCone);
    }

    private bool InsideContainer(ref Contact contactInfo)
    {
        Vector3 minCont = phys.GetMinContainerBound();
        Vector3 maxCont = phys.GetMaxContainerBound();
        List<Vector3> points = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        float minOverlap = float.MaxValue;

        foreach (Vector3 vertex in mesh.vertices)
        {
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
}
