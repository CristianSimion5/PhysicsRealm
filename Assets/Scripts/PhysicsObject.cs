using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;

public abstract class PhysicsObject : MonoBehaviour
{
    public struct Contact
    {
        public Vector3 norm;   // Normal should point from the A object towards the B object
        public float overlapDist;
        public Vector3 contactA;
        public Vector3 contactB;
    }

    public PhysicsManager phys;

    // Physics data
    protected float mass;
    public float Mass
    {
        get { return mass; }
        set { mass = value; UpdateInertiaTensor(); }
    }
    protected Vector3 force;
    public Vector3 velocity;
    protected Vector3 torque;
    protected Vector3 angularVelocity;
    protected Matrix4x4 baseInertiaTensor = Matrix4x4.identity;
    protected Matrix4x4 inertiaTensor = Matrix4x4.identity;

    // Bounding Box data
    public Vector3 minBound;
    public Vector3 maxBound;
    // Geometry data
    private (Vector3 A, Vector3 B, Vector3 C)[] baseTriangles;
    public (Vector3 A, Vector3 B, Vector3 C)[] triangles;

    // Start is called before the first frame update
   /* protected virtual void Start()
    {
       
    }*/

    public void Init(float mass, PhysicsManager manager)
    {
        this.mass = mass;
        phys = manager;
        velocity = Vector3.zero;
        //velocity.x = 5.0f;
        //angularVelocity.x = 2 * Mathf.PI;
        
        Mesh mesh = GetComponentInChildren<MeshFilter>().mesh;
        baseTriangles = new (Vector3, Vector3, Vector3)[mesh.triangles.Length / 3];
        triangles = new (Vector3, Vector3, Vector3)[mesh.triangles.Length / 3];
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            baseTriangles[i / 3] = (mesh.vertices[mesh.triangles[i]], mesh.vertices[mesh.triangles[i + 1]], mesh.vertices[mesh.triangles[i + 2]]);
            triangles[i / 3] = baseTriangles[i / 3];
        }
        phys.ComputeLocalBounds(mesh.vertices, out minBound, out maxBound);
        InitProperties();
        UpdateInertiaTensor();
    }

    protected abstract void InitProperties();

    protected virtual void FixedUpdate()
    {
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i].A = transform.TransformPoint(baseTriangles[i].A);
            triangles[i].B = transform.TransformPoint(baseTriangles[i].B);
            triangles[i].C = transform.TransformPoint(baseTriangles[i].C);
        }

        Vector3 acceleration = force / mass;
        acceleration += PhysicsManager.GRAVITY;
        velocity += acceleration * Time.fixedDeltaTime;
        transform.position += velocity * Time.fixedDeltaTime;

        Matrix4x4 R = Matrix4x4.Rotate(transform.rotation);
        inertiaTensor = R * baseInertiaTensor * R.transpose;
        Vector3 angularAcceleration = inertiaTensor.inverse.MultiplyVector(torque);

        angularVelocity += angularAcceleration * Time.fixedDeltaTime;
        // https://gamedev.stackexchange.com/questions/108920/applying-angular-velocity-to-quaternion
        Vector3 approx = Time.fixedDeltaTime * 0.5f * angularVelocity;
        Quaternion deltaRot = new Quaternion(approx.x, approx.y, approx.z, 0.0f) * transform.rotation;
        deltaRot.x += transform.rotation.x; 
        deltaRot.y += transform.rotation.y; 
        deltaRot.z += transform.rotation.z;
        deltaRot.w += transform.rotation.w;
        transform.rotation = deltaRot.normalized;

        velocity -= velocity * 0.1f * Time.fixedDeltaTime;
        angularVelocity -= angularVelocity * 0.1f * Time.fixedDeltaTime;

        ClearForces();
    }

    public void AddForce(Vector3 force)
    {
        this.force += force;
    }

    public void AddForceAtPosition(Vector3 force, Vector3 position)
    {
        this.force += force;
        this.torque += Vector3.Cross(position - transform.position, force);
    }

    public void AddLinearImpulse(Vector3 force)
    {
        velocity += force / mass;
    }

    public void AddAngularImpulse(Vector3 force)
    {
        angularVelocity += inertiaTensor.inverse.MultiplyVector(force);
    }

    public void ClearForces()
    {
        force = Vector3.zero;
        torque = Vector3.zero;
    }

    public void ResolveCollision(PhysicsObject other, Contact contactInfo)
    {
        Vector3 norm = contactInfo.norm;
        float overlapDist = contactInfo.overlapDist;
        Vector3 contactA = contactInfo.contactA;
        Vector3 contactB = contactInfo.contactB;

        float invMass1 = 1.0f / mass;
        float invMass2 = 1.0f / other.mass;
        float massSum = invMass1 + invMass2;
        transform.position -= norm * overlapDist * invMass1 / massSum;
        other.transform.position += norm * overlapDist * invMass2 / massSum;

        Vector3 rA = contactA - transform.position;
        Vector3 rB = contactB - other.transform.position;

        Vector3 angularA = Vector3.Cross(angularVelocity, rA);
        Vector3 angularB = Vector3.Cross(other.angularVelocity, rB);
        Vector3 fullVelocityA = velocity + angularA;
        Vector3 fullVelocityB = other.velocity + angularB;
        Vector3 fullVelocityDiff = fullVelocityA - fullVelocityB;

        Vector3 thetaA = Vector3.Cross(
            inertiaTensor.inverse.MultiplyVector(Vector3.Cross(rA, norm)), 
            rA);
        Vector3 thetaB = Vector3.Cross(
            other.inertiaTensor.inverse.MultiplyVector(Vector3.Cross(rB, norm)), 
            rB);
        float angularFactor = Vector3.Dot(thetaA + thetaB, norm);

        float e = PhysicsManager.RESTITUTION;
        float impulse = -(1 + e) *
            Vector3.Dot(fullVelocityDiff, norm) /
            (1.0f / mass + 1.0f / other.mass + angularFactor);
        Vector3 impulseNorm = impulse * norm;

        AddLinearImpulse(impulseNorm);
        other.AddLinearImpulse(-impulseNorm);

        AddAngularImpulse(Vector3.Cross(rA, impulseNorm));
        other.AddAngularImpulse(-Vector3.Cross(rB, impulseNorm));
    }

    protected void ResolveCollisionContainer(Contact contactInfo)
    {
        Vector3 norm = contactInfo.norm;
        float overlapDist = contactInfo.overlapDist;
        Vector3 contactA = contactInfo.contactA;
        Vector3 contactB = contactInfo.contactB;

        transform.position -= norm * overlapDist;

        Vector3 rA = contactA - transform.position;

        Vector3 angularA = Vector3.Cross(angularVelocity, rA);
        Vector3 fullVelocityA = velocity + angularA;
        Vector3 fullVelocityDiff = fullVelocityA;

        Vector3 thetaA = Vector3.Cross(
            inertiaTensor.inverse.MultiplyVector(Vector3.Cross(rA, norm)),
            rA);
        float angularFactor = Vector3.Dot(thetaA, norm);

        float e = PhysicsManager.RESTITUTION;
        float impulse = -(1 + e) *
            Vector3.Dot(fullVelocityDiff, norm) /
            (1.0f / mass + angularFactor);
        Vector3 impulseNorm = impulse * norm;

        AddLinearImpulse(impulseNorm);
        AddAngularImpulse(Vector3.Cross(rA, impulseNorm));
    }

    public abstract void UpdateInertiaTensor();

    public abstract bool CheckRaycast(Ray ray, out RaycastHit hit);

    public abstract void HandleCollision(PhysicsObject other);
    public abstract void HandleCollision(PhysicsSphere otherSphere);
    public abstract void HandleCollision(PhysicsCube otherCube);
    public abstract void HandleCollision(PhysicsCone otherCone);
}
