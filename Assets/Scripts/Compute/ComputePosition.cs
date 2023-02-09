using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

public class ComputePosition : MonoBehaviour
{
    public int warpCount = 2;
    public ComputeShader computeShader;
    public GameObject refObj;

    private GameObject[] objs;
    private const int warpSize = 32; //match with compute numOfThread X
    private ComputeBuffer cBufferPos, cBufferVelocity;
    private Vector3[] positionArray, velocityArray;

    static readonly int deltaTimeId = Shader.PropertyToID("_fixedDeltaTime");
    static readonly int positionsId = Shader.PropertyToID("positionBuffer");

    void Start()
    {
        //The actual number of particles
        int particleCount = 65;

        // Init particles to same place
        positionArray = new Vector3[particleCount];
        velocityArray = new Vector3[particleCount];

        for (int i = 0; i < particleCount; ++i)
        {
            positionArray[i] = new Vector3(
                Random.Range(-5, 5),
                Random.Range(-5, 5),
                Random.Range(-5, 5)) + transform.position;

            velocityArray[i] = Vector3.zero;
        }

        //Initiate the objects
        objs = new GameObject[particleCount];
        for (int i = 0; i < objs.Length; ++i)
        {
            objs[i] = Instantiate(refObj, this.transform);
            objs[i].transform.position = positionArray[i];
        }

        //init compute buffer
        cBufferPos = new ComputeBuffer(particleCount, sizeof(float) * 3); // 3*4bytes = sizeof(Particle)
        cBufferPos.SetData(positionArray);

        cBufferVelocity = new ComputeBuffer(particleCount, sizeof(float) * 3); // 3*4bytes = sizeof(Particle)
        cBufferVelocity.SetData(velocityArray);

        //set compute buffer to compute shader
        computeShader.SetBuffer(0, positionsId, cBufferPos);
        computeShader.SetBuffer(0, "velocityBuffer", cBufferVelocity);
    }

    void FixedUpdate()
    {
        computeShader.SetFloat(deltaTimeId, Time.fixedDeltaTime);
        //run the compute shader, the position of particles will be updated in GPU
        computeShader.Dispatch(0, warpCount, 1, 1);

        //Get data back from GPU to CPU
        cBufferPos.GetData(positionArray);
        //cBufferVelocity.GetData(velocityArray);

        //Place the GameObjects
        for (int i = 0; i < positionArray.Length; ++i)
        {
            objs[i].transform.position = positionArray[i];
        }
    }

    void OnDestroy()
    {
        //remember to release for every compute buffer
        cBufferPos.Release();
        cBufferVelocity.Release();
    }
}