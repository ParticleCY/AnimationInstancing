using System;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeHelper
{
    // Merge all bones to a single array and merge all bind pose
    /*
     * Input:
     * meshRender: SkinnedMeshRenderer get form prefab.
     * Inout:
     * bindPose: update bindPose, note that the index of bindpose is correspond to the index of bones.
     * Output:
     * listTransform: used as a map of bones.
     */
    public static Transform[] MergeBone(SkinnedMeshRenderer[] meshRender, List<Matrix4x4> bindPose)
    {
        UnityEngine.Profiling.Profiler.BeginSample("MergeBone()");
        List<Transform> listTransform = new List<Transform>(150);
        for (int i = 0; i != meshRender.Length; ++i)
        {
            //checkBindPose saves the bindpose of each bones in the mesh
            Transform[] bones = meshRender[i].bones;
            Matrix4x4[] checkBindPose = meshRender[i].sharedMesh.bindposes;
            //这里他把不同的mesh里面的骨骼都拿了出来拼在一起，但是bindposes是从sharedMesh里面拿出来的，最后返回出去，有趣的事，bindposes是
            //唯一的，所以在遍历meshRender取出bones的过程当中，已经有的Bones会用新的Mesh里面的bindposes去覆盖原有的bindposes，所以这个bindposes到底意义何在？
            for (int j = 0; j != bones.Length; ++j)
            {
#if UNITY_EDITOR
                Debug.Assert(checkBindPose[j].determinant != 0, "The bind pose can't be 0 matrix.");
#endif
                // the bind pose is correct base on the skinnedMeshRenderer, so we need to replace it
                //find bones in the listTransform
                int index = listTransform.FindIndex(q => q == bones[j]);
                if (index < 0)
                {
                    //if the bone is not in the listTransform, then add it and add a bindpose
                    listTransform.Add(bones[j]);
                    if (bindPose != null)
                    {
                        bindPose.Add(checkBindPose[j]);
                    }
                }
                else
                {
                    //测试了一下发现，不同的sharedMesh里面得到的相同骨骼的bindPose是相同的，可能就是相同的，那这里这个复制就没有意义，如果不是相同的，为什么以新的Mesh里面的为准？
                    //这里就看作得到的都相同，为BoneBindPose
                    //else update bindpose
                    bindPose[index] = checkBindPose[j];
                }
            }

            //disable defaut meshRender
            meshRender[i].enabled = false;
        }

        UnityEngine.Profiling.Profiler.EndSample();
        return listTransform.ToArray();
    }

    public static Quaternion QuaternionFromMatrix(Matrix4x4 mat)
    {
        Vector3 forward;
        forward.x = mat.m02;
        forward.y = mat.m12;
        forward.z = mat.m22;

        Vector3 upwards;
        upwards.x = mat.m01;
        upwards.y = mat.m11;
        upwards.z = mat.m21;

        return Quaternion.LookRotation(forward, upwards);
    }
}