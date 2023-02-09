using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class PhysicsSphere : PhysicsObject
{
    public float radius;

    /* // Start is called before the first frame update
     protected override void Start()
     {
         base.Start();
     }*/

    protected override void InitProperties()
    {
        // Transform to world coordinates
        minBound = transform.localToWorldMatrix.MultiplyPoint(minBound);
        maxBound = transform.localToWorldMatrix.MultiplyPoint(maxBound);
        radius = (maxBound.x - minBound.x) * 0.5f;
    }
    public override void UpdateInertiaTensor()
    {
        inertiaTensor.m00 = inertiaTensor.m11 = inertiaTensor.m22 = 0.4f * mass * radius * radius;
        baseInertiaTensor = inertiaTensor;
    }

    protected override void FixedUpdate()
    {
        // Sphere - Container collision test and resolution
        Vector3 center = transform.position;
        float dist;
        float r2 = Mathf.Pow(radius, 2);
        Vector3 cmin = phys.GetMinContainerBound();
        Vector3 cmax = phys.GetMaxContainerBound();

        for (int i = 0; i < 3; i++)
        {
            dist = Mathf.Pow(center[i] - cmin[i], 2);
            if (dist <= r2)
            {
                center[i] = cmin[i] + radius;
                velocity[i] = -PhysicsManager.RESTITUTION * velocity[i];
            }
            dist = Mathf.Pow(center[i] - cmax[i], 2);
            if (dist <= r2)
            {
                center[i] = cmax[i] - radius;
                velocity[i] = -PhysicsManager.RESTITUTION * velocity[i];
            }
        }
        transform.position = center;
        base.FixedUpdate();
    }
   
    public override bool CheckRaycast(Ray ray, out RaycastHit hit)
    {
        hit = new RaycastHit();
        Vector3 dir = transform.position - ray.origin;
        float dirDotRay = Vector3.Dot(dir, ray.direction);
        if (dirDotRay < 0.0f) return false;

        Vector3 centerOnRay = ray.GetPoint(dirDotRay);

        float dist = Vector3.Distance(transform.position, centerOnRay);
        if (dist > radius) return false;

        float insideSphereOffset = Mathf.Sqrt(radius * radius - dist * dist);
        hit.distance = dirDotRay - insideSphereOffset;
        hit.point = ray.GetPoint(hit.distance);

        return true;
    }

    public override void HandleCollision(PhysicsObject other)
    {
        other.HandleCollision(this);
    }

    public override void HandleCollision(PhysicsSphere otherSphere)
    {
        // Sphere - Sphere collision test and resolution
        // if (radius + otherSphere.radius > dist)
        if (phys.SphereSphereIntersect(transform.position, radius, 
            otherSphere.transform.position, otherSphere.radius))
        {
            float dist = Vector3.Distance(transform.position, otherSphere.transform.position);
            // Debug.Log("collision sphere-sphere");
            Contact contactInfo = new Contact();
            // Normal towards other object
            contactInfo.norm = Vector3.Normalize(otherSphere.transform.position - transform.position);
            contactInfo.contactA = transform.position + contactInfo.norm * radius;
            contactInfo.contactB = otherSphere.transform.position - contactInfo.norm * otherSphere.radius;
            contactInfo.overlapDist = radius + otherSphere.radius - dist;

            ResolveCollision(otherSphere, contactInfo);
        }
    }
    public override void HandleCollision(PhysicsCube otherCube)
    {
        phys.SphereOBBCollision(this, otherCube);
    }

    public override void HandleCollision(PhysicsCone otherCone)
    {
        phys.SphereConeCollision(this, otherCone);
    }
}
